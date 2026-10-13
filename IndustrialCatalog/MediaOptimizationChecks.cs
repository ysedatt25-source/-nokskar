using System.Diagnostics;
using System.Text.Json;
public static class MediaOptimizationChecks
{
    static async Task<string> Run(params string[] args)
    {
        var info=new ProcessStartInfo(args[0]){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true};
        foreach(var arg in args.Skip(1))info.ArgumentList.Add(arg);
        using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();if(p.ExitCode!=0)throw new Exception(await error);return await output;
    }
    public static async Task Check()
    {
        var root=Path.Combine(Path.GetTempPath(),"inokskar-media-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"uploads"));
        try
        {
            var image=new string('a',32)+".png";var video=new string('b',32)+".mp4";
            var photo=Path.Combine(root,"uploads",image);var movie=Path.Combine(root,"uploads",video);
            await Run("ffmpeg","-nostdin","-y","-f","lavfi","-i","color=c=blue:s=400x300","-frames:v","1","-threads","1",photo);
            await Run("ffmpeg","-nostdin","-y","-f","lavfi","-i","testsrc2=s=320x480:r=15","-t","1","-c:v","mpeg4","-threads","1",movie);
            var originalPhoto=await File.ReadAllBytesAsync(photo);var originalMovie=await File.ReadAllBytesAsync(movie);
            await MediaOptimizer.OptimizeAsync(root,image,CancellationToken.None);await MediaOptimizer.OptimizeAsync(root,video,CancellationToken.None);
            foreach(var variant in new[]{"thumb","card","detail"})
            {
                var file=MediaOptimizer.DerivedPath(root,image,variant);
                using var probe=JsonDocument.Parse(await Run("ffprobe","-v","error","-show_streams","-of","json",file));
                var stream=probe.RootElement.GetProperty("streams")[0];var max=variant=="thumb"?160:variant=="card"?640:1280;
                if(stream.GetProperty("codec_name").GetString()!="webp"||stream.GetProperty("width").GetInt32()>max||stream.GetProperty("height").GetInt32()>max)throw new Exception("Invalid image variant");
            }
            var poster=MediaOptimizer.DerivedPath(root,video,"poster");
            using(var probe=JsonDocument.Parse(await Run("ffprobe","-v","error","-show_streams","-of","json",poster)))
                if(probe.RootElement.GetProperty("streams")[0].GetProperty("codec_name").GetString()!="webp")throw new Exception("Video poster was not extracted");
            var optimized=MediaOptimizer.DerivedPath(root,video,"stream");
            using(var probe=JsonDocument.Parse(await Run("ffprobe","-v","error","-show_streams","-of","json",optimized)))
                if(probe.RootElement.GetProperty("streams")[0].GetProperty("codec_name").GetString()!="h264")throw new Exception("Video is not browser-compatible H264");
            var bytes=await File.ReadAllBytesAsync(optimized);var moov=-1;var mdat=-1;
            for(var offset=0;offset+8<=bytes.Length;){var size=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));var type=System.Text.Encoding.ASCII.GetString(bytes,offset+4,4);if(type=="moov")moov=offset;if(type=="mdat")mdat=offset;if(size<8||size>bytes.Length-offset)break;offset+=(int)size;}
            if(moov<0||mdat<0||moov>mdat)throw new Exception("Video faststart metadata is not at the beginning");
            if(!originalPhoto.SequenceEqual(await File.ReadAllBytesAsync(photo))||!originalMovie.SequenceEqual(await File.ReadAllBytesAsync(movie)))throw new Exception("Original media changed");
            await MediaOptimizer.OptimizeAsync(root,image,CancellationToken.None); // Existing outputs are reusable.
            Console.WriteLine("Media optimization checks: PASS (WebP dimensions, video poster, H264, faststart, original preservation, repeat processing).");
        }
        finally{Directory.Delete(root,true);}
    }
}
