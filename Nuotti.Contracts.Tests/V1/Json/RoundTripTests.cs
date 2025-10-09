using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Collections.Frozen;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Json;

public class RoundTripTests
{
    public static IEnumerable<object[]> Cases()
    {
        var fixedNow = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var guid = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

        // Helper builders
        SongId SongId(string id) => new SongId(id);
        SongRef SongRef(string id, string name, string? artist = "artist") => new SongRef(SongId(id), name, artist!);
        Hint Hint(int idx, string? text = null) => new Hint(idx, text, text ?? $"Hint {idx}", SongId($"song-{idx}"));

        // State
        yield return Case(
            new GameStateSnapshot(
                sessionCode: "SESSION",
                phase: Phase.Play,
                songIndex: 2,
                currentSong: SongRef("s-1", "Song One", "Artist"),
                choices: ["A", "B", "C"],
                hintIndex: 1,
                tallies: [1, 2, 3],
                scores: new Dictionary<string, int> { ["u1"] = 10, ["u2"] = 7 }.ToFrozenDictionary(),
                songStartedAtUtc: fixedNow
            )
        );

        // Commands (Phase/*)
        yield return Case(new CreateSession("SESSION-1")
        {
            CommandId = guid,
            SessionCode = "SESSION-1",
            IssuedByRole = Role.Performer,
            IssuedById = "performer-1",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new StartGame
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new PlaySong(SongId("song-42"))
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new GiveHint(Hint(1, "Some hint"))
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new SubmitAnswer(SongId("song-42"))
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new LockAnswers
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new RevealAnswer(SongRef("song-42", "Song", "Artist"),0)
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new EndSong(SongId("song-42"))
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        yield return Case(new NextRound(SongId("song-43"))
        {
            CommandId = guid,
            SessionCode = "SESS-42",
            IssuedByRole = Role.Performer,
            IssuedById = "user-abc",
            IssuedAtUtc = fixedNow
        });

        // Events
        yield return Case(new AnswerSubmitted("aud-1", 2)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 2,
            CorrelationId = guid,
            CausedByCommandId = guid,
            SessionCode = "SESS-42",
            EmittedAtUtc = fixedNow
        });
        yield return Case(new GamePhaseChanged(Phase.Guessing, Phase.Play)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Play,
            CorrelationId = guid,
            CausedByCommandId = guid,
            SessionCode = "SESS-42",
            EmittedAtUtc = fixedNow
        });
    }

    static object[] Case<T>(T instance) => [typeof(T), instance!];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Serialize_deserialize_serialize_roundtrip_is_binary_equal(Type type, object instance)
    {
        var options = ContractsJson.DefaultOptions;

        var json1 = JsonSerializer.Serialize(instance, type, options);
        var back = JsonSerializer.Deserialize(json1, type, options);
        var json2 = JsonSerializer.Serialize(back, type, options);

        Assert.Equal(json1, json2);
    }
}
