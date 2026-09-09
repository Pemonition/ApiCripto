using System.Globalization;
using System.Text.Json.Serialization;

namespace Carteira.Infrastructure.Integracoes.Binance;

/// <summary>
/// Espelho do JSON de GET /api/v3/ticker/24hr da Binance.
///
/// Esta classe é INTERNA à infraestrutura de propósito: o formato da Binance
/// não pode vazar para a aplicação nem para o domínio. Se a Binance renomear
/// um campo amanhã, só este arquivo muda.
///
/// Os números chegam como STRING ("78705.99000000") — é assim que a Binance
/// devolve, para não perder precisão em linguagens que só têm double. A
/// conversão acontece aqui e SEMPRE com InvariantCulture: numa máquina
/// configurada em pt-BR, o parse padrão leria o ponto como separador de
/// milhar e transformaria 78.705,99 em 7.870.599.
/// </summary>
internal class BinanceTicker24hDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; set; } = "0";

    [JsonPropertyName("priceChangePercent")]
    public string PriceChangePercent { get; set; } = "0";

    [JsonPropertyName("highPrice")]
    public string HighPrice { get; set; } = "0";

    [JsonPropertyName("lowPrice")]
    public string LowPrice { get; set; } = "0";

    [JsonPropertyName("quoteVolume")]
    public string QuoteVolume { get; set; } = "0";

    /// <summary>Fechamento da janela de 24h, em milissegundos desde 1970 (Unix epoch).</summary>
    [JsonPropertyName("closeTime")]
    public long CloseTime { get; set; }

    public decimal PrecoDecimal => ParaDecimal(LastPrice);
    public decimal VariacaoDecimal => ParaDecimal(PriceChangePercent);
    public decimal MaximaDecimal => ParaDecimal(HighPrice);
    public decimal MinimaDecimal => ParaDecimal(LowPrice);
    public decimal VolumeDecimal => ParaDecimal(QuoteVolume);

    public DateTime CapturadoEmUtc => CloseTime > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(CloseTime).UtcDateTime
        : DateTime.UtcNow;

    private static decimal ParaDecimal(string valor) =>
        decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0m;
}
