using ApiCripto.integracoes;
namespace ApiCripto.Tests;

// Apenas a fronteira externa é substituída; JWT, autorização, EF e SQLite são reais.
public class BinanceFake : IBinanceClient
{
    public Task<(BinanceTicker24hDto? Ticker, string? Erro)> ObterTicker24hAsync(
        string simbolo, CancellationToken cancellationToken = default) =>
        Task.FromResult<(BinanceTicker24hDto?, string?)>((new BinanceTicker24hDto
        {
            Symbol = simbolo, LastPrice = "100", HighPrice = "110", LowPrice = "90",
            QuoteVolume = "1000", CloseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }, null));
}
