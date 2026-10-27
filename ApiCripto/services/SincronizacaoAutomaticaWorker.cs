namespace ApiCripto.services;

/// <summary>
/// Sincroniza os ativos monitorados com a Binance de tempos em tempos, sem
/// ninguém precisar chamar POST /api/sincronizacao.
///
/// Configuração (appsettings.json ou variável de ambiente):
///   "Sincronizacao": { "IntervaloMinutos": 15 }   -> 0 desliga o worker.
///
/// BackgroundService é Singleton, mas os services/repositórios são Scoped
/// (dependem do DbContext). Por isso cada rodada cria o próprio escopo — pedir
/// ISincronizacaoService direto no construtor daria erro na subida da API.
/// </summary>
public class SincronizacaoAutomaticaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SincronizacaoAutomaticaWorker> _logger;
    private readonly TimeSpan _intervalo;

    public SincronizacaoAutomaticaWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SincronizacaoAutomaticaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromMinutes(configuration.GetValue("Sincronizacao:IntervaloMinutos", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_intervalo <= TimeSpan.Zero)
        {
            _logger.LogInformation("Sincronização automática desligada (Sincronizacao:IntervaloMinutos = 0).");
            return;
        }

        // Primeira rodada logo na subida: na nuvem o banco nasce do seed a cada
        // deploy, então trocar os preços de exemplo pelos reais o quanto antes.
        using var timer = new PeriodicTimer(_intervalo);
        do
        {
            await SincronizarAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SincronizarAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISincronizacaoService>();

            var resultado = await service.SincronizarTodosAsync(stoppingToken);
            _logger.LogInformation(
                "Sincronização automática: {Sincronizados}/{Total} ativos atualizados, {Falhas} com falha.",
                resultado.Sincronizados, resultado.TotalConsiderados, resultado.ComFalha);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // API desligando: sair em silêncio.
        }
        catch (Exception ex)
        {
            // Uma rodada com erro não pode derrubar o worker (nem a API inteira).
            _logger.LogError(ex, "Falha na sincronização automática; nova tentativa no próximo ciclo.");
        }
    }
}
