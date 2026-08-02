using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using System.Text.Json;
namespace Nuotti.Performer;

public enum UiState
{
    Select,
    Control
}

public sealed class SessionSelectionService
{
    readonly string _settingsPath;

    public UiState State { get; private set; } = UiState.Select;
    public string? LastSessionCode { get; private set; }

    public SessionSelectionService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Nuotti");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "performer.json");
        LoadLastSession();
        // If we have a last session remembered, keep UI in Select state but prefill
    }

    public void SelectExistingSession(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        LastSessionCode = code.Trim();
        SaveLastSession(LastSessionCode);
        State = UiState.Control;
    }

    void LoadLastSession()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("lastSession", out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    LastSessionCode = prop.GetString();
                }
            }
        }
        catch { /* ignore */ }
    }

    void SaveLastSession(string session)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { lastSession = session });
            File.WriteAllText(_settingsPath, json);
        }
        catch { /* ignore */ }
    }

    public static string GenerateSessionCode(int length = 6)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // avoid ambiguous
        var rng = Random.Shared;
        Span<char> buffer = stackalloc char[length];
        for (int i = 0; i < length; i++) buffer[i] = chars[rng.Next(chars.Length)];
        return new string(buffer);
    }

    public Task<string> CreateNewSessionAsync(Uri backendBaseUri, string? preferredCode = null, CancellationToken ct = default)
    {
        using var http = new HttpClient { BaseAddress = backendBaseUri };
        return CreateNewSessionAsync(http, preferredCode, ct);
    }

    /// <param name="workspaceId">
    /// The workspace to create the session in. When supplied, the workspace-scoped route is used -
    /// it is the only one a deployed backend exposes, and it is also what binds the session code to
    /// the workspace so the audience can join it later.
    /// </param>
    /// <param name="sessionToken">The signed-in member's token, required by that route.</param>
    public async Task<string> CreateNewSessionAsync(HttpClient http, string? preferredCode = null,
        CancellationToken ct = default, string? workspaceId = null, string? sessionToken = null)
    {
        var code = string.IsNullOrWhiteSpace(preferredCode) ? GenerateSessionCode() : preferredCode!.Trim();

        if (!string.IsNullOrWhiteSpace(workspaceId) && !string.IsNullOrWhiteSpace(sessionToken))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/sessions/{Uri.EscapeDataString(code)}/create");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);
            (await http.SendAsync(request, ct)).EnsureSuccessStatusCode();
            LastSessionCode = code;
            SaveLastSession(code);
            State = UiState.Control;
            return code;
        }

        // Build command payload
        var cmd = new CreateSession(code)
        {
            SessionCode = code,
            IssuedByRole = Role.Performer,
            IssuedById = "performer-ui",
        };
        var uri = $"/v1/message/phase/create-session/{Uri.EscapeDataString(code)}";
        var resp = await http.PostAsJsonAsync(uri, cmd, ct);
        resp.EnsureSuccessStatusCode();

        LastSessionCode = code;
        SaveLastSession(code);
        State = UiState.Control; // switch to control view
        return code;
    }

    public void GoToSelect()
    {
        State = UiState.Select;
    }
}
