using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Application.Integracoes;
using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;
using Microsoft.Extensions.Logging;

namespace Carteira.Application.Servicos;

/// <summary>
/// Desfechos possíveis de uma sincronização. Um enum em vez de bools soltos
/// porque cada caso vira um status HTTP diferente, e um bool não expressa
/// três respostas distintas sem virar adivinhação no controller.
/// </summary>
public enum StatusSincronizacao
{
    Sucesso,

    /// <summary>Ativo não existe -> 404.</summary>
    AtivoNaoEncontrado,

    /// <summary>Corretora sem provedor de cotação -> 400 (erro de quem chamou).</summary>
    SemIntegracao,

    /// <summary>Provedor existe mas falhou -> 502 (erro de terceiro).</summary>
    FalhaNaCorretora
}

public interface ISincronizacaoService
{
    Task<(CotacaoResposta? Cotacao, StatusSincronizacao Status, string? Erro)> SincronizarAtivoAsync(
        int ativoId, CancellationToken ct = default);

    Task<SincronizacaoResultadoDto> SincronizarTodosAsync(
        string codigoIntegracao, CancellationToken ct = default);
}

/// <summary>
/// Orquestra o mundo externo (corretoras) com o mundo interno (banco).
///
/// Divisão de responsabilidade proposital:
///   IProvedorDeCotacao   -> só sabe falar com UMA corretora;
///   SincronizacaoService -> decide O QUE sincronizar e com QUAL provedor;
///   CotacaoService       -> grava e mantém o espelho consistente.
/// </summary>
public class SincronizacaoService : ISincronizacaoService
{
    private readonly IAtivoRepository _ativoRepository;
    private readonly ICotacaoService _cotacaoService;
    private readonly IReadOnlyDictionary<string, IProvedorDeCotacao> _provedores;
    private readonly ILogger<SincronizacaoService> _logger;

    /// <summary>
    /// Recebe TODOS os provedores registrados no container e os indexa pelo
    /// código. É aqui que a extensibilidade deixa de ser promessa: adicionar
    /// uma corretora nova é escrever uma classe e registrá-la no Program.cs —
    /// este arquivo não muda uma linha.
    ///
    /// Compare com a alternativa: um switch (corretora.Codigo) que cresce a
    /// cada integração e obriga a mexer no núcleo do sistema toda vez.
    /// </summary>
    public SincronizacaoService(
        IAtivoRepository ativoRepository,
        ICotacaoService cotacaoService,
        IEnumerable<IProvedorDeCotacao> provedores,
        ILogger<SincronizacaoService> logger)
    {
        _ativoRepository = ativoRepository;
        _cotacaoService = cotacaoService;
        _logger = logger;
        _provedores = provedores.ToDictionary(
            p => p.Codigo, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(CotacaoResposta? Cotacao, StatusSincronizacao Status, string? Erro)> SincronizarAtivoAsync(
        int ativoId, CancellationToken ct = default)
    {
        var ativo = await _ativoRepository.ObterPorIdAsync(ativoId, ct);
        if (ativo is null)
            return (null, StatusSincronizacao.AtivoNaoEncontrado, $"Ativo {ativoId} não encontrado.");

        var codigo = ativo.Corretora?.CodigoIntegracao;

        if (string.IsNullOrWhiteSpace(codigo) || !_provedores.TryGetValue(codigo, out var provedor))
        {
            return (null, StatusSincronizacao.SemIntegracao,
                $"A corretora '{ativo.Corretora?.Nome}' não tem integração automática. " +
                $"Use POST /api/ativo/{ativoId}/cotacao para lançar o preço manualmente.");
        }

        var (cotacao, erro) = await BuscarEGravarAsync(provedor, ativo, ct);

        return cotacao is not null
            ? (cotacao.ParaResposta(), StatusSincronizacao.Sucesso, null)
            : (null, StatusSincronizacao.FalhaNaCorretora, erro);
    }

    public async Task<SincronizacaoResultadoDto> SincronizarTodosAsync(
        string codigoIntegracao, CancellationToken ct = default)
    {
        var resultado = new SincronizacaoResultadoDto();

        if (!_provedores.TryGetValue(codigoIntegracao, out var provedor))
            return resultado;

        var ativos = await _ativoRepository.ListarMonitoradosPorIntegracaoAsync(codigoIntegracao, ct);
        resultado.TotalConsiderados = ativos.Count;

        // Sequencial de propósito: as corretoras limitam requisições por IP
        // (HTTP 429) e o DbContext do EF Core NÃO é thread-safe. Task.WhenAll
        // aqui quebraria os dois de uma vez — e a falha do segundo só aparece
        // sob carga, nunca no teste com cinco ativos.
        //
        // Com milhares de ativos o caminho é uma fila com worker, não
        // paralelizar em cima do mesmo contexto.
        foreach (var ativo in ativos)
        {
            ct.ThrowIfCancellationRequested();

            var (cotacao, erro) = await BuscarEGravarAsync(provedor, ativo, ct);

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

    private async Task<(Cotacao? Cotacao, string? Erro)> BuscarEGravarAsync(
        IProvedorDeCotacao provedor, Ativo ativo, CancellationToken ct)
    {
        var (ticker, erro) = await provedor.ObterTickerAsync(ativo.Simbolo, ct);

        if (ticker is null)
            return (null, erro ?? "Falha desconhecida ao consultar a corretora.");

        var cotacao = new Cotacao
        {
            Preco = ticker.Preco,
            Variacao24h = ticker.Variacao24h,
            PrecoMaximo24h = ticker.Maxima24h,
            PrecoMinimo24h = ticker.Minima24h,
            Volume24h = ticker.Volume24h,
            CapturadoEm = ticker.CapturadoEmUtc,
            Fonte = provedor.Codigo
        };

        await _cotacaoService.RegistrarAsync(ativo, cotacao, ct);

        _logger.LogInformation("Ativo {Simbolo} sincronizado via {Provedor}: {Preco}",
            ativo.Simbolo, provedor.Codigo, cotacao.Preco);

        return (cotacao, null);
    }
}
