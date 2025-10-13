using MudBlazor;
using Nuotti.Performer.Shared;
namespace Nuotti.Performer.Services;

public class CommandPaletteService
{
    private readonly IDialogService _dialogs;
    public CommandPaletteService(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public event Func<string, Task>? ExecuteRequested;

    public Task RequestExecuteAsync(string key)
        => ExecuteRequested?.Invoke(key) ?? Task.CompletedTask;

    public void Open()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, FullWidth = true, MaxWidth = MaxWidth.Small, NoHeader = true };
        _dialogs.Show<CommandPalette>(string.Empty, options);
    }
}