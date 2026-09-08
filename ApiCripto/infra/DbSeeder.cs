using ApiCripto.models;

namespace ApiCripto.infra;

/// <summary>
/// Popula o banco na primeira execução. Serve para dois propósitos:
/// 1) quem clonar o projeto vê a API funcionando sem cadastrar nada;
/// 2) a API continua demonstrável mesmo SEM internet (rede da escola bloqueando
///    a Binance, por exemplo) — os filtros e o histórico já têm dados.
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        // Guarda de idempotência: se já tem dado, não popula de novo.
        if (context.Corretoras.Any()) return;

        var binance = new Corretora
        {
            Nome = "Binance",
            Site = "https://www.binance.com",
            PaisSede = "Malta",
            CodigoIntegracao = "BINANCE" // habilita a sincronização automática
        };

        var mercadoBitcoin = new Corretora
        {
            Nome = "Mercado Bitcoin",
            Site = "https://www.mercadobitcoin.com.br",
            PaisSede = "Brasil",
            CodigoIntegracao = null // sem integração: só cotação manual
        };

        context.Corretoras.AddRange(binance, mercadoBitcoin);
        context.SaveChanges(); // aqui os Ids das corretoras são gerados

        var referencia = DateTime.UtcNow.AddHours(-1);

        var ativos = new List<Ativo>
        {
            NovoAtivo(binance.Id, "BTCUSDT", "BTC", "USDT", TipoAtivo.Cripto, 78_705.99m, -0.347m, referencia),
            NovoAtivo(binance.Id, "ETHUSDT", "ETH", "USDT", TipoAtivo.Cripto, 2_412.30m, 1.882m, referencia),
            NovoAtivo(binance.Id, "SOLUSDT", "SOL", "USDT", TipoAtivo.Cripto, 121.44m, -2.115m, referencia),
            NovoAtivo(binance.Id, "BNBUSDT", "BNB", "USDT", TipoAtivo.Token, 604.10m, 0.740m, referencia),
            NovoAtivo(binance.Id, "USDCUSDT", "USDC", "USDT", TipoAtivo.Stablecoin, 0.9998m, 0.010m, referencia),
            // Ativo não monitorado: bom para demonstrar o filtro ?monitorado=false
            NovoAtivo(mercadoBitcoin.Id, "BTCBRL", "BTC", "BRL", TipoAtivo.Cripto, 421_500.00m, -0.510m, referencia, monitorado: false),
            // Ativo sem nenhuma cotação: bom para demonstrar lista vazia e UltimoPreco nulo
            new Ativo
            {
                Simbolo = "ETHBRL",
                MoedaBase = "ETH",
                MoedaCotacao = "BRL",
                Tipo = TipoAtivo.Cripto,
                Monitorado = false,
                CorretoraId = mercadoBitcoin.Id
            }
        };

        context.Ativos.AddRange(ativos);
        context.SaveChanges(); // salva ativos e as cotações filhas juntos
    }

    /// <summary>
    /// Cria o ativo já com três cotações históricas (t-2h, t-1h, agora) e com os
    /// campos espelho preenchidos a partir da cotação mais recente — exatamente
    /// a mesma regra que a sincronização aplica em produção.
    /// </summary>
    private static Ativo NovoAtivo(
        int corretoraId,
        string simbolo,
        string moedaBase,
        string moedaCotacao,
        TipoAtivo tipo,
        decimal precoAtual,
        decimal variacao,
        DateTime referencia,
        bool monitorado = true)
    {
        var ativo = new Ativo
        {
            Simbolo = simbolo,
            MoedaBase = moedaBase,
            MoedaCotacao = moedaCotacao,
            Tipo = tipo,
            Monitorado = monitorado,
            CorretoraId = corretoraId,
            UltimoPreco = precoAtual,
            Variacao24h = variacao,
            AtualizadoEm = referencia
        };

        for (var i = 2; i >= 0; i--)
        {
            // pequena variação artificial para o histórico não ser uma reta
            var fator = 1m + (i * 0.004m);
            var preco = decimal.Round(precoAtual * fator, 8);

            ativo.Cotacoes.Add(new Cotacao
            {
                Preco = i == 0 ? precoAtual : preco,
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
