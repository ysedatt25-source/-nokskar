public sealed class BackupScheduler(Store store,IWebHostEnvironment env,IConfiguration config,ILogger<BackupScheduler> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(bool.TryParse(config["Backup:DisableAutomatic"],out var disabled)&&disabled)return;
        var dataPath=Path.GetFullPath(config["Storage:Path"]??"App_Data",env.ContentRootPath);
        var dir=Path.Combine(dataPath,"backups");Directory.CreateDirectory(dir);
        using var timer=new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                var latest=Directory.EnumerateFiles(dir,"inokskar-auto-*.zip").Select(x=>new FileInfo(x)).OrderByDescending(x=>x.LastWriteTimeUtc).FirstOrDefault();
                if(latest==null||DateTime.UtcNow-latest.LastWriteTimeUtc>TimeSpan.FromHours(23))
                {
                    var bytes=CatalogBackup.Create(store.Snapshot(),dataPath);var path=Path.Combine(dir,$"inokskar-auto-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");await File.WriteAllBytesAsync(path,bytes,stoppingToken);store.RecordAudit("Otomatik yedek oluşturuldu",Path.GetFileName(path));
                    foreach(var old in Directory.EnumerateFiles(dir,"inokskar-auto-*.zip").Select(x=>new FileInfo(x)).OrderByDescending(x=>x.LastWriteTimeUtc).Skip(14))try{old.Delete();}catch{}
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning(ex,"Otomatik yedek oluşturulamadı.");}
        }
        while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
