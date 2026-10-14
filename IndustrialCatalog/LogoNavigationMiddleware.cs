using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

// Add the common logo behavior to server-rendered HTML, including future pages.
public sealed class LogoNavigationMiddleware
{
    private readonly RequestDelegate next;
    public LogoNavigationMiddleware(RequestDelegate next) => this.next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsPost(context.Request.Method)&&new[]{"/login","/kayit","/forgot-password"}.Contains(context.Request.Path.Value)) ||
            context.Request.Path.StartsWithSegments("/api") ||
            Path.HasExtension(context.Request.Path.Value))
        {
            await next(context);
            return;
        }
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            buffer.Position = 0;
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var html = await reader.ReadToEndAsync();
                if (!html.Contains("/logo-navigation.js", StringComparison.Ordinal))
                {
                    const string script = "<script defer src=\"/logo-navigation.js?v=ownership-20261012-2\"></script>";
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    html = headEnd >= 0 ? html.Insert(headEnd, script) : html + script;
                }
                // Render the same loader before any React or server content can paint.
                if (!html.Contains("/page-loading.js", StringComparison.Ordinal))
                {
                    var end = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    const string assets = "<link rel=\"stylesheet\" href=\"/page-loading.css?v=brand-progress-4\"><link rel=\"preload\" as=\"image\" href=\"/inokskar-logo.svg\"><script src=\"/page-loading.js?v=brand-progress-4\"></script>";
                    if(end>=0)html=html.Insert(end,assets);
                    html=Regex.Replace(html,@"<body\b[^>]*>",m=>m.Value+"<div id=\"inokskar-page-loader\" role=\"status\" aria-label=\"Sayfa yükleniyor\" aria-busy=\"true\"><div class=\"page-loading-brand\"><div class=\"page-loading-mark\"><img class=\"page-loading-logo\" src=\"/inokskar-logo.svg\" alt=\"İNOKSKAR\"></div><span class=\"page-loading-track\" aria-hidden=\"true\"></span><span class=\"page-loading-caption\" aria-hidden=\"true\">Yükleniyor</span></div></div><noscript><style>#inokskar-page-loader{display:none}</style></noscript>",RegexOptions.IgnoreCase);
                }
                // Install shared DOM subscriptions before deferred page owners start.
                var headStart=html.IndexOf("<head",StringComparison.OrdinalIgnoreCase);
                if(headStart>=0){headStart=html.IndexOf('>',headStart)+1;var common="<script src=\"/dom-lifecycle.js?v=ownership-1\"></script>";if(!html.Contains("/r123-security.js",StringComparison.Ordinal))common+="<script src=\"/r123-security.js?v=ownership-1\"></script>";html=html.Insert(headStart,common);}
                // Some legacy server pages omit the shared layout stylesheet.
                if (!html.Contains("/r22-layout-fixes.css", StringComparison.Ordinal) &&
                    !html.Contains("/site-bundle.css", StringComparison.Ordinal))
                {
                    const string style = "<link rel=\"stylesheet\" href=\"/r22-layout-fixes.css?v=qa-20261011-1\">";
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (headEnd >= 0) html = html.Insert(headEnd, style);
                }
                if (!html.Contains("/membership.js", StringComparison.Ordinal) && (context.Request.Path == "/admin" || context.Request.Path == "/admin/users"))
                {
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (headEnd >= 0) html = html.Insert(headEnd, "<script defer src=\"/membership.js?v=membership-6\"></script>");
                }
                // Load the server menu reset last so page-specific desktop rules
                // cannot leak into the shared mobile sheet on current or future pages.
                if (!html.Contains("/public-menu.css", StringComparison.Ordinal))
                {
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (headEnd >= 0) html = html.Insert(headEnd, "<link rel=\"stylesheet\" href=\"/public-menu.css?v=menu-1\">");
                }
                if (!html.Contains("/customer-ui.js", StringComparison.Ordinal))
                {
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (headEnd >= 0) html = html.Insert(headEnd, "<link rel='stylesheet' href='/customer-ui.css?v=customer-ui-8'><script defer src='/customer-ui.js?v=customer-ui-8'></script>");
                }
                html = html.Replace("/r125-customer.js\"", "/r125-customer.js?v=profile-consent-2\"");
                // Refresh the shared close-control styles on every HTML page.
                html = html.Replace("/r129-feedback.css?v=1", "/r129-feedback.css?v=close-controls-2");
                // These assets must advance together: existing browsers can retain old
                // fetch hooks and show success notifications for background heartbeats.
                html = Regex.Replace(html, @"/(r129-feedback\.js|live-support\.js)(?:\?[^""'<>\s]*)?", "/$1?v=ownership-20261012-2");
                // Shared hand-written assets change independently of the hashed Vite bundle.
                html = Regex.Replace(html, @"/(r95-warranty-admin\.js|logo-navigation\.js|r13-pages\.js|r22-layout-fixes\.css|r136-references\.css)(?:\?[^""'<>\s]*)?", "/$1?v=ownership-20261012-2");
                html = Regex.Replace(html, @"/logo-navigation\.js(?:\?[^""'<>\s]*)?", "/logo-navigation.js?v=ownership-20261012-2");
                html = Regex.Replace(html, @"/(r13-pages\.css|r22-layout-fixes\.css)(?:\?[^""'<>\s]*)?", "/$1?v=ownership-20261012-2");
                if (!html.Contains("/live-support.js", StringComparison.Ordinal) && !new[]{"/login","/kayit","/forgot-password","/reset-password"}.Contains(context.Request.Path.Value))
                {
                    var end = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    if(end>=0) html=html.Insert(end,"<script defer src=\"/live-support.js?v=ownership-20261012-2\"></script>");
                }
                html = SmsVisibility.Apply(html,context,context.RequestServices.GetRequiredService<SmsSettingsStore>());
                html=Regex.Replace(html,@"/(r123-security\.js|r129-feedback\.js|membership\.js|customer-ui\.js|r125-customer\.js)(?:\?[^""'<>\s]*)?","/$1?v=ownership-20261012-2");
                html=UiStylesheets.Rewrite(html);
                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength = bytes.Length;
                context.Response.Body = original;
                await original.WriteAsync(bytes);
            }
            else
            {
                context.Response.Body = original;
                await buffer.CopyToAsync(original);
            }
        }
        finally { context.Response.Body = original; }
    }
}
