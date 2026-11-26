using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace WinFormsCensus;

/// <summary>Why a method entered the reached set (the first edge that reached it).</summary>
internal readonly record struct Reason(string Kind, MethodId? From, int Offset);

/// <summary>
/// Conservative whole-program reachability using rapid type analysis (RTA).
///
/// Edges: call/newobj/ldftn targets; callvirt/ldvirtftn slots resolved to the
/// override or implementation in every type that reached code instantiates
/// (newobj, box, constrained., or a delegate allocation); the static
/// constructor of every type whose member, static field or constructor is
/// reached (ignoring the BeforeFieldInit relaxation, which can only delay it);
/// module initializers of every assembly that owns reached code; and Finalize
/// overrides of instantiated types, which the runtime invokes.
///
/// It is definition-granular: generic instantiations collapse onto their
/// definitions. It does not model reflection, Activator, TypeDescriptor
/// attribute activation, COM activation or runtime-synthesized array
/// interfaces; those sites are counted separately so the gap stays visible.
/// </summary>
internal sealed class CallGraph
{
    private readonly AssemblySet _set;
    private readonly Queue<MethodId> _work = new();
    public readonly Dictionary<MethodId, Reason> Reached = new();
    public readonly HashSet<TypeId> Instantiated = new();
    public readonly HashSet<MethodId> VirtualSlots = new();
    public readonly HashSet<TypeId> InitializedTypes = new();
    public readonly HashSet<int> InitializedModules = new();
    public readonly Dictionary<MethodId, List<Instruction>> Bodies = new();
    public readonly List<string> UnresolvedTokens = new();
    public int UnresolvedCount;
    // Slots whose declaring type is an ancestor: indexed by declaring type so a
    // newly instantiated type only checks slots it can actually implement.
    private readonly Dictionary<TypeId, List<MethodId>> _slotsByOwner = new();

    public CallGraph(AssemblySet set) => _set = set;

    public void Run(MethodId entry)
    {
        Add(entry, new Reason("entry", null, -1));
        while (_work.Count > 0) Visit(_work.Dequeue());
    }

    private void Add(MethodId method, Reason reason)
    {
        if (Reached.ContainsKey(method)) return;
        Reached[method] = reason;
        _work.Enqueue(method);
        TypeFacts owner = _set.TypeOf(method);
        Initialize(owner.Id, method);
        if (InitializedModules.Add(method.Assembly))
        {
            // <Module> is TypeDef row 1; its .cctor is the module initializer.
            TypeFacts module = _set.Type(new TypeId(method.Assembly, 1));
            if (module.StaticConstructor is not null) Add(module.StaticConstructor.Value, new Reason("module initializer", method, -1));
        }
    }

    private void Initialize(TypeId type, MethodId from)
    {
        if (!InitializedTypes.Add(type)) return;
        TypeFacts info = _set.Type(type);
        if (info.StaticConstructor is not null) Add(info.StaticConstructor.Value, new Reason("type initializer", from, -1));
    }

    private void Instantiate(TypeId type, MethodId from)
    {
        if (!Instantiated.Add(type)) return;
        Initialize(type, from);
        TypeFacts info = _set.Type(type);
        foreach (TypeId ancestor in info.Ancestors)
            if (_slotsByOwner.TryGetValue(ancestor, out List<MethodId>? slots))
                foreach (MethodId slot in slots.ToArray()) Dispatch(type, slot, from);
        // The finalizer thread calls Object.Finalize overrides.
        for (TypeId? current = type; current is not null; current = _set.Type(current.Value).BaseType)
            foreach (MethodId method in _set.Type(current.Value).Methods)
                if (_set.MethodName(method) == "Finalize" && _set.Definition(method).GetParameters().Count == 0 &&
                    (_set.Definition(method).Attributes & MethodAttributes.Virtual) != 0 && _set.Type(current.Value).FullName != "System.Object")
                {
                    Add(method, new Reason("finalizer", from, -1));
                    return;
                }
    }

    private void AddSlot(MethodId slot, MethodId from, int offset)
    {
        MethodDefinition definition = _set.Definition(slot);
        bool isVirtual = (definition.Attributes & MethodAttributes.Virtual) != 0;
        // The declared target is reachable when it has a body (non-virtual
        // callvirt, base implementation or default interface method).
        if ((definition.Attributes & MethodAttributes.Abstract) == 0) Add(slot, new Reason("callvirt", from, offset));
        else Initialize(_set.TypeOf(slot).Id, from);
        if (!isVirtual || !VirtualSlots.Add(slot)) return;
        TypeId owner = _set.TypeOf(slot).Id;
        if (!_slotsByOwner.TryGetValue(owner, out List<MethodId>? list)) _slotsByOwner[owner] = list = new();
        list.Add(slot);
        foreach (TypeId type in Instantiated.ToArray())
            if (_set.Type(type).Ancestors.Contains(owner)) Dispatch(type, slot, from);
    }

