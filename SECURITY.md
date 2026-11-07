# Security Policy

Online.Favorites is a personal start page that runs on your own machine at `https://localhost`.
It listens on the loopback address only and has no sign-in.

## Report privately

Report a suspected vulnerability through
[GitHub private vulnerability reporting](https://github.com/cdcavell/Online.Favorites/security/advisories/new)
(the **Security** tab, then **Report a vulnerability**). Do not open a public issue with exploit details.

Please include the commit or release you ran, how you ran it (`dotnet run` or the installed Windows
service), and the steps to reproduce.

Non-sensitive hardening suggestions can go to a normal issue or pull request.

## Supported versions

| Version | Supported |
| --- | --- |
| `main` | Yes. Fixes land here. |

## In scope

- anything that lets a page or another machine reach or change your favorites;
- script running on the menu page, which its Content-Security-Policy should block;
- the install scripts weakening the certificate, file permissions, or service account; and
- a vulnerable NuGet dependency or GitHub Action still referenced after a fix is available.

## Out of scope

- someone who already has administrator access to the machine; and
- vulnerabilities in ASP.NET Core, EF Core, SQLite, or the AI assistant sites. Report those to their own projects.
