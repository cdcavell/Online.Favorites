namespace Online.Favorites.Data;

/// <summary>Which deployment of a site a link points at. Values match the legacy Customizable.Menu enum.</summary>
public enum LinkEnvironment
{
    Development = 0,
    Staging = 1,
    Production = 2,
    Public = 3,
}

/// <summary>An item that keeps a 1-based position among its siblings.</summary>
public interface IOrdered
{
    int Id { get; }
    int Ordinal { get; set; }
}

/// <summary>A left-menu entry, e.g. "News / Weather".</summary>
public sealed class Category : IOrdered
{
    public const int NameMaxLength = 40;

    public int Id { get; set; }
    public int Ordinal { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Site> Sites { get; set; } = [];
}

/// <summary>A site within a category, e.g. "WBRZ-TV".</summary>
public sealed class Site : IOrdered
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int Ordinal { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<SiteLink> Links { get; set; } = [];
}

/// <summary>One URL for a site; at most one per environment.</summary>
public sealed class SiteLink
{
    public const int UrlMaxLength = 2048;

    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site? Site { get; set; }
    public LinkEnvironment Environment { get; set; }
    public string Url { get; set; } = string.Empty;
}

/// <summary>An AI assistant shown on the right edge of every page.</summary>
public sealed class QuickLink : IOrdered
{
    public const int NameMaxLength = 40;
    public const int IconMaxLength = 60;

    public int Id { get; set; }
    public int Ordinal { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>True when the site accepts a prompt as <c>?q=</c> on <see cref="Url"/>.</summary>
    public bool AcceptsPrompt { get; set; }

    /// <summary>SVG file name under wwwroot/icons/assistants.</summary>
    public string Icon { get; set; } = string.Empty;
}
