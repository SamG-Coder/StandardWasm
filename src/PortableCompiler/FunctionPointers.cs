namespace PortableCompiler
{
    // Stage 150 (decision 0078): managed function pointers. Slot k (1-based) of table 2 holds
    // the k-th address-taken method; slot 0 stays null. Each distinct calli signature has a
    // carrier: a synthetic function (rows = 0, body unreachable) whose type the call_indirect
    // names. Function type indices shift as classes are discovered, so each carrier keeps a
    // WasmIndexFixups chain that WriteModule resolves.
    internal sealed class FunctionPointerPlan
    {
        internal const int Table = 2;
        internal int[] Targets = new int[8];   // function index per slot, slot k at [k - 1]
        internal int TargetCount;
        internal int[] Carriers = new int[4];  // carrier function indices
        internal int[] Heads = new int[4];     // fixup chain head per carrier
        internal int CarrierCount;

        // The slot for function, assigned once so two loads of &M compare equal.
        internal int Slot(int function)
        {
            for (int i = 0; i < TargetCount; i++)
                if (Targets[i] == function) return i + 1;
            if (TargetCount == Targets.Length) Targets = Grow(Targets);
            Targets[TargetCount++] = function;
            return TargetCount;
        }

        // The carrier whose signature equals types[start .. start + arity] (result first), or -1.
        internal int FindCarrier(int[] arities, int[] offsets, int[] signatures, int[] types, int start, int arity)
        {
            for (int c = 0; c < CarrierCount; c++)
            {
                int carrier = Carriers[c];
                if (arities[carrier] != arity) continue;
                int shape = offsets[carrier];
                bool same = true;
                for (int i = 0; i <= arity && same; i++) same = signatures[shape + i] == types[start + i];
                if (same) return c;
            }
            return -1;
        }

        internal int AddCarrier(int function)
        {
            if (CarrierCount == Carriers.Length)
            {
                Carriers = Grow(Carriers);
                Heads = Grow(Heads);
            }
            Carriers[CarrierCount] = function;
            Heads[CarrierCount] = 0;
            return CarrierCount++;
        }

        private static int[] Grow(int[] values)
        {
            var grown = new int[values.Length * 2];
            for (int i = 0; i < values.Length; i++) grown[i] = values[i];
            return grown;
        }
    }

    internal static class FunctionPointerWasm
    {
        // call_indirect (type <carrier, fixed up>) 2. The pointer is on the Wasm stack last.
        internal static int CallIndirect(byte[] code, int used, int bodyStart, FunctionPointerPlan plan, int carrier)
        {
            code[used++] = 0x11;
            used = WasmIndexFixups.WriteAt(code, used, bodyStart, plan.Heads, carrier);
            code[used++] = FunctionPointerPlan.Table;
            return used;
        }

        // A carrier is never called: call_indirect only uses its type. Reaching it would
        // mean its own slot was taken, which never happens.
        internal static int CarrierBody(byte[] code, int used)
        {
            code[used++] = 0; // no locals
            code[used++] = 0x00; // unreachable
            code[used++] = 0x0b;
            return used;
        }
    }
}
