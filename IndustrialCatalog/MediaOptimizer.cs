using System.Diagnostics;

// Originals remain untouched. Derived files can be rebuilt and are published atomically.
public sealed class MediaOptimizer : BackgroundService
{
    readonly string root;
    readonly ILogger<MediaOptimizer> log;
    readonly HashSet<string> attempted=new(StringComparer.Ordinal);
    public MediaOptimizer(string dataPath,ILogger<MediaOptimizer> logger){root=dataPath;log=logger;}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000,stoppingToken);
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var uploads=Path.Combine(root,"uploads");
                if(Directory.Exists(uploads))
                foreach(var file in Directory.EnumerateFiles(uploads).OrderBy(p=>Path.GetExtension(p)==".mp4"?1:0))
                {
                    var name=Path.GetFileName(file);
                    if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[a-f0-9]{32}\.(png|jpg|webp|mp4)$")||File.GetLastWriteTimeUtc(file)>DateTime.UtcNow.AddSeconds(-5)||!attempted.Add(name))continue;
                    try{await OptimizeAsync(root,name,stoppingToken);log.LogInformation("Media variants ready: {File}",name);}
                    catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
                    catch(Exception ex){log.LogWarning(ex,"Media optimization failed for {File}; original remains available",name);}
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception ex){log.LogWarning(ex,"Media optimization scan failed");}
            await Task.Delay(15000,stoppingToken);
        }
    }
    public static string DerivedPath(string root,string id,string variant)=>Path.Combine(root,"media-cache-v1",id+"."+variant+(variant=="stream"?".mp4":".webp"));
    public static async Task OptimizeAsync(string root,string id,CancellationToken token)
    {
        var input=Path.Combine(root,"uploads",id);
        Directory.CreateDirectory(Path.Combine(root,"media-cache-v1"));
        var video=Path.GetExtension(id)==".mp4";
        foreach(var variant in video?new[]{"stream"}:new[]{"thumb","card","detail"})
        {
            var output=DerivedPath(root,id,variant);if(File.Exists(output))continue;
            var temporary=output+".tmp";
            var size=variant=="thumb"?160:variant=="card"?640:1280;
            var options=video
                ?new[]{"-map","0:v:0","-map","0:a:0?","-vf","scale=w='min(1920,iw)':h='min(1080,ih)':force_original_aspect_ratio=decrease:force_divisible_by=2","-c:v","libx264","-preset","veryfast","-crf","24","-maxrate","3M","-bufsize","6M","-pix_fmt","yuv420p","-threads","1","-c:a","aac","-b:a","128k","-movflags","+faststart","-f","mp4"}
                :new[]{"-vf",$"scale=w='min({size},iw)':h='min({size},ih)':force_original_aspect_ratio=decrease","-frames:v","1","-c:v","libwebp","-quality",variant=="detail"?"88":"82","-threads","1","-f","webp"};
            try
            {
                var start=new ProcessStartInfo("ffmpeg"){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true,CreateNoWindow=true};
                foreach(var arg in new[]{"-nostdin","-hide_banner","-loglevel","error","-filter_threads","1","-y","-i",input}.Concat(options).Append(temporary))start.ArgumentList.Add(arg);
                using var process=Process.Start(start)??throw new IOException("ffmpeg could not start");
                var errors=process.StandardError.ReadToEndAsync();var stdout=process.StandardOutput.ReadToEndAsync();
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(video?TimeSpan.FromMinutes(10):TimeSpan.FromSeconds(30));
                try{await process.WaitForExitAsync(deadline.Token);}
                catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync(CancellationToken.None);throw;}
                await stdout;var error=await errors;
                if(process.ExitCode!=0||!File.Exists(temporary)||new FileInfo(temporary).Length==0)throw new IOException("Media conversion failed: "+error);
                File.Move(temporary,output,true);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
