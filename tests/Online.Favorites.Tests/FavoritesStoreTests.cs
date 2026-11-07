using Microsoft.EntityFrameworkCore;
using Online.Favorites.Data;

namespace Online.Favorites.Tests;

public sealed class FavoritesStoreTests
{
    [Fact]
    public async Task Added_categories_are_numbered_in_order()
    {
        using var t = new TestDatabase();
        await t.Store.AddCategoryAsync("News");
        await t.Store.AddCategoryAsync(" Billing ");

        var names = (await t.Store.GetCategoriesAsync()).Select(c => (c.Ordinal, c.Name));
        Assert.Equal([(1, "News"), (2, "Billing")], names);
    }

    [Fact]
    public async Task Moving_swaps_with_the_neighbour_and_stops_at_the_ends()
    {
        using var t = new TestDatabase();
        var a = await t.Store.AddCategoryAsync("A");
        await t.Store.AddCategoryAsync("B");
        var c = await t.Store.AddCategoryAsync("C");

        await t.Store.MoveCategoryAsync(c.Id, -1);
        await t.Store.MoveCategoryAsync(a.Id, -1); // already first: no change

        Assert.Equal(["A", "C", "B"], (await t.Store.GetCategoriesAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Deleting_a_site_renumbers_only_its_own_category()
    {
        // The legacy app filtered on the wrong column here and never renumbered.
        using var t = new TestDatabase();
        var news = await t.Store.AddCategoryAsync("News");
        var other = await t.Store.AddCategoryAsync("Other");
        var first = await t.Store.AddSiteAsync(news.Id, "One");
        await t.Store.AddSiteAsync(news.Id, "Two");
        await t.Store.AddSiteAsync(news.Id, "Three");
        await t.Store.AddSiteAsync(other.Id, "Elsewhere");

        await t.Store.DeleteSiteAsync(first.Id);

        using var read = t.NewContext();
        var newsSites = await read.Sites.Where(s => s.CategoryId == news.Id).OrderBy(s => s.Ordinal).Select(s => new { s.Ordinal, s.Name }).ToListAsync();
        Assert.Equal([new { Ordinal = 1, Name = "Two" }, new { Ordinal = 2, Name = "Three" }], newsSites);
        Assert.Equal(1, (await read.Sites.SingleAsync(s => s.CategoryId == other.Id)).Ordinal);
    }

    [Fact]
    public async Task Deleting_a_category_removes_its_sites_and_links()
    {
        using var t = new TestDatabase();
        var news = await t.Store.AddCategoryAsync("News");
        var site = await t.Store.AddSiteAsync(news.Id, "WBRZ-TV");
        await t.Store.SaveLinkAsync(site.Id, LinkEnvironment.Public, "https://www.wbrz.com/");

        await t.Store.DeleteCategoryAsync(news.Id);

        using var read = t.NewContext();
        Assert.Empty(await read.Sites.ToListAsync());
        Assert.Empty(await read.Links.ToListAsync());
    }

    [Fact]
    public async Task Saving_a_link_twice_for_one_environment_updates_it()
    {
        using var t = new TestDatabase();
        var site = await t.Store.AddSiteAsync((await t.Store.AddCategoryAsync("News")).Id, "WBRZ-TV");

        await t.Store.SaveLinkAsync(site.Id, LinkEnvironment.Public, "https://old.example/");
        await t.Store.SaveLinkAsync(site.Id, LinkEnvironment.Public, "https://new.example/");

        using var read = t.NewContext();
        Assert.Equal("https://new.example/", (await read.Links.SingleAsync()).Url);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task Unsafe_or_invalid_urls_are_rejected(string url)
    {
        using var t = new TestDatabase();
        var site = await t.Store.AddSiteAsync((await t.Store.AddCategoryAsync("News")).Id, "Site");

        await Assert.ThrowsAsync<FavoritesValidationException>(() => t.Store.SaveLinkAsync(site.Id, LinkEnvironment.Public, url));
    }

    [Fact]
    public async Task Every_change_evicts_the_cached_menu()
    {
        using var t = new TestDatabase();
        var category = await t.Store.AddCategoryAsync("News");
        await t.Store.RenameCategoryAsync(category.Id, "Headlines");

        Assert.Equal([FavoritesStore.MenuCacheTag, FavoritesStore.MenuCacheTag], t.Cache.EvictedTags);
    }

    [Fact]
    public async Task Ai_assistants_are_seeded()
    {
        using var t = new TestDatabase();
        var names = (await t.Store.GetQuickLinksAsync()).Select(q => q.Name);
        Assert.Equal(["ChatGPT", "Claude", "Gemini", "Copilot", "Perplexity", "Grok"], names);
    }

    [Theory]
    [InlineData("../secret.svg")]
    [InlineData("icon.png")]
    [InlineData("Claude.svg")]
    public async Task Assistant_icons_must_be_plain_svg_file_names(string icon)
    {
        using var t = new TestDatabase();
        await Assert.ThrowsAsync<FavoritesValidationException>(() =>
            t.Store.AddQuickLinkAsync("Test", "https://example.com/", false, icon));
    }
}
