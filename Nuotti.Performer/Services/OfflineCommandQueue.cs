using MudBlazor;
using Nuotti.Contracts.V1.Message;
using System.Collections.Concurrent;
using System.Net;
namespace Nuotti.Performer.Services;

public sealed class OfflineCommandQueue
{
    private const int MaxQueue = 10;
    private readonly ConcurrentQueue<(string route, CommandBase cmd)> _queue = new();
    private readonly object _gate = new();

    public event Action? Changed;

    public bool IsOffline { get; private set; }

    public int Count => _queue.Count;

    public void SetOffline(bool offline)
    {
        lock (_gate)
        {
            IsOffline = offline;
        }
        Changed?.Invoke();
    }

    public void Enqueue(string route, CommandBase cmd)
    {
        lock (_gate)
        {
            // enforce max 10
            while (_queue.Count >= MaxQueue && _queue.TryDequeue(out _)) { }
            _queue.Enqueue((route, cmd));
        }
        Changed?.Invoke();
    }

    public async Task<bool> TrySendOrQueueAsync(Func<Task<HttpResponseMessage>> send, string route, CommandBase cmd, ISnackbar snackbar, CommandHistoryService history, CancellationToken ct)
    {
        try
        {
            var resp = await send();
            var ok = await HandleResponseAsync(resp, route, cmd, snackbar, history, ct);
            if (ok)
            {
                // Successful round-trip implies connectivity
                SetOffline(false);
            }
            return ok;
        }
        catch (HttpRequestException)
        {
            // network drop: go offline and queue
            SetOffline(true);
            Enqueue(route, cmd);
            snackbar.Add("Offline – commands queued", Severity.Warning);
            return true;
        }
        catch (TaskCanceledException)
        {
            // timeout treated as offline transient
            SetOffline(true);
            Enqueue(route, cmd);
            snackbar.Add("Offline – commands queued", Severity.Warning);
            return true;
        }
    }

    public async Task FlushAsync(Func<string, CommandBase, Task<HttpResponseMessage>> sender, ISnackbar? snackbar, CommandHistoryService history, CancellationToken ct = default)
    {
        // Try send in FIFO order
        var items = new List<(string route, CommandBase cmd)>();
        while (_queue.TryDequeue(out var item)) items.Add(item);
        if (items.Count == 0) return;

        foreach (var (route, cmd) in items)
        {
            try
            {
                var resp = await sender(route, cmd);
                var ok = await HandleResponseAsync(resp, route, cmd, snackbar, history, ct);
                if (!ok)
                {
                    // if backend rejects for non-idempotency reasons, stop draining further to avoid cascade
                    // push remaining back to the front preserving order
                    foreach (var rest in items.Skip(items.IndexOf((route, cmd)) + 1))
                    {
                        _queue.Enqueue(rest);
                    }
                    break;
                }
            }
            catch (Exception)
            {
                // network issue again; re-enqueue current and the rest and break
                _queue.Enqueue((route, cmd));
                foreach (var rest in items.Skip(items.IndexOf((route, cmd)) + 1))
                {
                    _queue.Enqueue(rest);
                }
                SetOffline(true);
                break;
            }
        }
        Changed?.Invoke();
    }

    private static async Task<bool> HandleResponseAsync(HttpResponseMessage resp, string route, CommandBase cmd, ISnackbar? snackbar, CommandHistoryService history, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            history.RecordSuccess(cmd);
            if (resp.StatusCode == HttpStatusCode.Accepted)
            {
                snackbar?.Add("Accepted", Severity.Success);
            }
            return true;
        }
        else
        {
            history.RecordFailure(cmd, null);
            snackbar.Add($"Command failed: {(int)resp.StatusCode}", Severity.Error);
            return false;
        }
    }
}