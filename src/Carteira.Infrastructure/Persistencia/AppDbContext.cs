using Carteira.Application.Comum;
using Carteira.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Carteira.Infrastructure.Persistencia;

public class AppDbContext : DbContext
{
    private readonly IUsuarioAtual _usuario;

    public AppDbContext(DbContextOptions<AppDbContext> options, IUsuarioAtual usuario)
        : base(options)
    {
        _usuario = usuario;
    }

    public DbSet<Corretora> Corretoras => Set<Corretora>();
    public DbSet<Ativo> Ativos => Set<Ativo>();
    public DbSet<Cotacao> Cotacoes => Set<Cotacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // -----------------------------------------------------------------
        // Relacionamento 1: Corretora 1 --- N Ativo
        //
        // Restrict: o banco RECUSA apagar corretora que ainda tem ativos.
        // Cascade aqui destruiria todo o histórico de preços em silêncio.
        // -----------------------------------------------------------------
        modelBuilder.Entity<Corretora>()
            .HasMany(c => c.Ativos)
            .WithOne(a => a.Corretora)
            .HasForeignKey(a => a.CorretoraId)
            .OnDelete(DeleteBehavior.Restrict);

        // -----------------------------------------------------------------
        // Relacionamento 2: Ativo 1 --- N Cotacao
        //
        // Cascade, o oposto do anterior e igualmente proposital: uma cotação
        // é o histórico DAQUELE ativo e não existe sozinha.
        // -----------------------------------------------------------------
        modelBuilder.Entity<Ativo>()
            .HasMany(a => a.Cotacoes)
            .WithOne(c => c.Ativo)
            .HasForeignKey(c => c.AtivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // -----------------------------------------------------------------
        // ISOLAMENTO POR USUÁRIO
        //
        // Filtro global: toda consulta a estas entidades ganha
        // "WHERE UsuarioId = @usuarioAtual" automaticamente, inclusive as que
        // alguém escrever daqui a dois anos sem lembrar que o sistema é
        // multiusuário.
        //
        // A alternativa — lembrar de filtrar em cada método de cada
        // repositório — funciona até a primeira vez que alguém esquece. E
        // esse tipo de esquecimento não dá erro: dá vazamento de dados de um
        // usuário para outro, calado.
        //
        // Para ignorar o filtro deliberadamente (seed, rotina administrativa),
        // use .IgnoreQueryFilters() na consulta — explícito, e visível em code
        // review.
        // -----------------------------------------------------------------
        modelBuilder.Entity<Corretora>().HasQueryFilter(c => c.UsuarioId == _usuario.Id);
        modelBuilder.Entity<Ativo>().HasQueryFilter(a => a.UsuarioId == _usuario.Id);

        // Cotacao não tem UsuarioId próprio: ela é sempre alcançada através do
        // Ativo, que já é filtrado. Duplicar a coluna criaria a possibilidade
        // de uma cotação pertencer a um usuário diferente do dono do ativo —
        // um estado inválido que é melhor não conseguir representar.
        modelBuilder.Entity<Cotacao>().HasQueryFilter(c => c.Ativo!.UsuarioId == _usuario.Id);

        // Um símbolo é único DENTRO de uma corretora: BTCUSDT pode existir em
        // duas corretoras, mas não duas vezes na mesma. Índice único = a regra
        // vira responsabilidade do banco, não só do C#.
        modelBuilder.Entity<Ativo>()
            .HasIndex(a => new { a.CorretoraId, a.Simbolo })
            .IsUnique();

        // Consulta mais frequente do histórico: "últimas cotações do ativo X".
        modelBuilder.Entity<Cotacao>()
            .HasIndex(c => new { c.AtivoId, c.CapturadoEm });

        modelBuilder.Entity<Corretora>().HasIndex(c => c.UsuarioId);
        modelBuilder.Entity<Ativo>().HasIndex(a => a.UsuarioId);

        modelBuilder.Entity<Corretora>().Property(c => c.Nome).HasMaxLength(80).IsRequired();
        modelBuilder.Entity<Corretora>().Property(c => c.Site).HasMaxLength(200);
        modelBuilder.Entity<Corretora>().Property(c => c.PaisSede).HasMaxLength(60);
        modelBuilder.Entity<Corretora>().Property(c => c.CodigoIntegracao).HasMaxLength(30);

        modelBuilder.Entity<Ativo>().Property(a => a.Simbolo).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<Ativo>().Property(a => a.MoedaBase).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<Ativo>().Property(a => a.MoedaCotacao).HasMaxLength(10).IsRequired();

        modelBuilder.Entity<Cotacao>().Property(c => c.Fonte).HasMaxLength(30).IsRequired();

        // -----------------------------------------------------------------
        // PRECISÃO DECIMAL
        //
        // No PostgreSQL decimal é numeric nativo, então some o conversor para
        // double que a versão SQLite precisava — e some junto o risco de
        // arredondamento que ele trazia.
        //
        // 18,8: oito casas porque cripto se negocia em satoshi (0,00000001) e
        // truncar em duas casas transformaria posições pequenas em zero.
        // Volume ganha 24,8 porque volume de par líquido passa fácil de bilhão.
        // -----------------------------------------------------------------
        foreach (var propriedade in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            propriedade.SetPrecision(18);
            propriedade.SetScale(8);
        }

        modelBuilder.Entity<Cotacao>().Property(c => c.Volume24h).HasPrecision(24, 8);

        base.OnModelCreating(modelBuilder);
    }
}
