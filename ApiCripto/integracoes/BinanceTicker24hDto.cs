using System.Globalization;
using System.Text.Json.Serialization;

namespace ApiCripto.integracoes;

/// <summary>
/// Espelho do JSON de GET /api/v3/ticker/24hr da Binance.
///
/// Todos os campos numéricos chegam como STRING ("78705.99000000") — é assim
/// que a Binance devolve, para não perder precisão em linguagens que só têm
/// double. Por isso as propriedades são string e a conversão acontece aqui,
/// sempre com InvariantCulture (senão, em máquina configurada em pt-BR, o
/// ponto decimal seria interpretado como separador de milhar).
///
/// Esta classe é uma FRONTEIRA: ela existe para o formato da Binance não
/// vazar para dentro do domínio. Se a Binance mudar um nome de campo, só este
/// arquivo muda.
/// </summary>
public class BinanceTicker24hDto
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

    /// <summary>Instante do fechamento da janela de 24h, em milissegundos desde 1970 (Unix epoch).</summary>
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
        decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado)
            ? resultado
            : 0m;
}
