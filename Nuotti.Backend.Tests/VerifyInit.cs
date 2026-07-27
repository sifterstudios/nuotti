using System.Runtime.CompilerServices;

namespace Nuotti.Backend.Tests;

public static class VerifyInit
{
    [ModuleInitializer]
    public static void Init()
    {
        // Without this, every failing snapshot test launches the OS diff tool — a Rider window per
        // failure, per run. The test output already shows the difference.
        DiffEngine.DiffRunner.Disabled = true;
    }
}
