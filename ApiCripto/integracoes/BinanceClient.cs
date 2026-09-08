using System.Net;
using System.Text.Json;

namespace ApiCripto.integracoes;

public interface IBinanceClient
{
    /// <summary>
    /// Busca o resumo de 24h de um símbolo. Devolve (dado, erro):
    /// dado preenchido em caso de sucesso, erro preenchido em caso de falha.
    /// NUNCA lança exceção para quem chama — falha de rede é resultado
    /// esperado de uma integração externa, não um bug.
    /// </summary>
    Task<(BinanceTicker24hDto? Ticker, string? Erro)> ObterTicker24hAsync(
        string simbolo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cliente HTTP tipado da Binance (registrado com AddHttpClient no Program.cs,
/// que cuida do pool de conexões — nunca instancie HttpClient com "new",
/// isso causa esgotamento de sockets).
///
/// Endpoint usado: /api/v3/ticker/24hr — público, sem chave de API.
/// </summary>
public class BinanceClient : IBinanceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BinanceClient> _logger;

    private static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BinanceClient(HttpClient http, ILogger<BinanceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(BinanceTicker24hDto? Ticker, string? Erro)> ObterTicker24hAsync(
        string simbolo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(simbolo))
            return (null, "Símbolo não informado.");

        var url = $"/api/v3/ticker/24hr?symbol={Uri.EscapeDataString(simbolo.ToUpperInvariant())}";

        try
        {
            using var resposta = await _http.GetAsync(url, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Binance respondeu {Status} para {Simbolo}: {Corpo}",
                    (int)resposta.StatusCode, simbolo, corpo);

                // Traduz os erros mais comuns para uma mensagem que o usuário da API entende.
                var mensagem = resposta.StatusCode switch
                {
                    HttpStatusCode.BadRequest =>
                        $"A Binance não reconhece o símbolo '{simbolo}'.",
                    HttpStatusCode.UnavailableForLegalReasons =>
                        "A Binance bloqueou a requisição por região (HTTP 451). " +
                        "Troque Binance:BaseUrl para https://data-api.binance.vision no appsettings.json.",
                    HttpStatusCode.TooManyRequests =>
                        "Limite de requisições da Binance atingido (HTTP 429). Tente de novo em alguns segundos.",
                    _ => $"A Binance respondeu HTTP {(int)resposta.StatusCode}."
                };

                return (null, mensagem);
            }

            var ticker = await resposta.Content.ReadFromJsonAsync<BinanceTicker24hDto>(
                JsonOpcoes, cancellationToken);

            return ticker is null
                ? (null, "A Binance respondeu um corpo vazio ou inesperado.")
                : (ticker, null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout do HttpClient também chega como TaskCanceledException.
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
}
