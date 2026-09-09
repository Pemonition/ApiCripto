using System.Reflection;
using System.Text.Json.Serialization;
using Carteira.Application;
using Carteira.Infrastructure;
using Carteira.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cada camada registra o que é dela. O Program.cs só compõe — é por isso que
// ele cabe em uma tela mesmo com o sistema crescendo.
builder.Services.AddAplicacao();
builder.Services.AddInfraestrutura(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enum sai como "Cripto" e não como 3 — resposta legível e estável
        // mesmo que a ordem do enum mude no código.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Nota: o ReferenceHandler.IgnoreCycles da versão anterior sumiu de propósito.
// Ele existia para quebrar ciclos ao serializar entidades. Com DTOs de saída
// não há ciclo para quebrar — o problema deixou de existir em vez de ser
// contornado.

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Carteira",
        Version = "v1",
        Description = "Acompanhamento de ativos e cotações, alimentado por APIs públicas de corretoras."
    });

    foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "Carteira.*.xml"))
        options.IncludeXmlComments(xml);
});

// Health check: sem isto, você só descobre que o banco caiu quando um usuário
// reclama. Com isto, o orquestrador (Docker, Kubernetes) descobre sozinho.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("banco");

var app = builder.Build();

// -----------------------------------------------------------------------
// Preparo do banco na subida.
//
// Prático em desenvolvimento e em projeto de estudo. Em produção o padrão é
// aplicar migration em passo separado do deploy, com backup e janela
// controlada — por isso está atrás de uma flag de configuração.
// -----------------------------------------------------------------------
if (app.Configuration.GetValue("Banco:MigrarNaSubida", true))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Carteira v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

/// <summary>
/// Exposto para os testes de integração conseguirem referenciar o host.
/// (Program é internal por padrão em projetos com top-level statements.)
/// </summary>
public partial class Program { }
