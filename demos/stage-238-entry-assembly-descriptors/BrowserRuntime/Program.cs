using System;

Console.WriteLine("STANDARDWASM_ENTRY_ORACLE|" + ProgramProbe.DefaultEntry() + "|" +
    ProgramProbe.OverrideAndRestore() + "|" + ProgramProbe.ExplicitNull());
