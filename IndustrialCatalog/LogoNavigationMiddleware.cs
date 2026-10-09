using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

// Add the common logo behavior to server-rendered HTML, including future pages.
public sealed class LogoNavigationMiddleware
{
    private readonly RequestDelegate next;
    public LogoNavigationMiddleware(RequestDelegate next) => this.next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) ||
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
                    const string script = "<script defer src=\"/logo-navigation.js?v=central-1\"></script>";
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    html = headEnd >= 0 ? html.Insert(headEnd, script) : html + script;
                }
                // Some legacy server pages omit the shared layout stylesheet.
                if (!html.Contains("/r22-layout-fixes.css", StringComparison.Ordinal))
                {
                    const string style = "<link rel=\"stylesheet\" href=\"/r22-layout-fixes.css?v=responsive-audit-2\">";
                    var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (headEnd >= 0) html = html.Insert(headEnd, style);
                }
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
