# Early Childhood Week (ECWEEK) – Umbraco site

A traditional (non-headless) Umbraco 17 website built from the ECWEEK Figma design, as a learning project.

- Umbraco 17.8 on .NET 10, Razor views, SQLite for local development
- Pages are built from **blocks** (Block List) edited in the backoffice

## Run it locally

You need the **.NET 10 SDK**.

1. Create `appsettings.Local.json` (it is git-ignored) next to `appsettings.json`:

   ```json
   {
     "ConnectionStrings": {
       "umbracoDbDSN": "Data Source=|DataDirectory|/Umbraco.sqlite.db;Foreign Keys=True;Pooling=True",
       "umbracoDbDSN_ProviderName": "Microsoft.Data.Sqlite"
     },
     "Umbraco": { "CMS": { "Imaging": { "HMACSecretKey": "<generate a long random base64 string>" } } }
   }
   ```

2. `dotnet run` and open the URL it prints. On first run Umbraco asks you to create the admin user.

> The database, uploaded media and contact-form submissions are **not** in git (they are in `.gitignore`).
> A fresh clone therefore starts without content: the document types, blocks and pages have to be recreated
> in the backoffice (or restored from a backup of the database + `wwwroot/media`).

## Structure

| What | Where |
|---|---|
| Shared layout (header, menu, footer) | `Views/Layout.cshtml` |
| Home page / inner pages | `Views/HomePage.cshtml`, `Views/contentPage.cshtml` (both just render the page's blocks) |
| One view per block | `Views/Partials/blocklist/Components/<blockAlias>.cshtml` |
| Hero carousel grouping | `Views/Partials/blocklist/default.cshtml`, `_heroCarousel.cshtml` |
| Styles / scripts | `wwwroot/css/ecweek.css`, `wwwroot/js/site.js` |
| Contact form handler | `Program.cs` (`POST /contact/submit`) |

### Blocks

Hero (carousel slides), Text + Image, Card Section (events / news), Partners, Page Banner, Timeline,
Testimonials, Feature Cards, Icon Columns, Download, Partner Directory, Contact, FAQ.

## Forms and e-mail

All public forms go through one handler, `Forms/FormEndpoints.cs` (`POST /forms/{key}`):
`contact`, `register` (event), `download` (toolkit, Parent Pack, report), `pledge`, `newsletter`.

For every submission it checks required fields and the e-mail address, ignores bots (hidden field), limits repeated
requests (per IP and per e-mail address), stores the submission as JSON in `umbraco/Data/Submissions/<form>/`
(git-ignored, contains personal data) and sends e-mails.

**E-mail texts are editable in the backoffice**: Content -> *Email Templates* (a root node, never a public page).
Each template has a key, subject, "send to" (`{{email}}` = the visitor, or a team address) and a message with `{{placeholders}}`
(the list is shown in the template). Team templates go to `team@example.test` -> change them to the real address.

Popup forms are blocks: add a **Form Popup Block** (download / pledge / newsletter) to a page and point any button at `#popup-<popupId>`.
Event registration is built into the event page. Download and event details are signed on the server so visitors cannot change them.

**Sending e-mail.** In development `appsettings.Local.json` uses Umbraco's pickup-folder mode, so e-mails are written as
`.eml` files to `umbraco/Data/Outbox/` (open them with any mail app). To send for real, replace that block with network settings:

```json
"Umbraco": { "CMS": { "Global": { "Smtp": { "From": "no-reply@your-domain", "Host": "smtp.your-provider.com", "Port": 587,
  "SecureSocketOptions": "StartTls", "Username": "...", "Password": "..." } } } }
```

## Notes

- Fonts: Oswald and IBM Plex Mono stand in for the design's Manuka and Fabrikat Mono until the licensed fonts are added.
- The navigation links are static for now (they point at the page URLs); making them follow the content tree is a later step.
