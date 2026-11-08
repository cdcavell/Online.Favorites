using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Html;
using Microsoft.EntityFrameworkCore;
using Online.Favorites.Data;

namespace Online.Favorites.Infrastructure;

public static class MenuCache
{
    public const string PolicyName = "Menu";
}

internal static class DatabaseSetup
{
    /// <summary>
    /// Database:Path from configuration (relative paths are under the content root), otherwise
    /// %ProgramData%\Online.Favorites\favorites.db so the service and upgrades share one file.
    /// </summary>
    public static string ResolvePath(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Database:Path"];
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Online.Favorites", "favorites.db")
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured), environment.ContentRootPath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavoritesDbContext>();
        await db.Database.MigrateAsync();
        // Write-ahead logging: readers never wait on a save. The setting is stored in the file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
}

internal static class AssistantIcons
{
    public const string RequestPath = "/icons/assistants";

    /// <summary>
    /// Serves wwwroot/icons/assistants straight from disk. MapStaticAssets only knows files that existed
    /// at build time, so an SVG dropped into the installed folder later would otherwise be a 404.
    /// "no-cache" makes the browser revalidate, so replacing an icon shows up on the next load.
    /// </summary>
    public static IApplicationBuilder UseAssistantIcons(this WebApplication app)
    {
        // WebRootPath can be null (e.g. depending on how the content root was given), so fall back to it explicitly.
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, "icons", "assistants");
        Directory.CreateDirectory(folder);

        return app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = RequestPath,
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(folder),
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
        });
    }
}

internal static class LocalhostCertificate
{
    public const string FriendlyName = "Online.Favorites";

    /// <summary>The newest valid certificate created by scripts/New-LocalhostCertificate.ps1.</summary>
    public static X509Certificate2 Find()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var now = DateTime.Now;

        return store.Certificates
            .Where(c => c.FriendlyName == FriendlyName && c.HasPrivateKey && c.NotBefore <= now && c.NotAfter > now)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No valid certificate named '{FriendlyName}' was found in LocalMachine\\My. " +
                "Run scripts\\New-LocalhostCertificate.ps1 as administrator.");
    }
}

internal static class SecurityHeaders
{
    // No script-src: the browser refuses to run any JavaScript on the menu.
    // form-action allows https: so the prompt box can open an AI assistant with the question.
    private const string ContentSecurityPolicy =
        "default-src 'none'; style-src 'self'; img-src 'self'; " +
        "form-action 'self' https:; frame-ancestors 'none'; base-uri 'none'";

    // Configure alone may load its one same-origin file, js/configure.js (save on leaving a row).
    private const string ConfigureContentSecurityPolicy = ContentSecurityPolicy + "; script-src 'self'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = context.Request.Path.StartsWithSegments("/Configure", StringComparison.OrdinalIgnoreCase)
                ? ConfigureContentSecurityPolicy
                : ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            return next(context);
        });
}

/// <summary>Inline reference to a symbol in wwwroot/icons/ui.svg.</summary>
public static class Icon
{
    public static IHtmlContent Use(string name) =>
        new HtmlString($"""<svg class="i" aria-hidden="true" focusable="false"><use href="/icons/ui.svg#{name}"></use></svg>""");
}
