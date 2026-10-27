using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using ApiCripto.infra;
using ApiCripto.integracoes;
using ApiCripto.repositories;
using ApiCripto.services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 0) PORTA NA NUVEM
// Render, Koyeb, Cloud Run e afins dizem em qual porta escutar pela variável
// PORT. Localmente ela não existe e vale o launchSettings.json (porta 5000).
// ---------------------------------------------------------------------------
var porta = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(porta))
    builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

// ---------------------------------------------------------------------------
// 1) BANCO DE DADOS
// A connection string fica no appsettings.json, nunca "chumbada" no código.
// Trocar de banco = trocar UseSqlite por UseNpgsql/UseSqlServer + a string.
// Nenhum controller, service ou repository precisa mudar.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ---------------------------------------------------------------------------
// 2) INJEÇÃO DE DEPENDÊNCIA
// Cada interface é ligada à sua implementação. É por isso que os services
// recebem interfaces no construtor e não dão "new" em nada: quem monta o
// objeto é o container, e trocar a implementação (por um fake em teste, por
// exemplo) é mudar UMA linha aqui.
//
// AddScoped = uma instância por requisição HTTP. É o tempo de vida certo para
// quem depende do DbContext, que também é Scoped.
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICorretoraRepository, CorretoraRepository>();
builder.Services.AddScoped<IAtivoRepository, AtivoRepository>();
builder.Services.AddScoped<ICotacaoRepository, CotacaoRepository>();

builder.Services.AddScoped<ICorretoraService, CorretoraService>();
builder.Services.AddScoped<IAtivoService, AtivoService>();
builder.Services.AddScoped<ICotacaoService, CotacaoService>();
builder.Services.AddScoped<ISincronizacaoService, SincronizacaoService>();

// Worker que chama a sincronização sozinho a cada N minutos (0 = desligado).
builder.Services.AddHostedService<SincronizacaoAutomaticaWorker>();

// ---------------------------------------------------------------------------
// 3) CLIENTE HTTP DA CORRETORA (typed client)
// AddHttpClient gerencia o pool de conexões e a reciclagem de DNS. Dar
// "new HttpClient()" dentro de um service é um erro clássico: esgota sockets
// sob carga e não enxerga mudança de DNS.
//
// BaseUrl padrão: data-api.binance.vision — endpoint público de market data da
// Binance, sem chave de API e sem bloqueio por região (o api.binance.com
// devolve HTTP 451 em alguns provedores/datacenters).
// ---------------------------------------------------------------------------
var binanceBaseUrl = builder.Configuration["Binance:BaseUrl"] ?? "https://data-api.binance.vision";
var binanceTimeout = builder.Configuration.GetValue("Binance:TimeoutSegundos", 15);

builder.Services.AddHttpClient<IBinanceClient, BinanceClient>(client =>
{
    client.BaseAddress = new Uri(binanceBaseUrl);
    // Timeout curto de propósito: a API não pode ficar pendurada esperando
    // um terceiro. Melhor falhar rápido e devolver 502 com mensagem clara.
    client.Timeout = TimeSpan.FromSeconds(binanceTimeout);
    client.DefaultRequestHeaders.Add("User-Agent", "ApiCripto/1.0");
});

// ---------------------------------------------------------------------------
// 4) MVC / JSON
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Protege contra loop infinito na serialização de relacionamentos
        // (Ativo -> Corretora -> Ativos -> ...). Rede de segurança: os
        // [JsonIgnore] nos models já cortam os ciclos previstos.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

        // Enum sai como "Cripto" e não como 0 — resposta legível e estável
        // mesmo que a ordem do enum mude no código.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ApiCripto",
        Version = "v1",
        Description = "API de acompanhamento de ativos de corretoras de cripto, " +
                      "alimentada pela API pública da Binance."
    });

    // Traz os comentários /// do código para dentro do Swagger.
    var arquivoXml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var caminhoXml = Path.Combine(AppContext.BaseDirectory, arquivoXml);
    if (File.Exists(caminhoXml)) options.IncludeXmlComments(caminhoXml);
});

// ---------------------------------------------------------------------------
// 5) PRONTO PARA NUVEM
// - Health check: a plataforma chama /health para saber se a API está viva
//   (e o check do DbContext confirma que o banco responde).
// - ProblemDetails: exceção não tratada vira JSON padrão (RFC 9457) em vez de
//   página de erro ou corpo vazio.
// - CORS liberado: um front (Angular, React...) em outro domínio pode consumir.
// - Rate limit na sincronização: com a API pública na internet, qualquer um
//   poderia disparar POST /api/sincronizacao em loop e queimar a cota da
//   Binance (HTTP 429). 10 chamadas por minuto é folga para uso humano.
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("banco");
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("sincronizacao", limite =>
    {
        limite.PermitLimit = builder.Configuration.GetValue("Sincronizacao:LimitePorMinuto", 10);
        limite.Window = TimeSpan.FromMinutes(1);
        limite.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// ---------------------------------------------------------------------------
// 6) PREPARO DO BANCO NA SUBIDA
// Migrate() aplica as migrations pendentes (e cria o banco se não existir),
// então um "git clone + dotnet run" já sobe funcionando.
//
// Cuidado ao levar para produção: rodar migration automática no start é
// prático em projeto de estudo, mas em produção o padrão é aplicar a migration
// em um passo separado do deploy, com backup e janela controlada.
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
    DbSeeder.Seed(context);
}

// Swagger habilitado sempre (é um projeto de estudo e a banca precisa abrir).
// Em produção real, isso ficaria dentro de "if (app.Environment.IsDevelopment())".
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiCripto v1");
    options.RoutePrefix = "swagger";
});

// Abrir a raiz http://localhost:5000 já cai no Swagger.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseCors();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Torna a classe Program visível para o projeto de testes (WebApplicationFactory).
public partial class Program;
