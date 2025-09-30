namespace Nuotti.Contracts.Tests;

public static class VerifyDefaults
{
    public static VerifySettings Settings()
    {
        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        return settings;
    }
}