using System.Runtime.CompilerServices;

namespace Nuotti.Contracts.Tests;

public static class VerifyInit
{
    [ModuleInitializer]
    public static void Init()
    {
        // Never launch the OS diff tool on a mismatch; the test output shows the difference.
        DiffEngine.DiffRunner.Disabled = true;

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            VerifierSettings.AutoVerify();
        }
    }
}