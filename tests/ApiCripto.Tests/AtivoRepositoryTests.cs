using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.repositories;

namespace ApiCripto.Tests;

public class AtivoRepositoryTests
{
    [Fact]
    public async Task Filtro_de_faixa_de_preco_compara_numericamente()
    {
        // Sem o conversor decimal->double do AppDbContext, o SQLite compararia
        // texto e "604.1" ficaria fora de 100..5000 ("6" > "5").
        using var banco = new BancoDeTeste();
        var repo = new AtivoRepository(banco.Context);

        var ativos = await repo.ListarAsync(new AtivoFiltro { PrecoMin = 100, PrecoMax = 5000 });

        Assert.Equal(new[] { "BNBUSDT", "ETHUSDT", "SOLUSDT" }, ativos.Select(a => a.Simbolo));
    }

    [Fact]
    public async Task Filtros_combinados_e_busca_parcial_sem_diferenciar_maiusculas()
    {
        using var banco = new BancoDeTeste();
        var repo = new AtivoRepository(banco.Context);

        var ativos = await repo.ListarAsync(new AtivoFiltro { Simbolo = "btc", Monitorado = true });

        var ativo = Assert.Single(ativos);
        Assert.Equal("BTCUSDT", ativo.Simbolo);
    }

    [Fact]
    public async Task Ordena_por_preco_decrescente()
    {
        using var banco = new BancoDeTeste();
        var repo = new AtivoRepository(banco.Context);

        var ativos = await repo.ListarAsync(new AtivoFiltro { Tipo = TipoAtivo.Cripto, OrdenarPor = "preco", Desc = true });

        Assert.Equal("BTCBRL", ativos.First().Simbolo);
    }
}
