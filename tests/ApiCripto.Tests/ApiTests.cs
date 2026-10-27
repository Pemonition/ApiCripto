using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiCripto.Tests;

/// <summary>
/// Sobe a API inteira em memória (sem porta, sem processo separado) e faz
/// requisições HTTP de verdade — testa roteamento, DI, JSON e banco juntos.
/// </summary>
public class ApiTests : IClassFixture<ApiTests.Fabrica>
{
    public sealed class Fabrica : WebApplicationFactory<Program>
    {
        private readonly string _arquivoBanco =
            Path.Combine(Path.GetTempPath(), $"apicripto-teste-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_arquivoBanco};Pooling=False");
            builder.UseSetting("Sincronizacao:IntervaloMinutos", "0"); // teste não fala com a Binance
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (File.Exists(_arquivoBanco)) File.Delete(_arquivoBanco);
        }
    }

    private readonly HttpClient _client;

    public ApiTests(Fabrica fabrica) => _client = fabrica.CreateClient();

    [Fact]
    public async Task Health_check_responde_Healthy()
    {
        var resposta = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lista_os_ativos_do_seed()
    {
        var ativos = await _client.GetFromJsonAsync<List<JsonElement>>("/api/ativo");

        Assert.Equal(7, ativos!.Count);
    }

    [Fact]
    public async Task Ativo_inexistente_devolve_404_em_ProblemDetails()
    {
        var resposta = await _client.GetAsync("/api/ativo/9999");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, corpo.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Rota_inexistente_tambem_responde_em_ProblemDetails()
    {
        // UseStatusCodePages: sem ele, um 404 de rota vem com corpo vazio.
        var resposta = await _client.GetAsync("/api/nao-existe");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }
}
