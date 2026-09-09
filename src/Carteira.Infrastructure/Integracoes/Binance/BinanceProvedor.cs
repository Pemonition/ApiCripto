using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carteira.Application.Integracoes;
using Microsoft.Extensions.Logging;

namespace Carteira.Infrastructure.Integracoes.Binance;

/// <summary>
/// Provedor de cotação da Binance.
///
/// Cliente HTTP tipado (registrado com AddHttpClient), nunca "new HttpClient()"
/// — instanciar manualmente esgota sockets sob carga e não enxerga mudança de
/// DNS.
///
/// Endpoint: /api/v3/ticker/24hr — público, sem chave de API.
/// </summary>
public class BinanceProvedor : IProvedorDeCotacao
{
    /// <summary>Casa com Corretora.CodigoIntegracao.</summary>
    public const string CodigoProvedor = "BINANCE";

    public string Codigo => CodigoProvedor;

    private readonly HttpClient _http;
    private readonly ILogger<BinanceProvedor> _logger;

    private static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BinanceProvedor(HttpClient http, ILogger<BinanceProvedor> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(TickerExterno? Ticker, string? Erro)> ObterTickerAsync(
        string simbolo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(simbolo))
            return (null, "Símbolo não informado.");

        var url = $"/api/v3/ticker/24hr?symbol={Uri.EscapeDataString(simbolo.ToUpperInvariant())}";

        try
        {
            using var resposta = await _http.GetAsync(url, ct);

            if (!resposta.IsSuccessStatusCode)
            {
                var corpo = await resposta.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Binance respondeu {Status} para {Simbolo}: {Corpo}",
                    (int)resposta.StatusCode, simbolo, corpo);

                return (null, TraduzirErro(resposta.StatusCode, simbolo));
            }

            var dto = await resposta.Content.ReadFromJsonAsync<BinanceTicker24hDto>(JsonOpcoes, ct);

            if (dto is null)
                return (null, "A Binance respondeu um corpo vazio ou inesperado.");

            // TRADUÇÃO: formato externo -> tipo neutro da aplicação.
            // Depois desta linha, nada mais no sistema sabe que a Binance existe.
            return (new TickerExterno(
                dto.Symbol,
                dto.PrecoDecimal,
                dto.VariacaoDecimal,
                dto.MaximaDecimal,
                dto.MinimaDecimal,
                dto.VolumeDecimal,
                dto.CapturadoEmUtc), null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout do HttpClient também chega como TaskCanceledException —
            // por isso o "when", que separa timeout de cancelamento real.
            _logger.LogWarning("Timeout ao consultar a Binance para {Simbolo}", simbolo);
            return (null, "Tempo esgotado ao falar com a Binance.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao consultar a Binance para {Simbolo}", simbolo);
            return (null, "Não foi possível alcançar a Binance (verifique a conexão/proxy).");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inesperado da Binance para {Simbolo}", simbolo);
            return (null, "A Binance devolveu um JSON em formato inesperado.");
        }
    }

    /// <summary>Traduz o status HTTP em uma mensagem acionável para quem usa a API.</summary>
    private static string TraduzirErro(HttpStatusCode status, string simbolo) => status switch
    {
        HttpStatusCode.BadRequest =>
            $"A Binance não reconhece o símbolo '{simbolo}'.",
        HttpStatusCode.UnavailableForLegalReasons =>
            "A Binance bloqueou a requisição por região (HTTP 451). " +
            "Troque Binance:BaseUrl para https://data-api.binance.vision.",
        HttpStatusCode.TooManyRequests =>
            "Limite de requisições da Binance atingido (HTTP 429). Tente de novo em alguns segundos.",
        _ => $"A Binance respondeu HTTP {(int)status}."
    };
}
