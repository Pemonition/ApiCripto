using Carteira.Application.Dtos;
using Carteira.Application.Servicos;
using Carteira.Domain.Entidades;
using Carteira.Tests.Fakes;
using FluentAssertions;

namespace Carteira.Tests;

public class AtivoServiceTests
{
    private readonly AtivoRepositoryFake _ativos = new();
    private readonly CorretoraRepositoryFake _corretoras = new();
    private readonly UsuarioAtualFake _usuario = new();
    private readonly AtivoService _service;

    public AtivoServiceTests()
    {
        _corretoras.Itens.Add(new Corretora { Id = 1, Nome = "Binance", CodigoIntegracao = "BINANCE" });
        _service = new AtivoService(_ativos, _corretoras, _usuario);
    }

    private static AtivoDto Dto(string simbolo = "btcusdt", int corretoraId = 1) => new()
    {
        Simbolo = simbolo,
        MoedaBase = "btc",
        MoedaCotacao = "usdt",
        Tipo = TipoAtivo.Cripto,
        Monitorado = true,
        CorretoraId = corretoraId
    };

    /// <summary>
    /// Normalizar na escrita é o que faz a busca por símbolo ser
    /// case-insensitive sem depender do collation do banco. Se essa
    /// normalização sumir, o filtro ?simbolo=btc para de achar registros
    /// gravados como "btcusdt" — e só em PostgreSQL, não em SQLite.
    /// </summary>
    [Fact]
    public async Task CriarAsync_deve_normalizar_simbolo_e_moedas_para_maiusculas()
    {
        var (criado, erro) = await _service.CriarAsync(Dto("  btcusdt  "));

        erro.Should().BeNull();
        _ativos.Itens.Should().ContainSingle();
        _ativos.Itens[0].Simbolo.Should().Be("BTCUSDT");
        _ativos.Itens[0].MoedaBase.Should().Be("BTC");
        _ativos.Itens[0].MoedaCotacao.Should().Be("USDT");
    }

    [Fact]
    public async Task CriarAsync_deve_carimbar_o_usuario_atual()
    {
        await _service.CriarAsync(Dto());

        _ativos.Itens[0].UsuarioId.Should().Be(_usuario.Id,
            "sem o carimbo, o registro nasce invisível para o filtro global do DbContext");
    }

    [Fact]
    public async Task CriarAsync_deve_recusar_corretora_inexistente()
    {
        var (criado, erro) = await _service.CriarAsync(Dto(corretoraId: 99));

        criado.Should().BeNull();
        erro.Should().Contain("99");
        _ativos.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task CriarAsync_deve_recusar_simbolo_repetido_na_mesma_corretora()
    {
        await _service.CriarAsync(Dto("BTCUSDT"));

        var (criado, erro) = await _service.CriarAsync(Dto("btcusdt"));

        criado.Should().BeNull();
        erro.Should().Contain("já está cadastrado");
        _ativos.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task AtualizarAsync_nao_deve_tratar_o_proprio_registro_como_duplicata()
    {
        await _service.CriarAsync(Dto("BTCUSDT"));

        var (atualizado, erro) = await _service.AtualizarAsync(1, Dto("BTCUSDT"));

        atualizado.Should().BeTrue();
        erro.Should().BeNull();
    }

    /// <summary>
    /// Preço é dado de mercado: só a cotação escreve nele. Se o PUT pudesse
    /// alterar o espelho, o valor divergiria do histórico sem nenhum rastro.
    /// </summary>
    [Fact]
    public async Task AtualizarAsync_nao_deve_mexer_nos_campos_espelho()
    {
        await _service.CriarAsync(Dto("BTCUSDT"));

        var ativo = _ativos.Itens[0];
        ativo.UltimoPreco = 78_000m;
        ativo.AtualizadoEm = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await _service.AtualizarAsync(1, Dto("ETHUSDT"));

        ativo.Simbolo.Should().Be("ETHUSDT");
        ativo.UltimoPreco.Should().Be(78_000m);
        ativo.AtualizadoEm.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
