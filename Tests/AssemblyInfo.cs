using Microsoft.VisualStudio.TestTools.UnitTesting;

// Assembly-level attributes only configure the assembly that declares them, so this belongs
// here rather than in the framework: declared in HardPoint.Testing.dll it would have configured the
// framework assembly and left this test assembly running serially.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
