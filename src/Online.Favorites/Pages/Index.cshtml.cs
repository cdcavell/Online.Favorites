using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Online.Favorites.Data;
using Online.Favorites.Infrastructure;

namespace Online.Favorites.Pages;

/// <summary>The menu. Rendered once per category, then served from the output cache.</summary>
[OutputCache(PolicyName = MenuCache.PolicyName)]
public sealed class IndexModel(FavoritesStore store) : ShellPageModel
{
    public Category? Category { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        await LoadShellAsync(store, ct);

        var categoryId = id ?? Categories.FirstOrDefault()?.Id;
        if (categoryId is int selected)
        {
            Category = await store.GetCategoryAsync(selected, ct);
            if (Category is null)
                return NotFound();
        }

        ActiveCategoryId = Category?.Id;
        Title = Category?.Name ?? Title;
        return Page();
    }
}
