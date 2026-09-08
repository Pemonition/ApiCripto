using ApiCripto.dtos;
using ApiCripto.integracoes;
using ApiCripto.models;
using ApiCripto.repositories;

namespace ApiCripto.services;

/// <summary>
/// Desfechos possíveis de uma sincronização. Um enum em vez de bools soltos
/// porque cada caso vira um status HTTP diferente no controller, e um bool
/// não consegue expressar três respostas distintas sem virar adivinhação.
/// </summary>
public enum StatusSincronizacao
{
    /// <summary>Cotação buscada e gravada.</summary>
    Sucesso,

    /// <summary>O ativo informado não existe -> 404.</summary>
    AtivoNaoEncontrado,

    /// <summary>A corretora do ativo não tem integração automática -> 400 (erro do cliente).</summary>
    SemIntegracao,

    /// <summary>A corretora existe mas falhou/recusou a resposta -> 502 (erro de terceiro).</summary>
    FalhaNaCorretora
}

public interface ISincronizacaoService
{
    Task<(Cotacao? Cotacao, StatusSincronizacao Status, string? Erro)> SincronizarAtivoAsync(
        int ativoId, CancellationToken cancellationToken = default);

    Task<SincronizacaoResultadoDto> SincronizarTodosAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orquestra o mundo externo (Binance) com o mundo interno (banco).
///
/// Divisão de responsabilidade proposital:
///   BinanceClient       -> só sabe falar HTTP com a Binance;
///   SincronizacaoService-> decide O QUE sincronizar e traduz o DTO externo
///                          em uma Cotacao do nosso domínio;
///   CotacaoService      -> grava e mantém o espelho consistente.
/// Trocar a Binance por outra corretora = escrever outro cliente e escolher
/// aqui; o resto do sistema não muda.
/// </summary>
public class SincronizacaoService : ISincronizacaoService
{
    /// <summary>Corretoras com este CodigoIntegracao são atendidas pelo BinanceClient.</summary>
    public const string IntegracaoBinance = "BINANCE";

    private readonly IAtivoRepository _ativoRepository;
    private readonly ICotacaoService _cotacaoService;
    private readonly IBinanceClient _binance;
    private readonly ILogger<SincronizacaoService> _logger;

    public SincronizacaoService(
        IAtivoRepository ativoRepository,
        ICotacaoService cotacaoService,
        IBinanceClient binance,
        ILogger<SincronizacaoService> logger)
    {
        _ativoRepository = ativoRepository;
        _cotacaoService = cotacaoService;
        _binance = binance;
        _logger = logger;
    }

    public async Task<(Cotacao? Cotacao, StatusSincronizacao Status, string? Erro)> SincronizarAtivoAsync(
        int ativoId, CancellationToken cancellationToken = default)
    {
        var ativo = await _ativoRepository.ObterPorIdAsync(ativoId);
        if (ativo is null)
            return (null, StatusSincronizacao.AtivoNaoEncontrado, $"Ativo {ativoId} não encontrado.");

        if (!string.Equals(ativo.Corretora?.CodigoIntegracao, IntegracaoBinance,
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, StatusSincronizacao.SemIntegracao,
                $"A corretora '{ativo.Corretora?.Nome}' não tem integração automática. " +
                $"Use POST /api/ativo/{ativoId}/cotacao para lançar o preço manualmente.");
        }

        var (cotacao, erro) = await BuscarEGravarAsync(ativo, cancellationToken);

        return cotacao is not null
            ? (cotacao, StatusSincronizacao.Sucesso, null)
            : (null, StatusSincronizacao.FalhaNaCorretora, erro);
    }

    public async Task<SincronizacaoResultadoDto> SincronizarTodosAsync(
        CancellationToken cancellationToken = default)
    {
        var ativos = await _ativoRepository.ListarMonitoradosPorIntegracaoAsync(IntegracaoBinance);

        var resultado = new SincronizacaoResultadoDto
        {
            TotalConsiderados = ativos.Count
        };

        // Sequencial de propósito: a Binance limita requisições por IP (HTTP 429)
        // e o DbContext do EF Core NÃO é thread-safe — disparar tudo em paralelo
        // com Task.WhenAll quebraria os dois. Com dezenas de ativos, o custo é
        // aceitável; se um dia forem milhares, o caminho é uma fila/worker,
        // não paralelizar em cima do mesmo DbContext.
        foreach (var ativo in ativos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (cotacao, erro) = await BuscarEGravarAsync(ativo, cancellationToken);

            resultado.Itens.Add(new ItemSincronizacaoDto
            {
                AtivoId = ativo.Id,
                Simbolo = ativo.Simbolo,
                Sucesso = cotacao is not null,
                Preco = cotacao?.Preco,
                Erro = erro
            });

            // Falha de um ativo NÃO aborta o lote: cada símbolo é independente.
            if (cotacao is not null) resultado.Sincronizados++;
            else resultado.ComFalha++;
        }

        return resultado;
    }

    /// <summary>Busca na corretora, traduz para o domínio e grava.</summary>
    private async Task<(Cotacao? Cotacao, string? Erro)> BuscarEGravarAsync(
        Ativo ativo, CancellationToken cancellationToken)
    {
        var (ticker, erro) = await _binance.ObterTicker24hAsync(ativo.Simbolo, cancellationToken);

        if (ticker is null)
            return (null, erro ?? "Falha desconhecida ao consultar a corretora.");

        // TRADUÇÃO: DTO externo -> entidade do domínio.
        // Depois desta linha, nada mais no sistema sabe que a Binance existe.
        var cotacao = new Cotacao
        {
            Preco = ticker.PrecoDecimal,
            Variacao24h = ticker.VariacaoDecimal,
            PrecoMaximo24h = ticker.MaximaDecimal,
            PrecoMinimo24h = ticker.MinimaDecimal,
            Volume24h = ticker.VolumeDecimal,
            CapturadoEm = ticker.CapturadoEmUtc,
            Fonte = "Binance"
        };

        await _cotacaoService.RegistrarAsync(ativo, cotacao);

        _logger.LogInformation("Ativo {Simbolo} sincronizado: {Preco}", ativo.Simbolo, cotacao.Preco);
        return (cotacao, null);
    }
}
