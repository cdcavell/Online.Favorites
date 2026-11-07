using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Online.Favorites.Data;

namespace Online.Favorites.Tests;

public sealed class LegacyImporterTests : IDisposable
{
    private readonly string legacyPath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.db");

    public LegacyImporterTests()
    {
        // Same tables and column types as Customizable.Menu's Initial migration (GUIDs stored as TEXT).
        using var connection = new SqliteConnection($"Data Source={legacyPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Menu (Guid TEXT PRIMARY KEY, Ordinal INTEGER, Description TEXT);
            CREATE TABLE Site (Guid TEXT PRIMARY KEY, Ordinal INTEGER, Description TEXT, MenuGuid TEXT);
            CREATE TABLE Url (Guid TEXT PRIMARY KEY, Environment INTEGER, Link TEXT, SiteGuid TEXT);
            INSERT INTO Menu VALUES ('M2', 2, 'State / City'), ('M1', 1, 'News / Weather');
            INSERT INTO Site VALUES ('S2', 5, 'WAFB-TV', 'M1'), ('S1', 2, 'WBRZ-TV', 'M1'), ('S9', 1, 'Orphan', 'GONE');
            INSERT INTO Url VALUES
                ('U1', 3, 'https://www.wbrz.com/', 'S1'),
                ('U2', 0, 'http://localhost:5000/', 'S1'),
                ('U3', 3, 'https://www.wafb.com/', 'S2'),
                ('U4', 3, 'javascript:alert(1)', 'S2');
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task Imports_menus_sites_and_links_in_order()
    {
        using var t = new TestDatabase();

        var result = await LegacyImporter.ImportAsync(legacyPath, t.Db);

        Assert.Equal((2, 2, 3), (result.Categories, result.Sites, result.Links));
        Assert.Equal(2, result.Skipped.Count); // the orphan site and the javascript: link

        var tree = await t.Store.GetTreeAsync();
        Assert.Equal(["News / Weather", "State / City"], tree.Select(c => c.Name));
        Assert.Equal(["WBRZ-TV", "WAFB-TV"], tree[0].Sites.Select(s => s.Name));
        Assert.Equal([1, 2], tree[0].Sites.Select(s => s.Ordinal)); // gaps in legacy ordinals are closed
        Assert.Equal([LinkEnvironment.Development, LinkEnvironment.Public], tree[0].Sites[0].Links.Select(l => l.Environment));
    }

    [Fact]
    public async Task Refuses_to_import_into_a_database_that_has_data()
    {
        using var t = new TestDatabase();
        await t.Store.AddCategoryAsync("Existing");

        await Assert.ThrowsAsync<FavoritesValidationException>(() => LegacyImporter.ImportAsync(legacyPath, t.Db));
        Assert.Equal(1, await t.Db.Categories.CountAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(legacyPath);
    }
}
