using ApiCripto.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApiCripto.infra;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Corretora> Corretoras => Set<Corretora>();
    public DbSet<Ativo> Ativos => Set<Ativo>();
    public DbSet<Cotacao> Cotacoes => Set<Cotacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // -----------------------------------------------------------------
        // Relacionamento 1: Corretora 1 --- N Ativo
        // DeleteBehavior.Restrict: o banco RECUSA apagar uma corretora que
        // ainda tem ativos. É proteção de dados de cadastro — apagar em
        // cascata aqui destruiria silenciosamente todo o histórico de preços.
        // O CorretoraService devolve 400 com mensagem antes de chegar nisso.
        // -----------------------------------------------------------------
        modelBuilder.Entity<Corretora>()
            .HasMany(c => c.Ativos)
            .WithOne(a => a.Corretora)
            .HasForeignKey(a => a.CorretoraId)
            .OnDelete(DeleteBehavior.Restrict);

        // -----------------------------------------------------------------
        // Relacionamento 2: Ativo 1 --- N Cotacao
        // Aqui o comportamento é o oposto — Cascade. Uma cotação não existe
        // sozinha: ela é o histórico DAQUELE ativo. Apagou o ativo, o
        // histórico vai junto, senão sobram linhas órfãs no banco.
        // -----------------------------------------------------------------
        modelBuilder.Entity<Ativo>()
            .HasMany(a => a.Cotacoes)
            .WithOne(c => c.Ativo)
            .HasForeignKey(c => c.AtivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um símbolo é único DENTRO de uma corretora (BTCUSDT pode existir em
        // duas corretoras diferentes, mas não duas vezes na mesma).
        // Índice único = a regra vira responsabilidade do banco, e não só do C#.
        modelBuilder.Entity<Ativo>()
            .HasIndex(a => new { a.CorretoraId, a.Simbolo })
            .IsUnique();

        // Consulta mais frequente do histórico: "as últimas cotações do ativo X".
        modelBuilder.Entity<Cotacao>()
            .HasIndex(c => new { c.AtivoId, c.CapturadoEm });

        modelBuilder.Entity<Corretora>().Property(c => c.Nome).HasMaxLength(80).IsRequired();
        modelBuilder.Entity<Ativo>().Property(a => a.Simbolo).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<Ativo>().Property(a => a.MoedaBase).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<Ativo>().Property(a => a.MoedaCotacao).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<Cotacao>().Property(c => c.Fonte).HasMaxLength(30).IsRequired();

        // -----------------------------------------------------------------
        // ATENÇÃO / SQLite: o SQLite não tem tipo decimal nativo — ele guarda
        // decimal como TEXT. Comparação de texto quebraria os filtros
        // precoMin/precoMax ("9" > "10" em ordem alfabética) e o EF emite
        // warning nesse caso. Converter para double faz a comparação acontecer
        // numericamente no SQL.
        //
        // Ao migrar para PostgreSQL/SQL Server, APAGUE este bloco: lá decimal
        // é nativo e a conversão só faria perder precisão.
        // -----------------------------------------------------------------
        if (Database.IsSqlite())
        {
            var conversorDecimal = new ValueConverter<decimal, double>(
                v => (double)v,
                v => (decimal)v);

            var conversorDecimalNulo = new ValueConverter<decimal?, double?>(
                v => v.HasValue ? (double?)(double)v.Value : null,
                v => v.HasValue ? (decimal?)(decimal)v.Value : null);

            foreach (var propriedade in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties()))
            {
                if (propriedade.ClrType == typeof(decimal))
                    propriedade.SetValueConverter(conversorDecimal);
                else if (propriedade.ClrType == typeof(decimal?))
                    propriedade.SetValueConverter(conversorDecimalNulo);
            }
        }
    }
}
