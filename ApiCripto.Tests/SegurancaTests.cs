using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApiCripto.dtos;
using ApiCripto.infra;
using ApiCripto.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ApiCripto.Tests;

public class SegurancaTests : IDisposable
{
    private readonly ApiFactory _app = new();
    private readonly HttpClient _client;
    public SegurancaTests()
    {
        _client = _app.CreateClient();
        _app.CriarUsuarios();
    }

    private async Task<string> Login(string login)
    {
        var r = await _client.PostAsJsonAsync("/api/auth/login", new { login, senha = ApiFactory.Senha });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var token = (await r.Content.ReadFromJsonAsync<TokenDto>())!;
        Assert.False(string.IsNullOrEmpty(token.Token));
        return token.Token;
    }

    private void Bearer(string? token) =>
        _client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);

    [Fact]
    public async Task Login_cadastro_hash_e_protecao_contra_escalacao()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/auth/me")).StatusCode);
        var r = await _client.PostAsJsonAsync("/api/auth/registrar", new { login = "carol", senha = ApiFactory.Senha });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal("Usuario", (await r.Content.ReadFromJsonAsync<UsuarioDto>())!.Perfil);
        Assert.DoesNotContain("hash", await r.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/auth/registrar",
            new { login = "CAROL", senha = ApiFactory.Senha })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/auth/registrar",
            new { login = "intruso", senha = ApiFactory.Senha, perfil = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/auth/registrar",
            new { login = "curta", senha = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/auth/login",
            new { login = "alice", senha = "senha-errada-123" })).StatusCode);
        Bearer(await Login("carol"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/auth/me")).StatusCode);
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Usuarios.SingleAsync(u => u.Login == "carol");
        Assert.NotEqual(ApiFactory.Senha, user.SenhaHash);
        Assert.Equal("Usuario", user.Perfil);
    }

    [Theory]
    [InlineData("expirado")]
    [InlineData("assinatura")]
    [InlineData("emissor")]
    [InlineData("destinatario")]
    [InlineData("sem-identidade")]
    [InlineData("perfil-forjado")]
    [InlineData("algoritmo")]
    [InlineData("sem-expiracao")]
    public async Task Recusa_tokens_invalidos(string caso)
    {
        var claims = new List<Claim> { new("role", caso == "perfil-forjado" ? "Admin" : "Usuario") };
        if (caso != "sem-identidade") claims.Add(new("sub", _app.DonoId));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            caso == "emissor" ? "outro" : "ApiCripto",
            caso == "destinatario" ? "outro" : "ApiCripto.Clientes",
            claims, now.AddHours(-2),
            caso == "sem-expiracao" ? null : caso == "expirado" ? now.AddMinutes(-1) : now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                caso == "assinatura" ? new string('x', 64) : _app.Key)),
                caso == "algoritmo" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        Bearer(new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Perfil_protege_todas_as_escritas_do_catalogo()
    {
        var paths = new[] { (HttpMethod.Post, "/api/corretora"), (HttpMethod.Put, "/api/corretora/1"),
            (HttpMethod.Delete, "/api/corretora/1"), (HttpMethod.Post, "/api/ativo"),
            (HttpMethod.Put, "/api/ativo/1"), (HttpMethod.Delete, "/api/ativo/1"),
            (HttpMethod.Post, "/api/ativo/1/cotacao"), (HttpMethod.Delete, "/api/ativo/1/cotacao/1") };
        foreach (var (method, path) in paths)
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(new(method, path))).StatusCode);
        Bearer(await Login("alice"));
        foreach (var (method, path) in paths)
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(new(method, path))).StatusCode);
        Bearer(await Login("admin"));
        var created = await _client.PostAsJsonAsync("/api/corretora", new { nome = "Teste autorizado" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<Corretora>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync($"/api/corretora/{item.Id}",
            new { nome = "Atualizado" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/corretora/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/corretora",
            new { nome = "" })).StatusCode);
    }

    [Fact]
    public async Task Usuario_especifico_nao_e_equivalente_a_perfil_admin()
    {
        foreach (var path in new[] { "/api/sincronizacao", "/api/sincronizacao/ativo/1" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync(path, null)).StatusCode);
        foreach (var login in new[] { "bob", "admin" })
        {
            Bearer(await Login(login));
            foreach (var path in new[] { "/api/sincronizacao", "/api/sincronizacao/ativo/1" })
                Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync(path, null)).StatusCode);
        }
        Bearer(await Login("alice"));
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/api/sincronizacao/ativo/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/api/sincronizacao", null)).StatusCode);
    }

    [Fact]
    public async Task Favoritos_isolam_dono_em_leitura_escrita_listagem_e_criacao()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/favoritos")).StatusCode);
        var alice = await Login("alice");
        Bearer(alice);
        var response = await _client.PostAsJsonAsync("/api/favoritos", new { ativoId = 1, nota = "Privado" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<Favorito>())!;
        Assert.Equal(_app.DonoId, item.UsuarioId);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/favoritos",
            new { ativoId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/favoritos",
            new { ativoId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(response.Headers.Location)).StatusCode);
        foreach (var login in new[] { "bob", "admin" })
        {
            Bearer(await Login(login));
            Assert.Empty((await _client.GetFromJsonAsync<List<Favorito>>($"/api/favoritos?usuarioId={_app.DonoId}"))!);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/favoritos/{item.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"/api/favoritos/{item.Id}",
                new { nota = "Invadido" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/favoritos/{item.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/favoritos",
                new { ativoId = 2, usuarioId = _app.DonoId })).StatusCode);
        }
        Bearer(alice);
        Assert.Equal("Privado", (await _client.GetFromJsonAsync<Favorito>($"/api/favoritos/{item.Id}"))!.Nota);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync($"/api/favoritos/{item.Id}",
            new { nota = "Atualizado" })).StatusCode);
        Assert.Equal("Atualizado", (await _client.GetFromJsonAsync<Favorito>($"/api/favoritos/{item.Id}"))!.Nota);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/favoritos/{item.Id}",
            new { nota = "Mudou dono", usuarioId = _app.OutroId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/favoritos/{item.Id}")).StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<Favorito>>("/api/favoritos"))!);
    }

    [Fact]
    public async Task Catalogo_e_swagger_continuam_publicos()
    {
        foreach (var path in new[] { "/api/ativo", "/api/ativo/1", "/api/corretora", "/api/corretora/1",
            "/api/ativo/1/cotacao", "/swagger/v1/swagger.json" })
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(path)).StatusCode);
        var ativos = await _client.GetFromJsonAsync<JsonElement>("/api/ativo?simbolo=BTC");
        Assert.True(ativos.GetArrayLength() > 0);
        Assert.All(ativos.EnumerateArray(), a => Assert.Contains("BTC", a.GetProperty("simbolo").GetString()));
    }

    [Fact]
    public async Task Login_tem_limite_de_tentativas()
    {
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/auth/login",
                new { login = "ninguem", senha = ApiFactory.Senha })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.PostAsJsonAsync("/api/auth/login",
            new { login = "ninguem", senha = ApiFactory.Senha })).StatusCode);
    }

    public void Dispose() { _client.Dispose(); _app.Dispose(); }
}
