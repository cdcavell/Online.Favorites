using Microsoft.AspNetCore.Mvc.RazorPages;
using Online.Favorites.Data;

namespace Online.Favorites.Pages;

/// <summary>What _Layout needs on every page: the category list and the AI assistant links.</summary>
public abstract class ShellPageModel : PageModel
{
    public IReadOnlyList<Category> Categories { get; protected set; } = [];
    public IReadOnlyList<QuickLink> QuickLinks { get; protected set; } = [];
    public int? ActiveCategoryId { get; protected set; }
    public string Title { get; protected set; } = "Online Favorites";
    public bool IsConfigure { get; protected set; }

    protected async Task LoadShellAsync(FavoritesStore store, CancellationToken ct)
    {
        Categories = await store.GetCategoriesAsync(ct);
        QuickLinks = await store.GetQuickLinksAsync(ct);
    }
}
