using Carteira.Application.Comum;
using Carteira.Infrastructure.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Carteira.Infrastructure.Persistencia;

/// <summary>
/// Fábrica usada SÓ pelo "dotnet ef" em tempo de design.
///
/// Ela existe porque o AppDbContext passou a depender de IUsuarioAtual, e a
/// ferramenta de migrations não sobe o container de injeção da aplicação —
/// sem isto, "dotnet ef migrations add" falha com "Unable to create an object
/// of type 'AppDbContext'".
///
/// A connection string aqui é só para o EF conseguir montar o modelo e gerar
/// o SQL; ela não é usada em tempo de execução.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("CARTEIRA_CONNECTION")
                      ?? "Host=localhost;Port=5432;Database=carteira;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexao)
            .Options;

        IUsuarioAtual usuario = new UsuarioAtualFixo();
        return new AppDbContext(options, usuario);
    }
}