    /// <summary>Adds every method that can execute for <paramref name="slot"/> on an exact <paramref name="type"/>.</summary>
    private void Dispatch(TypeId type, MethodId slot, MethodId from)
    {
        TypeFacts slotOwner = _set.TypeOf(slot);
        string name = _set.MethodName(slot), signature = _set.MethodSignature(slot);
        var reason = new Reason("virtual dispatch", from, -1);
        for (TypeId? current = type; current is not null && current != slotOwner.Id; current = _set.Type(current.Value).BaseType)
        {
            TypeFacts info = _set.Type(current.Value);
            bool found = false;
            foreach ((EntityHandle declaration, MethodId body) in info.MethodImpls)
            {
                MethodId? declared = _set.ResolveMethod(_set.All[current.Value.Assembly], declaration, out _);
                if (declared == slot) { Add(body, reason); found = true; }
            }
            if (found) return;
            foreach (MethodId candidate in info.Methods)
            {
                MethodDefinition definition = _set.Definition(candidate);
                if ((definition.Attributes & MethodAttributes.Virtual) == 0 || (definition.Attributes & MethodAttributes.Abstract) != 0) continue;
                string candidateName = _set.MethodName(candidate);
                if (candidateName != name) continue;
                string candidateSignature = _set.MethodSignature(candidate);
                if (candidateSignature == signature || Signatures.SameShape(candidateSignature, signature))
                {
                    // Conservative: the most derived match executes unless it
                    // is newslot; keep walking past newslot members.
                    Add(candidate, reason);
                    if (slotOwner.IsInterface || (definition.Attributes & MethodAttributes.NewSlot) == 0) return;
                }
            }
        }
    }

    private void Visit(MethodId method)
    {
        LoadedAssembly assembly = _set.All[method.Assembly];
        MethodDefinition definition = _set.Definition(method);
        if (definition.RelativeVirtualAddress == 0) return;
        MethodBodyBlock body = assembly.Pe.GetMethodBody(definition.RelativeVirtualAddress);
        List<Instruction> instructions = IlInstructions.Decode(body);
        Bodies[method] = instructions;
        for (int i = 0; i < instructions.Count; i++)
        {
            Instruction instruction = instructions[i];
            string op = instruction.OpCode.Name!;
            if (instruction.Token == 0) continue;
            EntityHandle token = IlInstructions.Entity(instruction.Token);
            switch (op)
            {
                case "call":
                case "newobj":
                case "ldftn":
                case "jmp":
                {
                    MethodId? target = _set.ResolveMethod(assembly, token, out _);
                    if (target is null) { Unresolved(method, instruction); break; }
                    if (op == "newobj")
                    {
                        TypeFacts owner = _set.TypeOf(target.Value);
                        Instantiate(owner.Id, method);
                    }
                    Add(target.Value, new Reason(op, method, instruction.Offset));
                    break;
                }
                case "callvirt":
                case "ldvirtftn":
                {
                    MethodId? target = _set.ResolveMethod(assembly, token, out _);
                    if (target is null) { Unresolved(method, instruction); break; }
                    if ((instruction.Prefixes & IlInstructions.Constrained) != 0 && i > 0)
                    {
                        TypeId? constrained = _set.ResolveType(assembly, IlInstructions.Entity(instructions[i - 1].Token));
                        if (constrained is not null) Instantiate(constrained.Value, method);
                    }
                    AddSlot(target.Value, method, instruction.Offset);
                    break;
                }
                case "box":
                {
                    TypeId? boxed = _set.ResolveType(assembly, token);
                    if (boxed is not null) Instantiate(boxed.Value, method);
                    break;
                }
                case "ldsfld":
                case "ldsflda":
                case "stsfld":
                {
                    (TypeId? owner, _) = _set.ResolveFieldOwner(assembly, token);
                    if (owner is not null) Initialize(owner.Value, method);
                    else Unresolved(method, instruction);
                    break;
                }
            }
        }
    }

    private void Unresolved(MethodId method, Instruction instruction)
    {
        UnresolvedCount++;
        if (UnresolvedTokens.Count < 200)
            UnresolvedTokens.Add($"{_set.Display(method)} IL_{instruction.Offset:x4} {instruction.OpCode.Name} 0x{instruction.Token:x8}");
    }
}
