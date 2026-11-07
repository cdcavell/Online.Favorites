using Microsoft.EntityFrameworkCore;

namespace Online.Favorites.Data;

public sealed class FavoritesDbContext(DbContextOptions<FavoritesDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SiteLink> Links => Set<SiteLink>();
    public DbSet<QuickLink> QuickLinks => Set<QuickLink>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
            e.HasIndex(x => x.Ordinal);
            e.HasMany(x => x.Sites).WithOne(x => x.Category).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Site>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(Site.NameMaxLength).IsRequired();
            e.HasIndex(x => new { x.CategoryId, x.Ordinal });
            e.HasMany(x => x.Links).WithOne(x => x.Site).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SiteLink>(e =>
        {
            e.ToTable("Links");
            e.Property(x => x.Url).HasMaxLength(SiteLink.UrlMaxLength).IsRequired();
            e.HasIndex(x => new { x.SiteId, x.Environment }).IsUnique();
        });

        model.Entity<QuickLink>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(QuickLink.NameMaxLength).IsRequired();
            e.Property(x => x.Url).HasMaxLength(SiteLink.UrlMaxLength).IsRequired();
            e.Property(x => x.Icon).HasMaxLength(QuickLink.IconMaxLength).IsRequired();
            e.HasData(
                new QuickLink { Id = 1, Ordinal = 1, Name = "ChatGPT", Url = "https://chatgpt.com/", AcceptsPrompt = true, Icon = "chatgpt.svg" },
                new QuickLink { Id = 2, Ordinal = 2, Name = "Claude", Url = "https://claude.ai/new", AcceptsPrompt = true, Icon = "claude.svg" },
                new QuickLink { Id = 3, Ordinal = 3, Name = "Gemini", Url = "https://gemini.google.com/app", AcceptsPrompt = false, Icon = "gemini.svg" },
                new QuickLink { Id = 4, Ordinal = 4, Name = "Copilot", Url = "https://copilot.microsoft.com/", AcceptsPrompt = true, Icon = "copilot.svg" },
                new QuickLink { Id = 5, Ordinal = 5, Name = "Perplexity", Url = "https://www.perplexity.ai/search", AcceptsPrompt = true, Icon = "perplexity.svg" },
                new QuickLink { Id = 6, Ordinal = 6, Name = "Grok", Url = "https://grok.com/", AcceptsPrompt = true, Icon = "grok.svg" });
        });
    }
}
