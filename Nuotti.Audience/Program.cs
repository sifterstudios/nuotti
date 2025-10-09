using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Nuotti.Audience;
using Nuotti.Audience.Services;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddMudServices();

// AudienceHubClient depends on HttpClient (scoped), so it must be scoped too
builder.Services.AddScoped<AudienceHubClient>();

// Theme service for light/dark mode
builder.Services.AddScoped<ThemeService>();

await builder.Build().RunAsync();