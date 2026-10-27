using ApiCripto.dtos;
using ApiCripto.integracoes;
using ApiCripto.models;
using ApiCripto.repositories;
using ApiCripto.services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiCripto.Tests;

public class ServicesTests
{
    /// <summary>Binance de mentira: o teste decide o que ela responde, sem internet.</summary>
    private sealed class BinanceFake : IBinanceClient
    {
        public BinanceTicker24hDto? Ticker { get; init; }
        public string? Erro { get; init; }

        public Task<(BinanceTicker24hDto? Ticker, string? Erro)> ObterTicker24hAsync(
            string simbolo, CancellationToken cancellationToken = default) =>
            Task.FromResult((Ticker, Erro));
    }

    private static SincronizacaoService CriarSincronizacao(BancoDeTeste banco, IBinanceClient binance)
    {
        var ativos = new AtivoRepository(banco.Context);
        var cotacoes = new CotacaoService(new CotacaoRepository(banco.Context), ativos);
        return new SincronizacaoService(ativos, cotacoes, binance, NullLogger<SincronizacaoService>.Instance);
    }

    private static Ativo BuscarAtivo(BancoDeTeste banco, string simbolo) =>
        banco.Context.Ativos.Single(a => a.Simbolo == simbolo);

    [Fact]
    public async Task Cotacao_antiga_nao_faz_o_ultimo_preco_voltar_no_tempo()
    {
        using var banco = new BancoDeTeste();
        var ativos = new AtivoRepository(banco.Context);
        var service = new CotacaoService(new CotacaoRepository(banco.Context), ativos);
        var btc = BuscarAtivo(banco, "BTCUSDT");
        var precoAtual = btc.UltimoPreco;

        await service.CriarAsync(btc.Id, new CotacaoDto
        {
            Preco = 1m,
            PrecoMaximo24h = 2m,
            PrecoMinimo24h = 1m,
            CapturadoEm = DateTime.UtcNow.AddDays(-30)
        });

        Assert.Equal(precoAtual, (await ativos.ObterPorIdAsync(btc.Id))!.UltimoPreco);
    }

    [Fact]
    public async Task Nao_remove_corretora_com_ativos()
    {
        using var banco = new BancoDeTeste();
        var service = new CorretoraService(new CorretoraRepository(banco.Context));
        var binance = banco.Context.Corretoras.Single(c => c.Nome == "Binance");

        var (removido, erro) = await service.RemoverAsync(binance.Id);

        Assert.False(removido);
        Assert.NotNull(erro);
    }

    [Fact]
    public async Task Sincronizacao_grava_cotacao_e_atualiza_o_espelho_do_ativo()
    {
        using var banco = new BancoDeTeste();
        var ticker = new BinanceTicker24hDto
        {
            Symbol = "BTCUSDT",
            LastPrice = "99999.50000000",
            PriceChangePercent = "1.250",
            HighPrice = "100500.00",
            LowPrice = "98000.00",
            QuoteVolume = "123456.78",
            CloseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        var service = CriarSincronizacao(banco, new BinanceFake { Ticker = ticker });
        var btc = BuscarAtivo(banco, "BTCUSDT");

        var (cotacao, status, _) = await service.SincronizarAtivoAsync(btc.Id);

        Assert.Equal(StatusSincronizacao.Sucesso, status);
        Assert.Equal("Binance", cotacao!.Fonte);
        Assert.Equal(99999.5m, BuscarAtivo(banco, "BTCUSDT").UltimoPreco);
    }

    [Fact]
    public async Task Sincronizacao_de_corretora_sem_integracao_devolve_SemIntegracao()
    {
        using var banco = new BancoDeTeste();
        var service = CriarSincronizacao(banco, new BinanceFake());

        var (_, status, _) = await service.SincronizarAtivoAsync(BuscarAtivo(banco, "BTCBRL").Id);

        Assert.Equal(StatusSincronizacao.SemIntegracao, status);
    }

    [Fact]
    public async Task Falha_na_Binance_nao_aborta_o_lote()
    {
        using var banco = new BancoDeTeste();
        var service = CriarSincronizacao(banco, new BinanceFake { Erro = "HTTP 429" });

        var resultado = await service.SincronizarTodosAsync();

        Assert.Equal(5, resultado.TotalConsiderados);
        Assert.Equal(5, resultado.ComFalha);
        Assert.All(resultado.Itens, i => Assert.Equal("HTTP 429", i.Erro));
    }
}
