using Carteira.Application.Servicos;
using Carteira.Domain.Entidades;
using Carteira.Tests.Fakes;
using FluentAssertions;

namespace Carteira.Tests;

/// <summary>
/// Testes da regra mais frágil do sistema: manter os campos espelho do Ativo
/// em sincronia com o histórico de cotações.
///
/// É frágil porque a inconsistência não gera erro — gera resposta errada. Um
/// UltimoPreco desatualizado faz o filtro precoMin/precoMax devolver a lista
/// errada silenciosamente, e ninguém percebe até conferir na mão.
/// </summary>
public class CotacaoServiceTests
{
    private readonly AtivoRepositoryFake _ativos = new();
    private readonly CotacaoRepositoryFake _cotacoes = new();
    private readonly CotacaoService _service;

    public CotacaoServiceTests()
    {
        _service = new CotacaoService(_cotacoes, _ativos);
    }

    private Ativo NovoAtivo(decimal? ultimoPreco = null, DateTime? atualizadoEm = null) => new()
    {
        Id = 1,
        Simbolo = "BTCUSDT",
        MoedaBase = "BTC",
        MoedaCotacao = "USDT",
        CorretoraId = 1,
        UltimoPreco = ultimoPreco,
        AtualizadoEm = atualizadoEm
    };

    [Fact]
    public async Task RegistrarAsync_deve_promover_a_cotacao_a_ultimo_preco_quando_o_ativo_nunca_teve_preco()
    {
        var ativo = NovoAtivo();
        var cotacao = new Cotacao { Preco = 78_000m, Variacao24h = 1.5m, CapturadoEm = DateTime.UtcNow };

        await _service.RegistrarAsync(ativo, cotacao);

        ativo.UltimoPreco.Should().Be(78_000m);
        ativo.Variacao24h.Should().Be(1.5m);
        ativo.AtualizadoEm.Should().Be(cotacao.CapturadoEm);
        _cotacoes.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task RegistrarAsync_deve_atualizar_o_espelho_quando_a_cotacao_e_mais_recente()
    {
        var ontem = DateTime.UtcNow.AddDays(-1);
        var ativo = NovoAtivo(ultimoPreco: 70_000m, atualizadoEm: ontem);

        await _service.RegistrarAsync(ativo, new Cotacao { Preco = 78_000m, CapturadoEm = DateTime.UtcNow });

        ativo.UltimoPreco.Should().Be(78_000m);
    }

    /// <summary>
    /// A regra sutil: importar histórico ANTIGO não pode fazer o preço atual
    /// voltar no tempo. Sem a comparação de data em RegistrarAsync, um backfill
    /// de candles velhos reescreveria o preço de hoje com o de meses atrás.
    /// </summary>
    [Fact]
    public async Task RegistrarAsync_nao_deve_deixar_uma_cotacao_antiga_retroceder_o_preco_atual()
    {
        var agora = DateTime.UtcNow;
        var ativo = NovoAtivo(ultimoPreco: 78_000m, atualizadoEm: agora);

        var antiga = new Cotacao { Preco = 30_000m, CapturadoEm = agora.AddYears(-2) };
        await _service.RegistrarAsync(ativo, antiga);

        ativo.UltimoPreco.Should().Be(78_000m, "a cotação antiga entra no histórico mas não vira o preço atual");
        ativo.AtualizadoEm.Should().Be(agora);
        _cotacoes.Itens.Should().ContainSingle("a cotação antiga ainda precisa ser gravada no histórico");
    }

    [Fact]
    public async Task CriarAsync_deve_recusar_quando_o_preco_minimo_e_maior_que_o_maximo()
    {
        _ativos.Itens.Add(NovoAtivo());

        var (criada, erro) = await _service.CriarAsync(1, new Application.Dtos.CotacaoDto
        {
            Preco = 10m,
            PrecoMinimo24h = 50m,
            PrecoMaximo24h = 5m
        });

        criada.Should().BeNull();
        erro.Should().NotBeNull();
        _cotacoes.Itens.Should().BeEmpty("nada pode ser gravado quando a validação falha");
    }

    [Fact]
    public async Task ListarPorAtivoAsync_deve_devolver_null_para_ativo_inexistente_e_lista_vazia_para_ativo_sem_historico()
    {
        (await _service.ListarPorAtivoAsync(999, null, null, 10))
            .Should().BeNull("ativo inexistente vira 404");

        _ativos.Itens.Add(NovoAtivo());

        (await _service.ListarPorAtivoAsync(1, null, null, 10))
            .Should().NotBeNull().And.BeEmpty("ativo sem cotação vira 200 com lista vazia");
    }
}
