using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

// Keep module ownership in source; serve ordered stylesheet groups in one request.
public static class UiStylesheets
{
    const string Order = "@layer theme, base, components, utilities, site, layout;";
    public static void Map(WebApplication app)
    {
        app.MapGet("/ui.css",(HttpContext context,IWebHostEnvironment env)=>{
            var names=context.Request.Query["files"].ToString().Split(',',StringSplitOptions.RemoveEmptyEntries);
            if(names.Length is <1 or >40||names.Any(name=>!Regex.IsMatch(name,"^[a-z0-9_-]+\\.css$")))return Results.BadRequest();
            var output=new StringBuilder(Order);
            foreach(var name in names){var file=Path.Combine(env.WebRootPath,name);if(!File.Exists(file))return Results.NotFound();output.Append('\n').Append(File.ReadAllText(file));}
            context.Response.Headers.CacheControl="no-cache";
            return Results.Text(output.ToString(),"text/css; charset=utf-8");
        });
    }
    public static string Rewrite(string html)
    {
        // Legacy server pages define their component styles inline. Keep them in
        // the same source layer as external modules, below explicit layout states.
        html=Regex.Replace(html,@"(<style\b[^>]*>)([\s\S]*?)(</style>)",m=>m.Groups[1].Value+Order+"@layer site{"+m.Groups[2].Value+"}"+m.Groups[3].Value,RegexOptions.IgnoreCase);
        var end=html.IndexOf("</head>",StringComparison.OrdinalIgnoreCase);if(end<0)return html;
        var head=html[..end];var changes=new List<(int Start,int Length,string Value)>();var group=new List<Match>();var files=new List<string>();
        void Flush(){if(group.Count==0)return;var url="/ui.css?files="+Uri.EscapeDataString(string.Join(',',files))+"&v=css-20261012-1";changes.Add((group[0].Index,group[0].Length,"<link rel=\"stylesheet\" href=\""+WebUtility.HtmlEncode(url)+"\">"));foreach(var match in group.Skip(1))changes.Add((match.Index,match.Length,""));group.Clear();files.Clear();}
        foreach(Match match in Regex.Matches(head,@"<link\b[^>]*>|<style\b[^>]*>[\s\S]*?</style>",RegexOptions.IgnoreCase)){
            if(match.Value.StartsWith("<style",StringComparison.OrdinalIgnoreCase)){Flush();continue;}
            var rel=Regex.Match(match.Value,@"\brel\s*=\s*[""']([^""']+)[""']",RegexOptions.IgnoreCase).Groups[1].Value;
            if(!rel.Equals("stylesheet",StringComparison.OrdinalIgnoreCase))continue;
            var href=WebUtility.HtmlDecode(Regex.Match(match.Value,@"\bhref\s*=\s*[""']([^""']+)[""']",RegexOptions.IgnoreCase).Groups[1].Value);var path=href.Split('?')[0];
            if(Regex.IsMatch(path,@"^/[a-z0-9_-]+\.css$")){group.Add(match);files.Add(path[1..]);}else Flush();
        }
        Flush();foreach(var change in changes.OrderByDescending(x=>x.Start))head=head.Remove(change.Start,change.Length).Insert(change.Start,change.Value);return head+html[end..];
    }
}
