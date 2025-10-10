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

    public void Open()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, FullWidth = true, MaxWidth = MaxWidth.Small, NoHeader = true };
        _dialogs.Show<CommandPalette>(string.Empty, options);
    }
}