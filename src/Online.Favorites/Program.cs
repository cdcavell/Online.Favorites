using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Online.Favorites.Data;
using Online.Favorites.Infrastructure;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // A Windows service starts in System32; serve wwwroot from beside the exe instead.
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
});

builder.Services.AddWindowsService(options => options.ServiceName = "Online.Favorites");

// Outside Development there is no IIS and no launch profile: Kestrel itself listens on
// https://localhost (loopback only) with the certificate made by scripts/New-LocalhostCertificate.ps1.
if (!builder.Environment.IsDevelopment())
{
    var port = builder.Configuration.GetValue("Hosting:HttpsPort", 443);
    builder.WebHost.ConfigureKestrel(kestrel =>
        kestrel.ListenLocalhost(port, listen => listen.UseHttps(LocalhostCertificate.Find())));
}

var databasePath = DatabaseSetup.ResolvePath(builder.Configuration, builder.Environment);
builder.Services.AddDbContext<FavoritesDbContext>(options =>
    options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()));
builder.Services.AddScoped<FavoritesStore>();

builder.Services.AddRazorPages();

// The menu pages are rendered once and then served from memory until FavoritesStore
// evicts the "menu" tag after any change made on the Configure page.
builder.Services.AddOutputCache(options =>
    options.AddPolicy(MenuCache.PolicyName, policy => policy
        .Expire(TimeSpan.FromDays(365))
        .Tag(FavoritesStore.MenuCacheTag)
        .SetVaryByRouteValue("id")));

var app = builder.Build();

await DatabaseSetup.MigrateAsync(app.Services);

// One-time copy from the old Customizable.Menu database:  Online.Favorites.exe --import "path\to\Application.db"
if (app.Configuration["import"] is { Length: > 0 } legacyDatabase)
    return await LegacyImporter.RunFromCommandLineAsync(app.Services, legacyDatabase);

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
else
    app.UseExceptionHandler("/Error");

app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseSecurityHeaders();
app.UseRouting();
app.UseOutputCache();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

await app.RunAsync();
return 0;
