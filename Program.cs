
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
#endif

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

// Contact form: validates, then stores the submission (and any attached files) in umbraco/Data/ContactSubmissions.
// No mail server is configured yet, so nothing is e-mailed; swap the "store" step for an e-mail send when SMTP exists.
app.MapPost("/contact/submit", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var page = form["page"].ToString();
    if (!page.StartsWith("/") || page.StartsWith("//")) page = "/";

    if (!string.IsNullOrEmpty(form["website"])) return Results.Redirect(page); // honeypot: bots fill this hidden field
    string[] required = { "name", "phone", "email", "purpose", "notes" };
    if (required.Any(k => string.IsNullOrWhiteSpace(form[k]))) return Results.Redirect(page + "?error=1");

    var dir = Path.Combine(app.Environment.ContentRootPath, "umbraco", "Data", "ContactSubmissions");
    Directory.CreateDirectory(dir);
    var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
    var saved = new List<string>();
    foreach (var f in form.Files.Take(5).Where(f => f.Length > 0 && f.Length <= 5 * 1024 * 1024))
    {
        var name = id + "-" + Path.GetFileName(f.FileName);
        await using var fs = File.Create(Path.Combine(dir, name));
        await f.CopyToAsync(fs);
        saved.Add(name);
    }
    var record = new
    {
        received = DateTime.UtcNow,
        name = form["name"].ToString(), phone = form["phone"].ToString(), company = form["company"].ToString(),
        position = form["position"].ToString(), email = form["email"].ToString(), purpose = form["purpose"].ToString(),
        notes = form["notes"].ToString(), files = saved
    };
    await File.WriteAllTextAsync(Path.Combine(dir, id + ".json"), System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return Results.Redirect(page + "?sent=1");
}).DisableAntiforgery();

await app.RunAsync();
