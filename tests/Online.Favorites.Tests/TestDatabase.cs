using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Online.Favorites.Data;

namespace Online.Favorites.Tests;

/// <summary>A migrated in-memory SQLite database plus a store wired to a recording cache.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        connection.Open();
        Db = NewContext();
        Db.Database.Migrate();
        Store = new FavoritesStore(Db, Cache);
    }

    public FavoritesDbContext Db { get; }
    public RecordingCache Cache { get; } = new();
    public FavoritesStore Store { get; }

    /// <summary>A second context on the same database, for reading back without tracked entities.</summary>
    public FavoritesDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FavoritesDbContext>().UseSqlite(connection).Options);

    public void Dispose()
    {
        Db.Dispose();
        connection.Dispose();
    }
}

internal sealed class RecordingCache : IOutputCacheStore
{
    public List<string> EvictedTags { get; } = [];

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        EvictedTags.Add(tag);
        return ValueTask.CompletedTask;
    }

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);

    public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
