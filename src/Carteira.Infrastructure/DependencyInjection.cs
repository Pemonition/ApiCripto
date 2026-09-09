using Carteira.Application.Comum;
using Carteira.Application.Integracoes;
using Carteira.Domain.Repositorios;
using Carteira.Infrastructure.Identidade;
using Carteira.Infrastructure.Integracoes.Binance;
using Carteira.Infrastructure.Persistencia;
using Carteira.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Carteira.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(
        this IServiceCollection services, IConfiguration configuration)
    {
        // -----------------------------------------------------------------
        // Banco. A connection string vem da configuração, nunca do código.
        // Trocar de banco = trocar UseNpgsql pelo provider novo. Nenhum
        // controller, service ou repositório muda.
        // -----------------------------------------------------------------
        var conexao = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default não configurada. " +
                "Defina no appsettings.json ou na variável de ambiente " +
                "ConnectionStrings__Default.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(conexao));

        // Quem é o dono dos dados. Hoje fixo; vira leitura do JWT sem tocar
        // em mais nada. (Ver UsuarioAtualFixo.)
        services.AddScoped<IUsuarioAtual, UsuarioAtualFixo>();

        services.AddScoped<ICorretoraRepository, CorretoraRepository>();
        services.AddScoped<IAtivoRepository, AtivoRepository>();
        services.AddScoped<ICotacaoRepository, CotacaoRepository>();

        // -----------------------------------------------------------------
        // Provedores de cotação.
        //
        // Registrados como IProvedorDeCotacao (a interface), não como a classe
        // concreta. É isso que faz o SincronizacaoService receber
        // IEnumerable<IProvedorDeCotacao> com TODOS eles e indexar por código.
        //
        // Para plugar uma segunda corretora: escreva a classe e acrescente um
        // AddHttpClient aqui. O núcleo do sistema não muda.
        // -----------------------------------------------------------------
        var binanceUrl = configuration["Binance:BaseUrl"] ?? "https://data-api.binance.vision";
        var binanceTimeout = configuration.GetValue("Binance:TimeoutSegundos", 15);

        services.AddHttpClient<IProvedorDeCotacao, BinanceProvedor>(client =>
            {
                client.BaseAddress = new Uri(binanceUrl);
                // Timeout curto de propósito: a API não pode ficar pendurada
                // esperando um terceiro. Melhor falhar rápido com 502 claro.
                client.Timeout = TimeSpan.FromSeconds(binanceTimeout);
                client.DefaultRequestHeaders.Add("User-Agent", "Carteira/1.0");
            })
            .AddPolicyHandler(PoliticaDeRetentativa());

        return services;
    }

    /// <summary>
    /// Retentativa com espera exponencial (2s, 4s, 8s) para falhas
    /// transitórias: erros 5xx, falhas de rede e 429 (limite de requisições).
    ///
    /// Exponencial e não fixo porque, quando o motivo é limite de requisições,
    /// insistir no mesmo ritmo prolonga o bloqueio em vez de resolvê-lo —
    /// cada tentativa conta contra a cota.
    ///
    /// Só três tentativas: além disso, o usuário já desistiu de esperar e o
    /// certo é devolver o erro e deixar a próxima sincronização tentar.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> PoliticaDeRetentativa() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, tentativa => TimeSpan.FromSeconds(Math.Pow(2, tentativa)));
}
