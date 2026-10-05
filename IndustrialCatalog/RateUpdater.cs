public sealed class RateUpdater(Store store,IHttpClientFactory clients,IConfiguration config,ILogger<RateUpdater> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(config["ExchangeRate:DisableAutoUpdate"],out var disabled)&&disabled) return;
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                var current=store.Snapshot();
                var settings=current["data"]!["settings"]!;
                var automatic=settings["autoRate"]?.GetValue<bool>()==true;
                var stale=!DateTimeOffset.TryParse(settings["lastRate"]?.ToString(),out var last)||DateTimeOffset.UtcNow-last>TimeSpan.FromMinutes(15);
                if(automatic&&stale)
                {
                    var snapshot=await OfficialExchangeRate.FetchEuroBuyingAsync(clients,stoppingToken);
                    store.UpdateExchangeRate(snapshot,DateTimeOffset.UtcNow);
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception e){logger.LogWarning(e,"Resmi TCMB EUR döviz alış kuru güncellenemedi; son geçerli kur ve yayınlanan TL fiyatları korundu.");}
        }
        while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
