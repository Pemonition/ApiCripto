using System.Text;
using System.Security.Cryptography;
using ApiCripto.infra;
using ApiCripto.integracoes;
using ApiCripto.models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiCripto.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Senha = "Senha-apenas-teste-123!";
    public string Key { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public string DonoId { get; } = Guid.NewGuid().ToString();
    public string OutroId { get; } = Guid.NewGuid().ToString();
    public string AdminId { get; } = Guid.NewGuid().ToString();
    private readonly string _banco = Path.Combine(Path.GetTempPath(), $"apicripto-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", Key);
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_banco};Pooling=False");
        builder.UseSetting("Autorizacao:SincronizadorUsuarioId", DonoId);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key,
            ["ConnectionStrings:Default"] = $"Data Source={_banco};Pooling=False",
            ["Autorizacao:SincronizadorUsuarioId"] = DonoId,
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBinanceClient>();
            services.AddSingleton<IBinanceClient, BinanceFake>();
        });
    }

    public void CriarUsuarios()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        foreach (var (id, login, perfil) in new[]
        {
            (DonoId, "alice", "Usuario"), (OutroId, "bob", "Usuario"), (AdminId, "admin", "Admin")
        })
        {
            var u = new Usuario { Id = id, Login = login, Perfil = perfil };
            u.SenhaHash = hasher.HashPassword(u, Senha);
            db.Usuarios.Add(u);
        }
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var path in new[] { _banco, _banco + "-wal", _banco + "-shm" })
                if (File.Exists(path)) File.Delete(path);
    }
}
