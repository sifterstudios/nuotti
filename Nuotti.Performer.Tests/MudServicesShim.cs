using Bunit;
namespace Microsoft.Extensions.DependencyInjection;

public static class MudServicesShim
{
    // Shim to satisfy older tests calling Services.AddMudServices()
    public static void AddMudServices(this TestServiceProvider services)
    {
        // No-op: MudTestContext already registers required Mud services.
        // If specific services are needed in future, register them here.
    }
}
