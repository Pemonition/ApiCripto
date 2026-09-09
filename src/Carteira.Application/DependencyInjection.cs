using Carteira.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Carteira.Application;

/// <summary>
/// Registro dos serviços desta camada.
///
/// Cada camada expõe o próprio método de registro em vez de o Program.cs
/// conhecer todas as classes concretas de todos os projetos. Assim, adicionar
/// um service novo mexe no arquivo da camada dele — e o Program.cs continua
/// legível com meia dúzia de linhas.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        // Scoped = uma instância por requisição HTTP. É o tempo de vida certo
        // para quem depende do DbContext, que também é Scoped.
        services.AddScoped<ICorretoraService, CorretoraService>();
        services.AddScoped<IAtivoService, AtivoService>();
        services.AddScoped<ICotacaoService, CotacaoService>();
        services.AddScoped<ISincronizacaoService, SincronizacaoService>();

        return services;
    }
}
