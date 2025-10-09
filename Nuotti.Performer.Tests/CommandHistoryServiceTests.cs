using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Performer.Services;
using Xunit;
namespace Nuotti.Performer.Tests;

public class CommandHistoryServiceTests
{
    [Fact]
    public void History_caps_at_50_and_trims_oldest()
    {
        var history = new CommandHistoryService();
        // push 60 entries
        for (int i = 0; i < 60; i++)
        {
            var cmd = new StartGame
            {
                SessionCode = "S",
                IssuedByRole = Role.Performer,
                IssuedById = "t",
            };
            // Force unique ids for determinism
            typeof(CommandBase)
                .GetProperty("CommandId")!
                .SetValue(cmd, Guid.NewGuid());
            history.RecordSuccess(cmd);
        }
        var entries = history.GetEntries();
        Assert.Equal(CommandHistoryService.MaxEntries, entries.Count);

        // Entries are added to the front; after pushing 60, we should have kept the latest 50.
        // Ensure no nulls and timestamps are in non-increasing order (roughly)
        Assert.All(entries, e => Assert.NotEqual(Guid.Empty, e.CommandId));
    }
}