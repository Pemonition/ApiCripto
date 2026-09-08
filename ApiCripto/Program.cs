using System.Reflection;
using System.Text.Json.Serialization;
using ApiCripto.infra;
using ApiCripto.integracoes;
using ApiCripto.repositories;
using ApiCripto.services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// ---------------------------------------------------------------------------
// 5) PREPARO DO BANCO NA SUBIDA
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

app.MapControllers();

app.Run();
