using ApiCripto.infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ApiCripto.Tests;

/// <summary>
/// Banco SQLite em memória, um por teste. É o MESMO provedor da aplicação
/// (diferente do InMemoryDatabase do EF), então os testes pegam problemas
/// reais de SQL — como a comparação de decimal que o AppDbContext corrige.
/// A conexão precisa ficar aberta: fechou, o banco em memória some.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;

    public AppDbContext Context { get; }

    public BancoDeTeste(bool comSeed = true)
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexao)
            .Options;

        Context = new AppDbContext(opcoes);
        Context.Database.Migrate(); // roda as migrations de verdade

        if (comSeed) DbSeeder.Seed(Context);
    }

    public void Dispose()
    {
        Context.Dispose();
        _conexao.Dispose();
    }
}
