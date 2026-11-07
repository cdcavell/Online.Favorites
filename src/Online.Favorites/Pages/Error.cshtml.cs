using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Online.Favorites.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : ShellPageModel
{
    public string? RequestId { get; private set; }
    public int ErrorCode { get; private set; }

    public void OnGet(int? code) => Handle(code);

    public void OnPost(int? code) => Handle(code);

    private void Handle(int? code)
    {
        ErrorCode = code ?? 500;
        Title = ErrorCode == 404 ? "Not Found" : "Error";
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
