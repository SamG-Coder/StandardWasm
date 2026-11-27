using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("The window class oracle requires the x86 CLR.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new { clr = Environment.Version.ToString(), outcomes = Witness.Run() }));

internal static class Witness
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Module, Icon, Cursor, Background, Menu, Name;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int WindowProcedure(nint window, uint message, uint wparam, int lparam);
    [DllImport("kernel32", ExactSpelling = true)] private static extern nint GetModuleHandleW(nint name);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern ushort RegisterClassW(in WindowClass definition);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern int GetClassInfoW(nint module, nint name, out WindowClass definition);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)] private static extern int UnregisterClassW(nint name, nint module);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern nint CreateWindowExW(uint exStyle, nint name, nint caption, uint style, int x, int y,
        int width, int height, nint parent, nint menu, nint module, nint parameter);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern int SetWindowLongW(nint window, int index, int value);
    [DllImport("user32", ExactSpelling = true, SetLastError = true)]
    private static extern int GetWindowLongW(nint window, int index);
    [DllImport("user32", ExactSpelling = true)]
    private static extern int CallWindowProcW(nint procedure, nint window, uint message, uint wparam, int lparam);
    [DllImport("user32", ExactSpelling = true)]
    private static extern int DefWindowProcW(nint window, uint message, uint wparam, int lparam);
    [DllImport("user32", ExactSpelling = true)] private static extern int DestroyWindow(nint window);
    [DllImport("user32", ExactSpelling = true)] private static extern int EnableWindow(nint window, int enabled);

    internal static object Run()
    {
        const int procedureIndex = -4;
        const uint witnessMessage = 0x8433;
        var messages = new List<int[]>();
        WindowProcedure original = (window, message, wparam, lparam) =>
            Dispatch(1, window, message, wparam, lparam);
        WindowProcedure replacement = (window, message, wparam, lparam) =>
            Dispatch(2, window, message, wparam, lparam);
        nint originalAddress = Marshal.GetFunctionPointerForDelegate(original);
        nint replacementAddress = Marshal.GetFunctionPointerForDelegate(replacement);
        nint module = GetModuleHandleW(0);
        nint name = Marshal.StringToHGlobalUni("StandardWasm.Stage184.É");
        nint caseVariant = Marshal.StringToHGlobalUni("standardwasm.stage184.É");
        nint otherUnicode = Marshal.StringToHGlobalUni("StandardWasm.Stage184.é");
        nint unknown = Marshal.StringToHGlobalUni("StandardWasm.Stage184.Missing");
        ushort atom = 0;
        nint window = 0;
        try
        {
            var definition = new WindowClass { Style = 3, Procedure = originalAddress, ClassExtra = 4,
                WindowExtra = 8, Module = module, Menu = 7, Name = name };
            atom = RegisterClassW(definition);
            int registerError = Marshal.GetLastPInvokeError();
            ushort exactDuplicate = RegisterClassW(definition);
            int exactDuplicateError = Marshal.GetLastPInvokeError();
            definition.Name = caseVariant;
            ushort duplicate = RegisterClassW(definition);
            int duplicateError = Marshal.GetLastPInvokeError();
            int named = GetClassInfoW(module, caseVariant, out WindowClass nameInfo);
            int nameError = Marshal.GetLastPInvokeError();
            int byAtom = GetClassInfoW(module, atom, out WindowClass atomInfo);
            int atomError = Marshal.GetLastPInvokeError();
            int missing = GetClassInfoW(module, unknown, out _);
            int missingError = Marshal.GetLastPInvokeError();
            int otherModule = GetClassInfoW(0, atom, out _);
            int otherModuleError = Marshal.GetLastPInvokeError();
            int unicodeCase = GetClassInfoW(module, otherUnicode, out _);
            int unicodeCaseError = Marshal.GetLastPInvokeError();
            definition.Name = name;
            definition.Module = 0x01234567;
            ushort differentModuleRegistration = RegisterClassW(definition);
            int differentModuleError = Marshal.GetLastPInvokeError();
            int namedOther = GetClassInfoW(definition.Module, name, out _);
            int atomOther = GetClassInfoW(definition.Module, atom, out _);
            if (differentModuleRegistration != 0) UnregisterClassW(name, definition.Module);

            window = CreateWindowExW(0, name, 0, 0, 13, 17, 161, 103, 0, 0, module, 0);
            if (window == 0) throw new InvalidOperationException($"Native HWND creation failed ({Marshal.GetLastPInvokeError()}).");
            int initial = GetWindowLongW(window, procedureIndex);
            int prior = SetWindowLongW(window, procedureIndex, (int)replacementAddress);
            int changeError = Marshal.GetLastPInvokeError();
            int current = GetWindowLongW(window, procedureIndex);
            int oldResult = CallWindowProcW(prior, window, witnessMessage, 30, -12);
            int newResult = CallWindowProcW(current, window, witnessMessage, 30, -12);
            int badIndex = SetWindowLongW(window, -1000, (int)replacementAddress);
            int badIndexError = Marshal.GetLastPInvokeError();
            int badWindow = SetWindowLongW(0, procedureIndex, (int)replacementAddress);
            int badWindowError = Marshal.GetLastPInvokeError();
            messages.Clear();
            EnableWindow(window, 0);
            EnableWindow(window, 1);

            return new
            {
                structBytes = Marshal.SizeOf<WindowClass>(),
                registered = atom != 0, registerError, exactDuplicate, exactDuplicateError,
                duplicate, duplicateError,
                namedEqualsAtom = named == atom, nameError, atomEqualsAtom = byAtom == atom, atomError,
                missing, missingError, otherModule, otherModuleError, unicodeCase, unicodeCaseError,
                differentModuleRegistered = differentModuleRegistration != 0, differentModuleError,
                sameAtomAcrossModule = atom == differentModuleRegistration,
                differentModuleByName = namedOther == differentModuleRegistration, differentModuleByAtom = atomOther == differentModuleRegistration,
                namedInfo = Fields(nameInfo), atomInfo = Fields(atomInfo),
                initialMatches = initial == originalAddress, previousMatches = prior == initial,
                currentMatches = current == replacementAddress, changeError, oldResult, newResult,
                badIndex, badIndexError, badWindow, badWindowError, messages = messages.ToArray()
            };
        }
        finally
        {
            if (window != 0) DestroyWindow(window);
            if (atom != 0) UnregisterClassW(name, module);
            UnregisterClassW(caseVariant, module);
            GC.KeepAlive(original);
            GC.KeepAlive(replacement);
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(caseVariant);
            Marshal.FreeHGlobal(otherUnicode);
            Marshal.FreeHGlobal(unknown);
        }

        object Fields(WindowClass definition) => new
        {
            style = definition.Style, original = definition.Procedure == originalAddress,
            classExtra = definition.ClassExtra, windowExtra = definition.WindowExtra,
            module = definition.Module == module, icon = definition.Icon.ToInt32(),
            cursor = definition.Cursor.ToInt32(), background = definition.Background.ToInt32(),
            menu = definition.Menu.ToInt32(),
            name = definition.Name == caseVariant ? "query name" : definition.Name == atom ? "query atom" :
                definition.Name == 0 ? "null" : Marshal.PtrToStringUni(definition.Name)
        };
        int Dispatch(int procedure, nint window, uint message, uint wparam, int lparam)
        {
            if (message is 0x1f or 0x0a) messages.Add([procedure, (int)message, (int)wparam, lparam]);
            if (message == witnessMessage) return procedure == 1 ? (int)wparam + lparam : (int)wparam - lparam;
            return DefWindowProcW(window, message, wparam, lparam);
        }
    }
}
