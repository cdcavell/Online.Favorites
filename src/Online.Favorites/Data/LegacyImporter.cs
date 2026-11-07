using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Online.Favorites.Data;

public sealed record ImportResult(int Categories, int Sites, int Links, IReadOnlyList<string> Skipped);

/// <summary>
/// Copies Menu → Site → Url rows from the old Customizable.Menu Application.db (read-only)
/// into an empty Online.Favorites database. Configuration and AuditHistory are not carried over.
/// </summary>
public static class LegacyImporter
{
    public static async Task<ImportResult> ImportAsync(string legacyPath, FavoritesDbContext db, CancellationToken ct = default)
    {
        if (!File.Exists(legacyPath))
            throw new FavoritesValidationException($"Legacy database not found: {legacyPath}");
        if (await db.Categories.AnyAsync(ct))
            throw new FavoritesValidationException("This database already has categories. Import only runs into an empty database.");

        var connectionString = new SqliteConnectionStringBuilder { DataSource = legacyPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        await using var legacy = new SqliteConnection(connectionString);
        await legacy.OpenAsync(ct);

        var skipped = new List<string>();
        var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        var sites = new Dictionary<string, Site>(StringComparer.OrdinalIgnoreCase);

        await foreach (var row in ReadAsync(legacy, "SELECT Guid, Description FROM Menu ORDER BY Ordinal", ct))
        {
            var category = new Category
            {
                Ordinal = categories.Count + 1,
                Name = Validate.Text(Truncate(row.GetString(1), Category.NameMaxLength), Category.NameMaxLength, "Category name"),
            };
            categories[row.GetString(0)] = category;
        }

        await foreach (var row in ReadAsync(legacy, "SELECT Guid, MenuGuid, Description FROM Site ORDER BY MenuGuid, Ordinal", ct))
        {
            var name = Truncate(row.GetString(2), Site.NameMaxLength);
            if (!categories.TryGetValue(row.GetString(1), out var category))
            {
                skipped.Add($"Site \"{name}\": its menu no longer exists.");
                continue;
            }

            var site = new Site { Ordinal = category.Sites.Count + 1, Name = Validate.Text(name, Site.NameMaxLength, "Site name") };
            category.Sites.Add(site);
            sites[row.GetString(0)] = site;
        }

        var links = 0;
        await foreach (var row in ReadAsync(legacy, "SELECT SiteGuid, Environment, Link FROM Url ORDER BY Environment", ct))
        {
            var url = row.GetString(2);
            if (!sites.TryGetValue(row.GetString(0), out var site))
            {
                skipped.Add($"Link {url}: its site no longer exists.");
                continue;
            }

            var environment = (LinkEnvironment)row.GetInt32(1);
            if (!Enum.IsDefined(environment) || site.Links.Any(l => l.Environment == environment))
            {
                skipped.Add($"Link {url} on \"{site.Name}\": unknown or duplicate environment.");
                continue;
            }

            try
            {
                site.Links.Add(new SiteLink { Environment = environment, Url = Validate.Url(url, "URL") });
                links++;
            }
            catch (FavoritesValidationException ex)
            {
                skipped.Add($"Link {url} on \"{site.Name}\": {ex.Message}");
            }
        }

        db.Categories.AddRange(categories.Values);
        await db.SaveChangesAsync(ct);
        return new ImportResult(categories.Count, sites.Count, links, skipped);
    }

    /// <summary>Entry point for <c>Online.Favorites.exe --import "path"</c>. Returns the process exit code.</summary>
    public static async Task<int> RunFromCommandLineAsync(IServiceProvider services, string legacyPath)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavoritesDbContext>();
        try
        {
            var result = await ImportAsync(Path.GetFullPath(legacyPath), db);
            Console.WriteLine($"Imported {result.Categories} categories, {result.Sites} sites and {result.Links} links.");
            foreach (var line in result.Skipped)
                Console.WriteLine($"  Skipped: {line}");
            return 0;
        }
        catch (FavoritesValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async IAsyncEnumerable<SqliteDataReader> ReadAsync(
        SqliteConnection connection, string sql, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            yield return reader;
    }

    private static string Truncate(string value, int maxLength)
    {
        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd();
    }
}
