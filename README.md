# Online.Favorites

A personal start page of favorite sites, served at **https://localhost**. It's a fresh rewrite of
Customizable.Menu with a much smaller footprint:

- **No IIS.** Kestrel runs as a Windows service and listens on `https://localhost` (loopback only).
- **No JavaScript on the menu, and no client libraries anywhere.** The menu's Content-Security-Policy
  blocks scripts outright. Accordions are `<details>`, delete confirmations are native popovers, page
  changes cross-fade with CSS view transitions, and every edit is a plain form post.
- **Configure saves as you go.** The only script is `wwwroot/js/configure.js` (about 15 lines,
  Configure page only). It saves a row when focus leaves it after a change. Pressing Enter in a field
  also saves, even without the script.
- **Fast menu.** Each menu page is rendered once and then served from memory (ASP.NET Core output
  cache). Any change on Configure clears the cached pages. Static files are fingerprinted, compressed
  and cached by the browser.
- **SQLite** (EF Core 10, WAL mode), stored at `%ProgramData%\Online.Favorites\favorites.db`.
- **AI assistants** replace the search icons: ChatGPT, Claude, Gemini, Copilot, Perplexity and Grok.
  Type into the ask box and click an icon to open that assistant with your question.

Built on .NET 10 (LTS) with Razor Pages.

## Layout

```
src/Online.Favorites/
  Data/            Entities, DbContext, FavoritesStore (all reads/writes), LegacyImporter, Migrations
  Infrastructure/  Hosting: database path, localhost certificate, security headers, icon helper
  Pages/           Index (menu), Configure (editor), Error, Shared/_Layout
  wwwroot/         css/site.css, img/ (background, logo), icons/ui.svg, icons/assistants/*.svg
tests/Online.Favorites.Tests/   Store and importer tests (in-memory SQLite)
scripts/                        Install-Service.ps1, Uninstall-Service.ps1, New-LocalhostCertificate.ps1
```

## Develop

```powershell
dotnet tool restore
dotnet run --project src/Online.Favorites --launch-profile https   # https://localhost:7297
dotnet test
```

Development uses the .NET dev certificate (`dotnet dev-certs https --trust`). The database is created
at `src/Online.Favorites/App_Data/favorites.db`, which git ignores.

To change the schema:

```powershell
dotnet ef migrations add <Name> --project src/Online.Favorites --output-dir Data/Migrations
```

Migrations are applied automatically at startup.

## Install as a service (replaces IIS)

1. Free port 443. In IIS Manager, remove the `https` binding for the old Customizable.Menu site, or
   stop that site.
2. In an **administrator** PowerShell:

   ```powershell
   .\scripts\Install-Service.ps1 -LegacyDatabase 'D:\GitHub\CustomizedMenu\Customizable.Menu\Data\Application.db'
   ```

   The script:
   - creates and trusts a `localhost` certificate (friendly name `Online.Favorites`, in LocalMachine\My);
   - publishes a self-contained `Online.Favorites.exe` to `C:\Program Files\Online.Favorites`;
   - copies your old menus, sites and links into the new database (it only reads the old file);
   - registers and starts the `Online.Favorites` service, set to start automatically and restart if it fails.

3. Your browser's start page stays `https://localhost`.

Re-run the script to upgrade. Your data is kept. Use `-Port 8443` to listen on another port.
`.\scripts\Uninstall-Service.ps1` removes the service but keeps your data.

To import later or by hand, run this while the new database is still empty:

```powershell
& 'C:\Program Files\Online.Favorites\Online.Favorites.exe' --import 'path\to\Application.db'
```

The import doesn't bring over the legacy Configuration (encryption key, max menu count) or the
AuditHistory tables.

## AI assistants

Edit them under **Configure → AI assistants**: name, URL, icon file and whether the site accepts a
prompt. An assistant marked **Prompt** receives the ask box text as `?q=...`. Others open their home
page. Pressing Enter in the ask box uses the first prompt-capable assistant.

| Assistant  | URL                                | Prompt |
|------------|------------------------------------|--------|
| ChatGPT    | https://chatgpt.com/               | yes    |
| Claude     | https://claude.ai/new              | yes    |
| Gemini     | https://gemini.google.com/app      | no     |
| Copilot    | https://copilot.microsoft.com/     | yes    |
| Perplexity | https://www.perplexity.ai/search   | yes    |
| Grok       | https://grok.com/                  | yes    |

These `?q=` behaviors aren't official APIs and can change. If one stops pre-filling, untick **Prompt**.

The icons in `wwwroot/icons/assistants` are simple stand-in marks drawn for this project. To use
official logos, drop an SVG with the same file name into that folder. Simple Icons
(https://simpleicons.org, CC0) has most of them; give them a light fill so they show on the dark
background.

## Assets

The jackdaw background and logo come from Customizable.Menu. The background is resized to a 100 KB
WebP. The look is recreated in `site.css`, and none of the Kitzu template's files are used.

## Security and CI

Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md). GitHub Actions run CI
(locked restore, build, format check, tests), OWASP Dependency-Check, OpenSSF Scorecard, and workflow
linting (zizmor, actionlint). Dependabot proposes NuGet and Actions updates weekly.

## License

[MIT](LICENSE)
