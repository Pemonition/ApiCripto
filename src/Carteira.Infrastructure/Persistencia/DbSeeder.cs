using Carteira.Domain.Entidades;
using Carteira.Infrastructure.Identidade;
using Microsoft.EntityFrameworkCore;

namespace Carteira.Infrastructure.Persistencia;

/// <summary>
/// Popula o banco na primeira execução. Dois propósitos:
/// 1) quem clonar o projeto vê a API funcionando sem cadastrar nada;
/// 2) a API continua demonstrável SEM internet — os filtros e o histórico já
///    têm dados mesmo com a corretora inacessível.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: o seed roda fora do contexto de uma requisição.
        // Sem isso a checagem "já tem dado?" enxergaria só o que pertence ao
        // usuário atual e poderia repopular a base a cada subida.
        if (await context.Corretoras.IgnoreQueryFilters().AnyAsync(ct)) return;

        var usuario = UsuarioAtualFixo.UsuarioPadrao;

        var binance = new Corretora
        {
            Nome = "Binance",
            Site = "https://www.binance.com",
            PaisSede = "Malta",
            CodigoIntegracao = "BINANCE", // habilita a coleta automática
            UsuarioId = usuario
        };

        var mercadoBitcoin = new Corretora
        {
            Nome = "Mercado Bitcoin",
            Site = "https://www.mercadobitcoin.com.br",
            PaisSede = "Brasil",
            CodigoIntegracao = null, // sem integração: só cotação manual
            UsuarioId = usuario
        };

        context.Corretoras.AddRange(binance, mercadoBitcoin);
        await context.SaveChangesAsync(ct); // aqui os Ids das corretoras são gerados

        var referencia = DateTime.UtcNow.AddHours(-1);

        var ativos = new List<Ativo>
        {
            Novo(usuario, binance.Id, "BTCUSDT",  "BTC",  "USDT", TipoAtivo.Cripto,     78_705.99m, -0.347m, referencia),
            Novo(usuario, binance.Id, "ETHUSDT",  "ETH",  "USDT", TipoAtivo.Cripto,      2_412.30m,  1.882m, referencia),
            Novo(usuario, binance.Id, "SOLUSDT",  "SOL",  "USDT", TipoAtivo.Cripto,        121.44m, -2.115m, referencia),
            Novo(usuario, binance.Id, "BNBUSDT",  "BNB",  "USDT", TipoAtivo.Token,         604.10m,  0.740m, referencia),
            Novo(usuario, binance.Id, "USDCUSDT", "USDC", "USDT", TipoAtivo.Stablecoin,     0.9998m, 0.010m, referencia),

            // Não monitorado: demonstra o filtro ?monitorado=false
            Novo(usuario, mercadoBitcoin.Id, "BTCBRL", "BTC", "BRL", TipoAtivo.Cripto, 421_500.00m, -0.510m, referencia, monitorado: false),

            // Sem nenhuma cotação: demonstra lista vazia e UltimoPreco nulo
            new Ativo
            {
                Simbolo = "ETHBRL",
                MoedaBase = "ETH",
                MoedaCotacao = "BRL",
                Tipo = TipoAtivo.Cripto,
                Monitorado = false,
                CorretoraId = mercadoBitcoin.Id,
                UsuarioId = usuario
            }
        };

        context.Ativos.AddRange(ativos);
        await context.SaveChangesAsync(ct); // salva ativos e cotações filhas juntos
    }

    /// <summary>
    /// Cria o ativo já com três cotações históricas e com os campos espelho
    /// preenchidos a partir da mais recente — exatamente a mesma regra que a
    /// sincronização aplica em produção.
    /// </summary>
    private static Ativo Novo(
        Guid usuarioId, int corretoraId, string simbolo, string moedaBase, string moedaCotacao,
        TipoAtivo tipo, decimal precoAtual, decimal variacao, DateTime referencia, bool monitorado = true)
    {
        var ativo = new Ativo
        {
            Simbolo = simbolo,
            MoedaBase = moedaBase,
            MoedaCotacao = moedaCotacao,
            Tipo = tipo,
            Monitorado = monitorado,
            CorretoraId = corretoraId,
            UsuarioId = usuarioId,
            UltimoPreco = precoAtual,
            Variacao24h = variacao,
            AtualizadoEm = referencia
        };

        for (var i = 2; i >= 0; i--)
        {
            var fator = 1m + (i * 0.004m); // variação artificial para o histórico não ser uma reta

            ativo.Cotacoes.Add(new Cotacao
            {
                Preco = i == 0 ? precoAtual : decimal.Round(precoAtual * fator, 8),
                Variacao24h = variacao,
                PrecoMaximo24h = decimal.Round(precoAtual * 1.012m, 8),
                PrecoMinimo24h = decimal.Round(precoAtual * 0.985m, 8),
                Volume24h = 1_000_000m,
                CapturadoEm = referencia.AddHours(-i),
                Fonte = "Seed"
            });
        }

        return ativo;
    }
}
