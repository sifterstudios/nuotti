using Microsoft.JSInterop;
namespace Nuotti.Performer.Services;

public interface ITourService
{
    ValueTask<bool> GetSeenAsync(CancellationToken ct = default);
    ValueTask SetSeenAsync(bool seen, CancellationToken ct = default);
}

public class TourService : ITourService
{
    private readonly IJSRuntime _js;
    private const string Key = "performer.tour.seen";

    public TourService(IJSRuntime js)
    {
        _js = js;
    }

    public async ValueTask<bool> GetSeenAsync(CancellationToken ct = default)
    {
        try
        {
            var value = await _js.InvokeAsync<string?>("localStorage.getItem", ct, Key);
            if (string.IsNullOrWhiteSpace(value)) return false;
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask SetSeenAsync(bool seen, CancellationToken ct = default)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", ct, Key, seen ? "true" : "false");
        }
        catch
        {
            // ignore
        }
    }
}
