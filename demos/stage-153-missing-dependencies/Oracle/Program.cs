using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Issue #299 (Stage 153). The CLR runs each kernel with MissingDependency.dll absent: the
// fixture is copied alone into the evidence folder, so the loader cannot find its dependency.
string evidence = args[0], built = Path.GetFullPath(args[1]);
string alone = Path.Combine(evidence, "clr");
Directory.CreateDirectory(alone);
string fixture = Path.Combine(alone, Path.GetFileName(built));
File.Copy(built, fixture, overwrite: true);
if (File.Exists(Path.Combine(alone, "MissingDependency.dll"))) throw new InvalidOperationException("The dependency must be absent.");
Type kernels = Assembly.LoadFrom(fixture).GetType("MissingDependencyFixture.Kernels", throwOnError: true)!;

var cases = new List<object>();
void Run(string name, params int[] inputs)
{
    MethodInfo method = kernels.GetMethod(name)!;
    try
    {
        object result = method.Invoke(null, inputs.Cast<object>().ToArray())!;
        cases.Add(new { name, row = method.MetadataToken & 0xffffff, inputs, expected = result.ToString(), exception = (string?)null });
    }
    catch (TargetInvocationException error) when (error.InnerException != null)
    {
        Exception inner = error.InnerException;
        cases.Add(new { name, row = method.MetadataToken & 0xffffff, inputs, expected = (string?)null,
            exception = inner.GetType().FullName, message = inner.Message, fileName = (inner as FileNotFoundException)?.FileName });
    }
}

Run("Untouched");
Run("ReferenceFieldOnly");
Run("ReferenceLocalOnly");
for (int selection = 0; selection <= 5; selection++) Run("Caught", selection);
Run("CaughtAsException");
Run("InsideHandlerSkipped");
Run("Repeated");
Run("Partial", 1);

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
