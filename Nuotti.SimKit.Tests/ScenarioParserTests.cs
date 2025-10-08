using Nuotti.SimKit.Script;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class ScenarioParserTests
{
    [Fact]
    public void ParseJson_ValidScenario_Succeeds()
    {
        var json = """
{
  "audience": { "demographic": "Adults", "expectedSize": 150, "energy": "Medium" },
  "sessions": [ { "id": "s1", "name": "Evening", "playlist": [ { "songId": "song-1" } ] } ],
  "songs": [ {
      "id": "song-1",
      "title": "Track One",
      "artist": "Band A",
      "phases": [ { "name": "Intro", "durationMs": 5000 }, { "name": "Chorus", "durationMs": 10000 } ]
  } ]
}
""";
        var scenario = ScenarioParser.ParseJson(json);
        Assert.NotNull(scenario);
        Assert.Single(scenario.Sessions);
        Assert.Single(scenario.Songs);
        Assert.Equal("song-1", scenario.Songs[0].Id);
        Assert.Equal(2, scenario.Songs[0].Phases.Count);
    }

    [Fact]
    public void ParseYaml_ValidScenario_Succeeds()
    {
        var yaml = @"audience:
  demographic: Teens
  expectedSize: 80
  energy: High
sessions:
  - id: s1
    name: Matinee
    playlist:
      - songId: s1-1
songs:
  - id: s1-1
    title: Hello
    artist: World
    phases:
      - name: Intro
        durationMs: 2000
      - name: Verse
        durationMs: 4000
";
        var scenario = ScenarioParser.ParseYaml(yaml);
        Assert.NotNull(scenario);
        Assert.Single(scenario.Sessions);
        Assert.Single(scenario.Songs);
        Assert.Equal(2, scenario.Songs[0].Phases.Count);
    }

    [Fact]
    public void ParseJson_InvalidScenario_MissingSessions_RejectedWithClearError()
    {
        var json = """{ "songs": [] }"""; // sessions missing, songs empty (also invalid)
        var ex = Assert.Throws<InvalidOperationException>(() => ScenarioParser.ParseJson(json));
        Assert.Contains("Scenario validation failed", ex.Message);
        Assert.Contains("sessions", ex.Message); // ensure error mentions the missing 'sessions' property
    }

    [Fact]
    public void ParseJson_InvalidPhaseDurationType_RejectedWithClearError()
    {
        var json = """
{
  "sessions": [ { "id": "s1", "playlist": [ { "songId": "song-1" } ] } ],
  "songs": [ {
      "id": "song-1",
      "title": "Track One",
      "artist": "Band A",
      "phases": [ { "name": "Intro", "durationMs": "5s" } ]
  } ]
}
""";
        var ex = Assert.Throws<InvalidOperationException>(() => ScenarioParser.ParseJson(json));
        Assert.Contains("durationMs", ex.Message);
        Assert.Contains("integer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
