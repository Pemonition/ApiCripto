using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Carteira.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Camada de ACESSO A DADOS. É a única que conhece o EF Core e o AppDbContext.
/// Não decide nada de negócio — só sabe ler e gravar.
/// </summary>
public class CorretoraRepository : ICorretoraRepository
{
    private readonly AppDbContext _context;

    public CorretoraRepository(AppDbContext context) => _context = context;

    public async Task<List<Corretora>> ListarAsync(CancellationToken ct = default) =>
        await _context.Corretoras
            .AsNoTracking() // só leitura: dispensa o change tracker e fica mais rápido
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

    public async Task<Corretora?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        await _context.Corretoras.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<bool> ExisteAsync(int id, CancellationToken ct = default) =>
        await _context.Corretoras.AnyAsync(c => c.Id == id, ct);

    /// <summary>
    /// AnyAsync gera "SELECT EXISTS(...)": não traz os ativos para a memória
    /// só para contar. Em tabela grande é a diferença entre milissegundos e
    /// segundos.
    /// </summary>
    public async Task<bool> PossuiAtivosAsync(int id, CancellationToken ct = default) =>
        await _context.Ativos.AnyAsync(a => a.CorretoraId == id, ct);

    public async Task AdicionarAsync(Corretora corretora, CancellationToken ct = default)
    {
        _context.Corretoras.Add(corretora);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Corretora corretora, CancellationToken ct = default)
    {
        if (_context.Entry(corretora).State == EntityState.Detached)
            _context.Corretoras.Update(corretora);

        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Corretora corretora, CancellationToken ct = default)
    {
        _context.Corretoras.Remove(corretora);
        await _context.SaveChangesAsync(ct);
    }
}
