using ApiCripto.infra;
using ApiCripto.models;
using Microsoft.EntityFrameworkCore;

namespace ApiCripto.repositories;

public interface ICorretoraRepository
{
    Task<List<Corretora>> ListarAsync();
    Task<Corretora?> ObterPorIdAsync(int id);
    Task<bool> ExisteAsync(int id);
    Task<bool> PossuiAtivosAsync(int id);
    Task AdicionarAsync(Corretora corretora);
    Task AtualizarAsync(Corretora corretora);
    Task RemoverAsync(Corretora corretora);
}

/// <summary>
/// Camada de ACESSO A DADOS. É a única que conhece o EF Core e o AppDbContext.
/// Não decide nada de negócio — só sabe ler e gravar.
/// </summary>
public class CorretoraRepository : ICorretoraRepository
{
    private readonly AppDbContext _context;

    public CorretoraRepository(AppDbContext context) => _context = context;

    public async Task<List<Corretora>> ListarAsync() =>
        await _context.Corretoras
            .AsNoTracking() // só leitura: dispensa o change tracker e fica mais rápido
            .OrderBy(c => c.Nome)
            .ToListAsync();

    public async Task<Corretora?> ObterPorIdAsync(int id) =>
        await _context.Corretoras.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<bool> ExisteAsync(int id) =>
        await _context.Corretoras.AnyAsync(c => c.Id == id);

    // AnyAsync gera "SELECT EXISTS(...)" — não traz os ativos para a memória
    // só para contar. Em tabela grande isso é a diferença entre milissegundos
    // e segundos.
    public async Task<bool> PossuiAtivosAsync(int id) =>
        await _context.Ativos.AnyAsync(a => a.CorretoraId == id);

    public async Task AdicionarAsync(Corretora corretora)
    {
        _context.Corretoras.Add(corretora);
        await _context.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Corretora corretora)
    {
        // Mesma ideia do AtivoRepository: só marca manualmente o que está solto.
        if (_context.Entry(corretora).State == EntityState.Detached)
            _context.Corretoras.Update(corretora);

        await _context.SaveChangesAsync();
    }

    public async Task RemoverAsync(Corretora corretora)
    {
        _context.Corretoras.Remove(corretora);
        await _context.SaveChangesAsync();
    }
}
