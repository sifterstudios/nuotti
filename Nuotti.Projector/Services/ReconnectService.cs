using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Projector.Services;

public class ReconnectService
{
    private readonly HttpClient _httpClient;
    private readonly string _backendUrl;
    
    public ReconnectService(string backendUrl)
    {
        _backendUrl = backendUrl;
        _httpClient = new HttpClient();
    }
    
    public async Task<GameStateSnapshot?> FetchLatestStateAsync(string sessionCode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_backendUrl}/status/{sessionCode}");
            
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
