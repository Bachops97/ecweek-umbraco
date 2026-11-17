
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

// Public forms (contact, event registration, downloads, pledge, newsletter) - see Forms/FormEndpoints.cs
app.MapPost("/forms/{key}", MyProject.Forms.FormEndpoints.Handle).DisableAntiforgery();
app.MapPost("/contact/submit", (HttpContext ctx) => MyProject.Forms.FormEndpoints.Handle(ctx, "contact")).DisableAntiforgery(); // kept for the older contact markup

await app.RunAsync();
