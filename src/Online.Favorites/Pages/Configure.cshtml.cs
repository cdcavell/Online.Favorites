using Microsoft.AspNetCore.Mvc;
using Online.Favorites.Data;

namespace Online.Favorites.Pages;

/// <summary>
/// Edits categories, sites, links and AI assistants with plain HTML forms (no JavaScript).
/// Every handler saves, then redirects back here with the edited category/site expanded.
/// </summary>
public sealed class ConfigureModel(FavoritesStore store) : ShellPageModel
{
    public List<Category> Tree { get; private set; } = [];

    /// <summary>Category to show expanded (query/form field "c").</summary>
    [BindProperty(SupportsGet = true, Name = "c")]
    public int? OpenCategory { get; set; }

    /// <summary>Site to show expanded (query/form field "s").</summary>
    [BindProperty(SupportsGet = true, Name = "s")]
    public int? OpenSite { get; set; }

    /// <summary>Show the AI assistants section expanded (query field "a").</summary>
    [BindProperty(SupportsGet = true, Name = "a")]
    public bool OpenAssistants { get; set; }

    [TempData]
    public string? Error { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        IsConfigure = true;
        Title = "Configure";
        await LoadShellAsync(store, ct);
        Tree = await store.GetTreeAsync(ct);
    }

    // ---- Categories ----

    public Task<IActionResult> OnPostAddCategoryAsync(string? name, CancellationToken ct) =>
        Run(async () => OpenCategory = (await store.AddCategoryAsync(name, ct)).Id);

    public Task<IActionResult> OnPostRenameCategoryAsync(int id, string? name, CancellationToken ct) =>
        Run(() => store.RenameCategoryAsync(id, name, ct));

    public Task<IActionResult> OnPostMoveCategoryAsync(int id, int delta, CancellationToken ct) =>
        Run(() => store.MoveCategoryAsync(id, delta, ct));

    public Task<IActionResult> OnPostDeleteCategoryAsync(int id, CancellationToken ct) =>
        Run(async () => { await store.DeleteCategoryAsync(id, ct); OpenCategory = OpenSite = null; });

    // ---- Sites ----

    public Task<IActionResult> OnPostAddSiteAsync(int categoryId, string? name, CancellationToken ct) =>
        Run(async () => OpenSite = (await store.AddSiteAsync(categoryId, name, ct)).Id);

    public Task<IActionResult> OnPostRenameSiteAsync(int id, string? name, CancellationToken ct) =>
        Run(() => store.RenameSiteAsync(id, name, ct));

    public Task<IActionResult> OnPostMoveSiteAsync(int id, int delta, CancellationToken ct) =>
        Run(() => store.MoveSiteAsync(id, delta, ct));

    public Task<IActionResult> OnPostDeleteSiteAsync(int id, CancellationToken ct) =>
        Run(async () => { await store.DeleteSiteAsync(id, ct); OpenSite = null; });

    // ---- Links ----

    public Task<IActionResult> OnPostSaveLinkAsync(int siteId, LinkEnvironment environment, string? url, CancellationToken ct) =>
        Run(() => store.SaveLinkAsync(siteId, environment, url, ct));

    public Task<IActionResult> OnPostDeleteLinkAsync(int id, CancellationToken ct) =>
        Run(() => store.DeleteLinkAsync(id, ct));

    // ---- AI assistants ----

    public Task<IActionResult> OnPostAddAssistantAsync(string? name, string? url, bool acceptsPrompt, string? icon, CancellationToken ct) =>
        Run(() => store.AddQuickLinkAsync(name, url, acceptsPrompt, icon, ct), "assistants");

    public Task<IActionResult> OnPostSaveAssistantAsync(int id, string? name, string? url, bool acceptsPrompt, string? icon, CancellationToken ct) =>
        Run(() => store.UpdateQuickLinkAsync(id, name, url, acceptsPrompt, icon, ct), "assistants");

    public Task<IActionResult> OnPostMoveAssistantAsync(int id, int delta, CancellationToken ct) =>
        Run(() => store.MoveQuickLinkAsync(id, delta, ct), "assistants");

    public Task<IActionResult> OnPostDeleteAssistantAsync(int id, CancellationToken ct) =>
        Run(() => store.DeleteQuickLinkAsync(id, ct), "assistants");

    /// <summary>Runs one change, keeps any validation message for the next GET, and redirects (post/redirect/get).</summary>
    private async Task<IActionResult> Run(Func<Task> change, string? fragment = null)
    {
        try
        {
            await change();
        }
        catch (FavoritesValidationException ex)
        {
            Error = ex.Message;
        }

        fragment ??= OpenSite is int s ? $"s-{s}" : OpenCategory is int c ? $"c-{c}" : null;
        var a = fragment == "assistants" ? true : (bool?)null;
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: new { c = OpenCategory, s = OpenSite, a }, fragment: fragment);
    }
}
