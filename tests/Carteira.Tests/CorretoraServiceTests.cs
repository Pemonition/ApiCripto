using Carteira.Application.Dtos;
using Carteira.Application.Servicos;
using Carteira.Tests.Fakes;
using FluentAssertions;

namespace Carteira.Tests;

public class CorretoraServiceTests
{
    private readonly CorretoraRepositoryFake _repo = new();
    private readonly UsuarioAtualFake _usuario = new();
    private readonly CorretoraService _service;

    public CorretoraServiceTests()
    {
        _service = new CorretoraService(_repo, _usuario);
    }

    [Fact]
    public async Task CriarAsync_deve_normalizar_o_codigo_de_integracao_para_maiusculas()
    {
        // O código é a chave que casa com IProvedorDeCotacao.Codigo. Gravado
        // em minúsculas, a busca no dicionário de provedores não acha nada e o
        // ativo simplesmente nunca sincroniza — sem erro, sem log, sem pista.
        var criada = await _service.CriarAsync(new CorretoraDto { Nome = "Binance", CodigoIntegracao = " binance " });

        criada.CodigoIntegracao.Should().Be("BINANCE");
        criada.TemIntegracaoAutomatica.Should().BeTrue();
    }

    [Fact]
    public async Task CriarAsync_deve_tratar_codigo_em_branco_como_ausencia_de_integracao()
    {
        var criada = await _service.CriarAsync(new CorretoraDto { Nome = "Rico", CodigoIntegracao = "   " });

        criada.CodigoIntegracao.Should().BeNull();
        criada.TemIntegracaoAutomatica.Should().BeFalse();
    }

    /// <summary>
    /// Os três desfechos de remoção precisam ser distinguíveis: é isso que
    /// permite o controller responder 204, 404 e 400 corretamente.
    /// </summary>
    [Fact]
    public async Task RemoverAsync_deve_diferenciar_inexistente_de_bloqueado_por_regra()
    {
        var (removido, erro) = await _service.RemoverAsync(999);
        removido.Should().BeFalse();
        erro.Should().BeNull("erro nulo significa 'não existe' e vira 404");

        await _service.CriarAsync(new CorretoraDto { Nome = "Binance" });
        _repo.PossuiAtivos = _ => true;

        (removido, erro) = await _service.RemoverAsync(1);
        removido.Should().BeFalse();
        erro.Should().NotBeNull("erro preenchido significa 'existe mas a regra proibiu' e vira 400");
        _repo.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoverAsync_deve_remover_corretora_sem_ativos()
    {
        await _service.CriarAsync(new CorretoraDto { Nome = "Kraken" });

        var (removido, erro) = await _service.RemoverAsync(1);

        removido.Should().BeTrue();
        erro.Should().BeNull();
        _repo.Itens.Should().BeEmpty();
    }
}
