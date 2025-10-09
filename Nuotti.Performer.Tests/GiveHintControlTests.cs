using Xunit;
namespace Nuotti.Performer.Tests;

public class PerformerUiStateHintIndexTests
{
    sealed class DummyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    [Fact]
    public void NextHintIndex_Increments_From_State()
    {
        var state = new PerformerUiState(new DummyFactory());
        // initial
        Assert.Equal(1, state.NextHintIndex);
        // increment
        state.IncrementHintIndex();
        Assert.Equal(1, state.HintIndex);
        Assert.Equal(2, state.NextHintIndex);
    }
}
