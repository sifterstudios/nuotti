using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Performer.Shared;
using Xunit;
namespace Nuotti.Performer.Tests;

public class ProjectorPreviewTests : MudTestContext
{
    sealed class FactoryFromHandler : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FactoryFromHandler(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
    }

    [Fact]
    public void Preview_matches_phase_question_and_tallies_when_visible()
    {
        var handler = new HttpClientHandler();
        Services.AddSingleton<IHttpClientFactory>(new FactoryFromHandler(handler));
        var state = new PerformerUiState(new FactoryFromHandler(handler));
        Services.AddSingleton(state);

        // Arrange snapshot with choices and tallies
        var snap = new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Guessing,
            songIndex: 2,
            currentSong: new SongRef(new SongId("s-1"), "SongTitle", "SongArtist"),
            catalog: Array.Empty<SongRef>(),
            choices: new[] { "Red", "Blue", "Green" },
            hintIndex: 1,
            tallies: new[] { 5, 3, 1 },
            scores: new Dictionary<string, int>(),
            songStartedAtUtc: null
        );
        state.UpdateGameState(snap);

        // Act
        var cut = RenderComponent<ProjectorPreview>(ps => ps.Add(p => p.StartVisible, true));

        // Assert Phase
        var phaseEl = cut.Find("[data-test='phase-text']");
        Assert.Contains("Guessing", phaseEl.TextContent);

        // Assert Question line (Title — Artist)
        var q = cut.Find("[data-test='question-text']");
        Assert.Equal("SongTitle — SongArtist", q.TextContent);

        // Assert tallies
        for (int i = 0; i < 3; i++)
        {
            var choice = cut.Find($"[data-test='choice-{i}']");
            var tally = cut.Find($"[data-test='tally-{i}']");
            Assert.Equal(snap.Choices[i], choice.TextContent);
            Assert.Equal(snap.Tallies[i].ToString(), tally.TextContent);
        }
    }

    [Fact]
    public void Hiding_preview_stops_re_render_churn()
    {
        var handler = new HttpClientHandler();
        Services.AddSingleton<IHttpClientFactory>(new FactoryFromHandler(handler));
        var state = new PerformerUiState(new FactoryFromHandler(handler));
        Services.AddSingleton(state);

        // Starts hidden by default
        var cut = RenderComponent<ProjectorPreview>();
        Assert.NotNull(cut.Find("[data-test='preview-hidden']"));
        var renderCountBefore = cut.RenderCount;

        // Push many updates; component should not re-render while hidden
        for (int i = 0; i < 5; i++)
        {
            state.UpdateGameState(new GameStateSnapshot(
                sessionCode: "dev",
                phase: Phase.Start,
                songIndex: i,
                currentSong: null,
                catalog: Array.Empty<SongRef>(),
                choices: Array.Empty<string>(),
                hintIndex: 0,
                tallies: Array.Empty<int>(),
                scores: new Dictionary<string, int>(),
                songStartedAtUtc: null
            ));
        }

        Assert.Equal(renderCountBefore, cut.RenderCount);
    }
}
