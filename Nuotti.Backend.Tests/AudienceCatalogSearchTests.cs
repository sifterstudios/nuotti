using Nuotti.Backend.Assets;
using Nuotti.Backend.Catalog;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Tests;

public sealed class AudienceCatalogSearchTests
{
    [Fact]
    public async Task Search_combines_shared_and_workspace_catalog_and_isolates_tenants()
    {
        var shared = new InMemorySharedSongCatalog();
        shared.Seed([
            new SongRef(new SongId("shared-1"), "Shared Alpha", "Artist A"),
            new SongRef(new SongId("shared-2"), "Shared Beta", "Artist B")
        ]);
        var privateStore = new InMemoryPrivateAssetMetadataStore();
        await privateStore.CreateEntryAsync("ws-a", "Private Alpha", "Band A", "user-1");
        await privateStore.CreateEntryAsync("ws-b", "Secret Beta", "Band B", "user-2");
        var binder = new InMemorySessionWorkspaceBinder();
        binder.Bind("SESS-A", "ws-a");
        binder.Bind("SESS-B", "ws-b");
        var search = new AudienceCatalogSearch(shared, privateStore, binder);

        var resultsA = await search.SearchAsync("SESS-A", "Alpha", limit: 50);
        Assert.Contains(resultsA, r => r.Title == "Shared Alpha");
        Assert.Contains(resultsA, r => r.Title == "Private Alpha");
        Assert.DoesNotContain(resultsA, r => r.Title == "Secret Beta");

        var resultsB = await search.SearchAsync("SESS-B", "Beta", limit: 50);
        Assert.Contains(resultsB, r => r.Title == "Shared Beta");
        Assert.Contains(resultsB, r => r.Title == "Secret Beta");
        Assert.DoesNotContain(resultsB, r => r.Title == "Private Alpha");
    }

    [Fact]
    public async Task Search_over_one_thousand_entries_stays_under_budget()
    {
        var shared = new InMemorySharedSongCatalog();
        shared.Seed(Enumerable.Range(0, 1200).Select(i =>
            new SongRef(new SongId($"shared-{i}"), $"Song {i:D4}", $"Artist {i % 40}")));
        var privateStore = new InMemoryPrivateAssetMetadataStore();
        var binder = new InMemorySessionWorkspaceBinder();
        binder.Bind("SESS-L", "ws-large");
        var search = new AudienceCatalogSearch(shared, privateStore, binder);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await search.SearchAsync("SESS-L", "Song 11", limit: 25);
        sw.Stop();

        Assert.NotEmpty(results);
        Assert.True(results.Count <= 25);
        Assert.True(sw.ElapsedMilliseconds < 250, $"search took {sw.ElapsedMilliseconds}ms");
    }
}
