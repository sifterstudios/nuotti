using MudBlazor.Services;
using Nuotti.Performer;
using Nuotti.Performer.Endpoints;
using Nuotti.Performer.Services;
using Serilog;
using ServiceDefaults;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddMudServices();
builder.AddNuottiWebHost();

builder.Services.AddHttpClient();
builder.Services.AddScoped<SessionSelectionService>();
builder.Services.AddSingleton<PerformerUiState>();
builder.Services.AddScoped<PerformerCommands>();
builder.Services.AddSingleton<IManifestService, ManifestService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddSingleton<CommandHistoryService>();
builder.Services.AddSingleton<KeyboardShortcutsService>();
builder.Services.AddSingleton<OfflineCommandQueue>();
builder.Services.AddScoped<CommandPaletteService>();
builder.Services.AddSingleton<IEnvironmentService, EnvironmentService>();
builder.Services.AddScoped<ITourService, TourService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapAboutEndpoints();
app.MapNuottiEndpoints("Nuotti.Performer");

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
