using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Carteira.Infrastructure.Persistencia.Repositorios;

public class CotacaoRepository : ICotacaoRepository
{
    private readonly AppDbContext _context;

    public CotacaoRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Histórico do ativo, do mais recente para o mais antigo, com recorte por
    /// data e teto de linhas. O limite existe porque histórico de preço cresce
    /// sem parar — endpoint de histórico sem teto vira um "SELECT *" acidental.
    /// </summary>
    public async Task<List<Cotacao>> ListarPorAtivoAsync(
        int ativoId, DateTime? desde, DateTime? ate, int limite, CancellationToken ct = default)
    {
        var query = _context.Cotacoes
            .AsNoTracking()
            .Where(c => c.AtivoId == ativoId);

        if (desde.HasValue)
            query = query.Where(c => c.CapturadoEm >= desde.Value);

        if (ate.HasValue)
            query = query.Where(c => c.CapturadoEm <= ate.Value);

        return await query
            .OrderByDescending(c => c.CapturadoEm)
            .ThenByDescending(c => c.Id)
            .Take(limite)
            .ToListAsync(ct);
    }

    public async Task<Cotacao?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        await _context.Cotacoes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cotacao?> ObterUltimaDoAtivoAsync(int ativoId, CancellationToken ct = default) =>
        await _context.Cotacoes
            .AsNoTracking()
            .Where(c => c.AtivoId == ativoId)
            .OrderByDescending(c => c.CapturadoEm)
            .FirstOrDefaultAsync(ct);

    public async Task AdicionarAsync(Cotacao cotacao, CancellationToken ct = default)
    {
        _context.Cotacoes.Add(cotacao);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Cotacao cotacao, CancellationToken ct = default)
    {
        _context.Cotacoes.Remove(cotacao);
        await _context.SaveChangesAsync(ct);
    }
}
