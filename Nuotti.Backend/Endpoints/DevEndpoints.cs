using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Endpoints;

internal static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        app.MapPost("/dev/reset/{session}", (ISessionStore sess, IGameStateStore game, string session) =>
        {
            sess.Clear(session);
            game.Remove(session);
            return Results.NoContent();
        });

        app.MapPost("/dev/fake/{session}", async (IHubContext<QuizHub> hub, string session, HttpRequest request) =>
        {
            // Expect JSON body like { "type": "AnswerSubmitted", "payload": { ... } }
            using var doc = await JsonDocument.ParseAsync(request.Body);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            {
                return Results.BadRequest(new { error = "Missing or invalid 'type'" });
            }
            if (!doc.RootElement.TryGetProperty("payload", out var payloadEl))
            {
                return Results.BadRequest(new { error = "Missing 'payload'" });
            }

            var type = typeEl.GetString()!.Trim();
            object? evt = null;
            string? target = null;

            try
            {
                switch (type.ToLowerInvariant())
                {
                    case "answersubmitted":
                        {
                            // Accept case-insensitive property names for convenience in tests/tools
                            string? audienceId = null;
                            int? choiceIndex = null;
                            if (payloadEl.TryGetProperty("AudienceId", out var audEl) && audEl.ValueKind == JsonValueKind.String)
                            {
                                audienceId = audEl.GetString();
                            }
                            else if (payloadEl.TryGetProperty("audienceId", out var audEl2) && audEl2.ValueKind == JsonValueKind.String)
                            {
                                audienceId = audEl2.GetString();
                            }

                            if (payloadEl.TryGetProperty("ChoiceIndex", out var choiceEl) && choiceEl.ValueKind == JsonValueKind.Number)
                            {
                                choiceIndex = choiceEl.GetInt32();
                            }
                            else if (payloadEl.TryGetProperty("choiceIndex", out var choiceEl2) && choiceEl2.ValueKind == JsonValueKind.Number)
                            {
                                choiceIndex = choiceEl2.GetInt32();
                            }

                            if (string.IsNullOrWhiteSpace(audienceId))
                                return Results.BadRequest(new { error = "Invalid payload for AnswerSubmitted: AudienceId" });
                            if (choiceIndex is null)
                                return Results.BadRequest(new { error = "Invalid payload for AnswerSubmitted: ChoiceIndex" });
                            var corr = Guid.NewGuid();
                            var x = new AnswerSubmitted(audienceId!, choiceIndex!.Value)
                            {
                                AudienceId = audienceId!,
                                ChoiceIndex = choiceIndex!.Value,
                                SessionCode = session,
                                CorrelationId = corr,
                                CausedByCommandId = corr
                            };
                            evt = x;
                            target = "AnswerSubmitted";
                            break;
                        }
                    case "gamephasechanged":
                        {
                            // Accept either string or numeric phases
                            Phase current;
                            Phase next;
                            if (payloadEl.TryGetProperty("CurrentPhase", out var curEl))
                            {
                                current = curEl.ValueKind == JsonValueKind.String
                                    ? Enum.TryParse<Phase>(curEl.GetString(), out var cp) ? cp : default
                                    : (Phase)curEl.GetInt32();
                            }
                            else return Results.BadRequest(new { error = "Invalid payload for GamePhaseChanged: CurrentPhase" });

                            if (payloadEl.TryGetProperty("NewPhase", out var newEl))
                            {
                                next = newEl.ValueKind == JsonValueKind.String
                                    ? Enum.TryParse<Phase>(newEl.GetString(), out var np) ? np : default
                                    : (Phase)newEl.GetInt32();
                            }
                            else return Results.BadRequest(new { error = "Invalid payload for GamePhaseChanged: NewPhase" });

                            var corr = Guid.NewGuid();
                            var x = new GamePhaseChanged(current, next)
                            {
                                CurrentPhase = current,
                                NewPhase = next,
                                SessionCode = session,
                                CorrelationId = corr,
                                CausedByCommandId = corr
                            };
                            evt = x;
                            target = "GamePhaseChanged";
                            break;
                        }
                    default:
                        return Results.BadRequest(new { error = $"Unsupported type '{type}'" });
                }
            }
            catch (JsonException jex)
            {
                return Results.BadRequest(new { error = "Invalid JSON", detail = jex.Message });
            }

            await hub.Clients.Group(session).SendAsync(target!, evt!);
            return Results.Accepted();
        });
    }
}
