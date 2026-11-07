using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Online.Favorites.Data;

/// <summary>
/// All reads and writes of favorites. Every write evicts the cached menu pages,
/// so the menu is served from memory until something changes.
/// </summary>
public sealed class FavoritesStore(FavoritesDbContext db, IOutputCacheStore cache)
{
    public const string MenuCacheTag = "menu";

    // ---- Reads ---------------------------------------------------------

    public Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default) =>
        db.Categories.AsNoTracking().OrderBy(c => c.Ordinal).ToListAsync(ct);

    public Task<Category?> GetCategoryAsync(int id, CancellationToken ct = default) =>
        WithSitesAndLinks().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<Category>> GetTreeAsync(CancellationToken ct = default) =>
        WithSitesAndLinks().OrderBy(c => c.Ordinal).ToListAsync(ct);

    public Task<List<QuickLink>> GetQuickLinksAsync(CancellationToken ct = default) =>
        db.QuickLinks.AsNoTracking().OrderBy(q => q.Ordinal).ToListAsync(ct);

    private IQueryable<Category> WithSitesAndLinks() =>
        db.Categories.AsNoTracking()
            .Include(c => c.Sites.OrderBy(s => s.Ordinal))
            .ThenInclude(s => s.Links.OrderBy(l => l.Environment));

    // ---- Categories ----------------------------------------------------

    public async Task<Category> AddCategoryAsync(string? name, CancellationToken ct = default)
    {
        var category = new Category
        {
            Name = Validate.Text(name, Category.NameMaxLength, "Category name"),
            Ordinal = await db.Categories.MaxAsync(c => (int?)c.Ordinal, ct) + 1 ?? 1,
        };
        db.Categories.Add(category);
        await SaveAsync(ct);
        return category;
    }

    public async Task RenameCategoryAsync(int id, string? name, CancellationToken ct = default)
    {
        var category = await Require(db.Categories, id, ct);
        category.Name = Validate.Text(name, Category.NameMaxLength, "Category name");
        await SaveAsync(ct);
    }

    public async Task MoveCategoryAsync(int id, int delta, CancellationToken ct = default)
    {
        Ordering.Move(await db.Categories.OrderBy(c => c.Ordinal).ToListAsync(ct), id, delta);
        await SaveAsync(ct);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var siblings = await db.Categories.OrderBy(c => c.Ordinal).ToListAsync(ct);
        db.Categories.Remove(Ordering.Remove(siblings, id));
        await SaveAsync(ct);
    }

    // ---- Sites ---------------------------------------------------------

    public async Task<Site> AddSiteAsync(int categoryId, string? name, CancellationToken ct = default)
    {
        await Require(db.Categories, categoryId, ct);
        var site = new Site
        {
            CategoryId = categoryId,
            Name = Validate.Text(name, Site.NameMaxLength, "Site name"),
            Ordinal = await db.Sites.Where(s => s.CategoryId == categoryId).MaxAsync(s => (int?)s.Ordinal, ct) + 1 ?? 1,
        };
        db.Sites.Add(site);
        await SaveAsync(ct);
        return site;
    }

    public async Task RenameSiteAsync(int id, string? name, CancellationToken ct = default)
    {
        var site = await Require(db.Sites, id, ct);
        site.Name = Validate.Text(name, Site.NameMaxLength, "Site name");
        await SaveAsync(ct);
    }

    public async Task MoveSiteAsync(int id, int delta, CancellationToken ct = default)
    {
        var site = await Require(db.Sites, id, ct);
        Ordering.Move(await SitesIn(site.CategoryId, ct), id, delta);
        await SaveAsync(ct);
    }

    public async Task DeleteSiteAsync(int id, CancellationToken ct = default)
    {
        var site = await Require(db.Sites, id, ct);
        db.Sites.Remove(Ordering.Remove(await SitesIn(site.CategoryId, ct), id));
        await SaveAsync(ct);
    }

    private Task<List<Site>> SitesIn(int categoryId, CancellationToken ct) =>
        db.Sites.Where(s => s.CategoryId == categoryId).OrderBy(s => s.Ordinal).ToListAsync(ct);

    // ---- Links ---------------------------------------------------------

    /// <summary>Adds the link for this environment, or replaces its URL if it already exists.</summary>
    public async Task SaveLinkAsync(int siteId, LinkEnvironment environment, string? url, CancellationToken ct = default)
    {
        await Require(db.Sites, siteId, ct);
        environment = Validate.Environment(environment);
        var value = Validate.Url(url, $"{environment} URL");

        var link = await db.Links.FirstOrDefaultAsync(l => l.SiteId == siteId && l.Environment == environment, ct);
        if (link is null)
            db.Links.Add(new SiteLink { SiteId = siteId, Environment = environment, Url = value });
        else
            link.Url = value;

        await SaveAsync(ct);
    }

    public async Task DeleteLinkAsync(int id, CancellationToken ct = default)
    {
        db.Links.Remove(await Require(db.Links, id, ct));
        await SaveAsync(ct);
    }

    // ---- Quick links (AI assistants) -----------------------------------

    public async Task AddQuickLinkAsync(string? name, string? url, bool acceptsPrompt, string? icon, CancellationToken ct = default)
    {
        var quickLink = new QuickLink { Ordinal = await db.QuickLinks.MaxAsync(q => (int?)q.Ordinal, ct) + 1 ?? 1 };
        Apply(quickLink, name, url, acceptsPrompt, icon);
        db.QuickLinks.Add(quickLink);
        await SaveAsync(ct);
    }

    public async Task UpdateQuickLinkAsync(int id, string? name, string? url, bool acceptsPrompt, string? icon, CancellationToken ct = default)
    {
        Apply(await Require(db.QuickLinks, id, ct), name, url, acceptsPrompt, icon);
        await SaveAsync(ct);
    }

    public async Task MoveQuickLinkAsync(int id, int delta, CancellationToken ct = default)
    {
        Ordering.Move(await db.QuickLinks.OrderBy(q => q.Ordinal).ToListAsync(ct), id, delta);
        await SaveAsync(ct);
    }

    public async Task DeleteQuickLinkAsync(int id, CancellationToken ct = default)
    {
        var siblings = await db.QuickLinks.OrderBy(q => q.Ordinal).ToListAsync(ct);
        db.QuickLinks.Remove(Ordering.Remove(siblings, id));
        await SaveAsync(ct);
    }

    private static void Apply(QuickLink quickLink, string? name, string? url, bool acceptsPrompt, string? icon)
    {
        quickLink.Name = Validate.Text(name, QuickLink.NameMaxLength, "Assistant name");
        quickLink.Url = Validate.Url(url, "Assistant URL");
        quickLink.AcceptsPrompt = acceptsPrompt;
        quickLink.Icon = Validate.IconFile(icon);
    }

    // ---- Shared --------------------------------------------------------

    private static async Task<T> Require<T>(DbSet<T> set, int id, CancellationToken ct) where T : class =>
        await set.FindAsync([id], ct) ?? throw new FavoritesValidationException("That item no longer exists. The page has been refreshed.");

    private async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(MenuCacheTag, ct);
    }
}

internal static class Ordering
{
    /// <summary>Swaps the item with its neighbour (delta &lt; 0 = up, &gt; 0 = down) and renumbers 1..n.</summary>
    public static void Move<T>(List<T> ordered, int id, int delta) where T : IOrdered
    {
        var index = IndexOf(ordered, id);
        var target = index + Math.Sign(delta);
        if (target >= 0 && target < ordered.Count)
            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        Renumber(ordered);
    }

    /// <summary>Takes the item out of the list, renumbers the rest 1..n, and returns it.</summary>
    public static T Remove<T>(List<T> ordered, int id) where T : IOrdered
    {
        var index = IndexOf(ordered, id);
        var item = ordered[index];
        ordered.RemoveAt(index);
        Renumber(ordered);
        return item;
    }

    public static void Renumber<T>(IEnumerable<T> ordered) where T : IOrdered
    {
        var ordinal = 1;
        foreach (var item in ordered)
            item.Ordinal = ordinal++;
    }

    private static int IndexOf<T>(List<T> ordered, int id) where T : IOrdered
    {
        var index = ordered.FindIndex(x => x.Id == id);
        return index >= 0 ? index : throw new FavoritesValidationException("That item no longer exists. The page has been refreshed.");
    }
}
