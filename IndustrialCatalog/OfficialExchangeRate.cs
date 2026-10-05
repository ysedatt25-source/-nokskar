using System.Globalization;
using System.Xml.Linq;

public sealed record ExchangeRateSnapshot(decimal Rate,string EffectiveDate,string Source);

public static class OfficialExchangeRate
{
    public static readonly Uri TcmbDailyRates = new("https://www.tcmb.gov.tr/kurlar/today.xml");
    public const string SourceName = "TCMB EUR Döviz Alış";

    public static async Task<ExchangeRateSnapshot> FetchEuroBuyingAsync(IHttpClientFactory clients,CancellationToken cancellationToken=default)
    {
        using var client=clients.CreateClient();
        client.Timeout=TimeSpan.FromSeconds(12);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("InokskarCatalog/1.0");
        var xml=await client.GetStringAsync(TcmbDailyRates,cancellationToken);
        var doc=XDocument.Parse(xml,LoadOptions.None);
        var eur=doc.Descendants("Currency").FirstOrDefault(x=>(string?)x.Attribute("CurrencyCode")=="EUR")
            ?? throw new InvalidDataException("TCMB EUR kuru bulunamadı.");
        var raw=eur.Element("ForexBuying")?.Value;
        if(!decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out var rate)||rate<=0)
            throw new InvalidDataException("TCMB EUR döviz alış kuru geçersiz.");

        var root=doc.Root;
        var rawDate=(string?)root?.Attribute("Tarih") ?? (string?)root?.Attribute("Date") ?? "";
        var effectiveDate="";
        if(DateTime.TryParseExact(rawDate,new[]{"dd.MM.yyyy","MM/dd/yyyy"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))
            effectiveDate=parsed.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);

        return new ExchangeRateSnapshot(decimal.Round(rate,4,MidpointRounding.AwayFromZero),effectiveDate,SourceName);
    }
}
