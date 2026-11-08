using System.Text.RegularExpressions;

namespace Online.Favorites.Data;

/// <summary>A user-facing problem with input; its message is safe to show on the page.</summary>
public sealed class FavoritesValidationException(string message) : Exception(message);

internal static partial class Validate
{
    private static readonly string[] AllowedSchemes = [Uri.UriSchemeHttps, Uri.UriSchemeHttp, Uri.UriSchemeFile];

    public static string Text(string? value, int maxLength, string label)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new FavoritesValidationException($"{label} is required.");
        if (text.Length > maxLength)
            throw new FavoritesValidationException($"{label} cannot be longer than {maxLength} characters.");
        return text;
    }

    public static string Url(string? value, string label)
    {
        var text = Text(value, SiteLink.UrlMaxLength, label);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !AllowedSchemes.Contains(uri.Scheme))
            throw new FavoritesValidationException($"{label} must be a full http://, https:// or file:// address.");
        return text;
    }

    public static string IconFile(string? value)
    {
        var text = Text(value, QuickLink.IconMaxLength, "Icon file");
        if (!IconFilePattern().IsMatch(text))
            throw new FavoritesValidationException("Icon file must be an .svg file name (letters, digits, '.', '_' or '-', no folders) in wwwroot/icons/assistants.");
        return text;
    }

    public static LinkEnvironment Environment(LinkEnvironment value) =>
        Enum.IsDefined(value) ? value : throw new FavoritesValidationException("Unknown environment.");

    // No slashes and no leading dot, so the name can only point at a file inside the icons folder.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\.svg$")]
    private static partial Regex IconFilePattern();
}
