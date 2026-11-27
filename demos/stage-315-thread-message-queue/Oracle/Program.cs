using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("Expected Windows x86 CLR.");

const uint first = 0x8049, second = 0x8050, third = 0x8051;
uint thread = Guest.ThreadId();
var operations = new List<object>();
Peek(0, first, third, 0);
Post(true, first, 1, -2, 1);
Post(true, second, 99, -99, 1);
Post(true, first, 0x80000001, -17, 0);
Post(false, first, 7, -8, 0);
Post(false, first, 7, -8, 1);
Peek(-1, second, third, 0x00200000); // PM_QS_PAINT
Peek(-1, second, third, 0x00980000); // PM_QS_POSTMESSAGE
Peek(-1, second, third, 2);
Peek(-1, second, third, 3);
Peek(-1, second, third, 1);
Peek(0, 0, 0, 1);
Peek(-1, first, first, 1);
Peek(-1, first, first, 1);
Post(true, 0x12, 23, 0, 1);
Peek(-1, first, first, 1);
Peek(-1, 0, 0, 1);

File.WriteAllText(args[0], JsonSerializer.Serialize(new {
    assembly = typeof(Guest).Assembly.Location, runtime = Environment.Version.ToString(), operations
}));

void Post(bool current, uint message, nuint wParam, nint lParam, int capture)
{
    Guest.SetSavedError(789);
    int result = Guest.Post(current ? thread : uint.MaxValue, message, wParam, lParam, capture);
    operations.Add(new { kind = "post", current, message, wParam = (uint)wParam, lParam = (int)lParam, capture,
        expected = new { result, error = Guest.SavedError() } });
}

unsafe void Peek(int window, uint minimum, uint maximum, uint flags)
{
    int* output = stackalloc int[8];
    for (int i = 0; i < 8; i++) output[i] = -1;
    int result = Guest.Peek((nint)output, window, minimum, maximum, flags);
    operations.Add(new { kind = "peek", window, minimum, maximum, flags,
        expected = new { result, hwnd = output[0], message = (uint)output[1], wParam = (uint)output[2], lParam = output[3],
            timestampWritten = output[4] != -1, pointWritten = output[5] != -1 && output[6] != -1, guard = output[7] } });
}
