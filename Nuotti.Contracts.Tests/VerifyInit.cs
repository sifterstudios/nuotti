using System.Runtime.CompilerServices;

namespace Nuotti.Contracts.Tests;

public static class VerifyInit
{
    [ModuleInitializer]
    public static void Init()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            VerifierSettings.AutoVerify();
        }
    }
}