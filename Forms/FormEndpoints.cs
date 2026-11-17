using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace MyProject.Forms;

/// <summary>
/// One handler for every public form (contact, event registration, downloads, pledge, newsletter):
/// validate -> reject spam -> store -> e-mail the visitor and the team using the editable "Email Template" nodes.
/// If no SMTP server is configured the e-mails are written to umbraco/Data/Outbox instead of being sent.
/// </summary>
public static class FormEndpoints
{
    public const string ContextPurpose = "ecweek-form-context";

    sealed record FormDef(string[] Required, string[] Optional, string VisitorTemplate, string? TeamTemplate);

    static readonly Dictionary<string, FormDef> Forms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["contact"] = new(new[] { "name", "phone", "email", "purpose", "notes" }, new[] { "company", "position" }, "contact-visitor", "contact-team"),
        ["register"] = new(new[] { "name", "email", "phone", "children" }, Array.Empty<string>(), "register-visitor", "register-team"),
        ["download"] = new(new[] { "name", "email" }, Array.Empty<string>(), "download-visitor", null),
        ["pledge"] = new(new[] { "name", "email", "childName" }, Array.Empty<string>(), "pledge-visitor", "pledge-team"),
        ["newsletter"] = new(new[] { "email" }, new[] { "name" }, "newsletter-visitor", "newsletter-team"),
    };

    static readonly ConcurrentDictionary<string, List<DateTime>> Hits = new();
    static readonly Regex Placeholder = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);
    static readonly Regex Url = new(@"(https?://[^\s<]+)", RegexOptions.Compiled);

    public static async Task<IResult> Handle(HttpContext http, string key)
    {
        var wantsJson = http.Request.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase);
        var back = SafePath(http.Request.Form.TryGetValue("page", out var p) ? p.ToString() : "/");
        IResult Fail(string message, int status = 400) =>
            wantsJson ? Results.Json(new { ok = false, message }, statusCode: status) : Results.Redirect(back + "?error=1");
        IResult Ok() => wantsJson ? Results.Json(new { ok = true }) : Results.Redirect(back + "?sent=1");

        if (!Forms.TryGetValue(key, out var def)) return Results.NotFound();
        var form = await http.Request.ReadFormAsync();

        // Spam traps: hidden field bots fill in, and too-fast / too-frequent submissions
        if (!string.IsNullOrEmpty(form["hp_trap_9x"])) { http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Forms").LogWarning("Form {Form}: spam trap field was filled, submission ignored", key); return Ok(); }
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (TooMany("ip:" + ip, 12, TimeSpan.FromMinutes(10))) return Fail("Too many requests. Please try again in a few minutes.", 429);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in def.Required.Concat(def.Optional))
        {
            var v = form[name].ToString().Trim();
            if (v.Length > 3000) v = v[..3000];
            values[name] = v;
        }
        if (def.Required.Any(r => string.IsNullOrWhiteSpace(values[r]))) return Fail("Please fill in all required fields.");
        if (values.TryGetValue("email", out var email) && !ValidEmail(email)) return Fail("Please enter a valid email address.");
        if (values.TryGetValue("email", out email) && TooMany("mail:" + email.ToLowerInvariant(), 4, TimeSpan.FromHours(1))) return Fail("This email address has been used too many times. Please try again later.", 429);

        // Trusted context (event / download details) is signed on the server when the page is rendered, so visitors cannot forge it
        var siteUrl = $"{http.Request.Scheme}://{http.Request.Host}";
        values["siteUrl"] = siteUrl;
        var token = form["ctx"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var json = http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(ContextPurpose).Unprotect(token);
                foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new()) values[kv.Key] = kv.Value;
            }
            catch { return Fail("This form has expired. Please reload the page and try again."); }
        }

        // 1. Store
        var env = http.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var dir = Path.Combine(env.ContentRootPath, "umbraco", "Data", "Submissions", key.ToLowerInvariant());
        Directory.CreateDirectory(dir);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
        var saved = new List<string>();
        foreach (var f in form.Files.Take(5).Where(f => f.Length > 0 && f.Length <= 5 * 1024 * 1024))
        {
            var fileName = id + "-" + Path.GetFileName(f.FileName);
            await using var fs = File.Create(Path.Combine(dir, fileName));
            await f.CopyToAsync(fs);
            saved.Add(fileName);
        }
        await File.WriteAllTextAsync(Path.Combine(dir, id + ".json"),
            JsonSerializer.Serialize(new { received = DateTime.UtcNow, form = key, values, files = saved }, new JsonSerializerOptions { WriteIndented = true }));

        var log = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Forms");

        // 1b. Also store as a node under Content -> Form Submissions so the team can read it in the backoffice
        try { SaveToBackoffice(http.RequestServices, key, values); }
        catch (Exception ex) { log.LogError(ex, "Form {Form}: could not save submission {Id} to the backoffice", key, id); }

        // 2. E-mail (visitor + team) - switched off unless Forms:SendEmails is true
        if (!http.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("Forms:SendEmails")) return Ok();
        try
        {
            var templates = LoadTemplates(http.RequestServices);
            await Send(http.RequestServices, env, log, templates, def.VisitorTemplate, values);
            if (def.TeamTemplate != null) await Send(http.RequestServices, env, log, templates, def.TeamTemplate, values);
        }
        catch (Exception ex) { log.LogError(ex, "Form {Form}: submission {Id} was stored but the e-mails failed", key, id); }
        return Ok();
    }

    static void SaveToBackoffice(IServiceProvider sp, string key, Dictionary<string, string> values)
    {
        var cs = sp.GetRequiredService<IContentService>();
        var root = cs.GetRootContent().FirstOrDefault(c => c.ContentType.Alias == "formSubmissions");
        if (root == null) return;
        string V(string k) => values.TryGetValue(k, out var v) ? v : "";
        var skip = new[] { "name", "email", "phone", "siteUrl" };
        var details = string.Join("\n", values.Where(kv => !skip.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key + ": " + kv.Value));
        var who = V("name"); if (who == "") who = V("email");
        var node = cs.Create($"{key} - {who} - {DateTime.Now:yyyy-MM-dd HH:mm}", root, "formSubmission");
        node.SetValue("formType", key);
        node.SetValue("visitorName", V("name"));
        node.SetValue("visitorEmail", V("email"));
        node.SetValue("visitorPhone", V("phone"));
        node.SetValue("submittedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        node.SetValue("details", details);
        cs.Save(node);
    }

    static Dictionary<string, (string Subject, string To, string Body)> LoadTemplates(IServiceProvider sp)
    {
        var result = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);
        var query = sp.GetRequiredService<IPublishedContentQuery>();
        foreach (var node in query.ContentAtRoot().SelectMany(r => r.DescendantsOrSelf()).Where(c => c.ContentType.Alias == "emailTemplate"))
            result[node.Value<string>("templateKey") ?? ""] = (node.Value<string>("subject") ?? "", node.Value<string>("toAddress") ?? "", node.Value<string>("body") ?? "");
        return result;
    }

    static async Task Send(IServiceProvider sp, IWebHostEnvironment env, ILogger log, Dictionary<string, (string Subject, string To, string Body)> templates, string key, Dictionary<string, string> values)
    {
        if (!templates.TryGetValue(key, out var t)) { log.LogWarning("Email template '{Key}' does not exist", key); return; }
        var to = Fill(t.To, values, false).Trim();
        if (!ValidEmail(to)) { log.LogWarning("Email template '{Key}' has no valid recipient ('{To}')", key, to); return; }
        var subject = Regex.Replace(Fill(t.Subject, values, false), @"[\r\n]+", " ").Trim();
        var html = Layout(Paragraphs(Fill(t.Body, values, true)));
        try
        {
            // Umbraco silently skips sending when no SMTP / pickup folder is configured, so check first and keep a copy instead
            if (sp.GetRequiredService<IOptionsMonitor<GlobalSettings>>().CurrentValue.Smtp is null) throw new InvalidOperationException("no SMTP settings configured");
            await sp.GetRequiredService<IEmailSender>().SendAsync(new EmailMessage("no-reply@ecweek.local", to, subject, html, true), "ecweek-form");
        }
        catch (Exception ex)
        {
            // No SMTP server configured yet: keep the rendered e-mail so it can be reviewed
            var dir = Path.Combine(env.ContentRootPath, "umbraco", "Data", "Outbox");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + key + ".html");
            await File.WriteAllTextAsync(file, $"<!-- To: {WebUtility.HtmlEncode(to)} | Subject: {WebUtility.HtmlEncode(subject)} -->\n" + html);
            log.LogInformation("E-mail '{Key}' to {To} not sent ({Reason}); saved to {File}", key, to, ex.Message, file);
        }
    }

    static string Fill(string text, Dictionary<string, string> values, bool encode) =>
        Placeholder.Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? (encode ? WebUtility.HtmlEncode(v) : v) : "");

    static string Paragraphs(string encoded)
    {
        var sb = new StringBuilder();
        foreach (var para in encoded.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            sb.Append("<p style=\"margin:0 0 16px\">").Append(Url.Replace(para.Replace("\n", "<br>"), "<a href=\"$1\" style=\"color:#3c46b2\">$1</a>")).Append("</p>");
        return sb.ToString();
    }

    static string Layout(string inner) =>
        "<!doctype html><html><body style=\"margin:0;background:#f7f2e8;font-family:Arial,Helvetica,sans-serif;color:#1d1d1b\">" +
        "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td align=\"center\" style=\"padding:24px\">" +
        "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#ffffff\">" +
        "<tr><td style=\"background:#49ad71;color:#ffffff;padding:20px 28px;font-size:20px;font-weight:bold\">Early Childhood Week</td></tr>" +
        "<tr><td style=\"padding:28px;font-size:15px;line-height:1.6\">" + inner + "</td></tr>" +
        "<tr><td style=\"background:#f7f2e8;padding:14px 28px;font-size:12px;color:#777\">You received this email because of a request on the Early Childhood Week website.</td></tr>" +
        "</table></td></tr></table></body></html>";

    static bool ValidEmail(string v)
    {
        if (string.IsNullOrWhiteSpace(v) || v.Length > 254 || v.Contains(' ')) return false;
        try { return new MailAddress(v).Address == v; } catch { return false; }
    }

    static string SafePath(string p) => p.StartsWith('/') && !p.StartsWith("//") && !p.Contains('\\') && !p.Contains('\r') && !p.Contains('\n') ? p : "/";

    static bool TooMany(string key, int max, TimeSpan window)
    {
        var list = Hits.GetOrAdd(key, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(t => t < DateTime.UtcNow - window);
            if (list.Count >= max) return true;
            list.Add(DateTime.UtcNow);
            return false;
        }
    }
}

/// <summary>What a popup form needs to render (see Views/Partials/FormDialog.cshtml).</summary>
public sealed record FormDialogModel(string Id, string Key, string Heading, string Intro, string ButtonText, string Thanks, Dictionary<string, string>? Context = null);
