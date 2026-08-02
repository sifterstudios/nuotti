using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Projector.Services;

/// <summary>
/// Catches the projector up on what it missed while it was disconnected.
/// </summary>
/// <remarks>
/// The status read is credentialled, so this needs the device's own lease. Without it the resync
/// after every reconnect failed and the projector sat on a stale frame until the next event
/// happened to arrive - which, between songs, can be a long time to show the wrong thing.
/// </remarks>
public class ReconnectService
{
    private readonly HttpClient _httpClient;
    private readonly string _backendUrl;
    private readonly Func<Task<string?>>? _accessToken;

    public ReconnectService(string backendUrl, Func<Task<string?>>? accessToken = null)
    {
        _backendUrl = backendUrl;
        _accessToken = accessToken;
        _httpClient = new HttpClient();
    }

    public async Task<GameStateSnapshot?> FetchLatestStateAsync(string sessionCode)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_backendUrl}/status/{sessionCode}");
            if (_accessToken is not null && await _accessToken() is { Length: > 0 } token)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var state = await response.Content.ReadFromJsonAsync<GameStateSnapshot>();
                return state;
            }
            else
            {
                Console.WriteLine($"Failed to fetch state: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching state: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
