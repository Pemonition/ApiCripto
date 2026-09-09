namespace Carteira.Application.Integracoes;

/// <summary>
/// Preço vindo de uma fonte externa, já traduzido para termos nossos.
///
/// É um record e não a classe do JSON da Binance de propósito: este tipo é a
/// FRONTEIRA. Nada acima da infraestrutura sabe que a Binance devolve números
/// como string, que o campo se chama "lastPrice" ou que o horário vem em
/// milissegundos desde 1970.
/// </summary>
public record TickerExterno(
    string Simbolo,
    decimal Preco,
    decimal Variacao24h,
    decimal Maxima24h,
    decimal Minima24h,
    decimal Volume24h,
    DateTime CapturadoEmUtc);

/// <summary>
/// Uma fonte de preços. Cada corretora integrada tem a sua implementação.
///
/// Não lança exceção: devolve (Ticker, Erro). Terceiro fora do ar é resultado
/// ESPERADO de uma integração, não um bug — e exceção para fluxo esperado
/// esconde o caso de erro em vez de obrigar quem chama a tratá-lo.
/// </summary>
public interface IProvedorDeCotacao
{
    /// <summary>
    /// Casa com Corretora.CodigoIntegracao (ex.: "BINANCE").
    ///
    /// É este par — a propriedade aqui e o campo na entidade — que faz a
    /// escolha do provedor virar uma consulta em dicionário em vez de um
    /// switch que cresce a cada corretora nova.
    /// </summary>
    string Codigo { get; }

    Task<(TickerExterno? Ticker, string? Erro)> ObterTickerAsync(
        string simbolo, CancellationToken ct = default);
}
