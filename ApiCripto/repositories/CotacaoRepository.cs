using ApiCripto.infra;
using ApiCripto.models;
using Microsoft.EntityFrameworkCore;

namespace ApiCripto.repositories;

public interface ICotacaoRepository
{
    Task<List<Cotacao>> ListarPorAtivoAsync(int ativoId, DateTime? desde, DateTime? ate, int limite);
    Task<Cotacao?> ObterPorIdAsync(int id);
    Task<Cotacao?> ObterUltimaDoAtivoAsync(int ativoId);
    Task AdicionarAsync(Cotacao cotacao);
    Task RemoverAsync(Cotacao cotacao);
}

public class CotacaoRepository : ICotacaoRepository
{
    private readonly AppDbContext _context;

    public CotacaoRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Histórico do ativo, do mais recente para o mais antigo, com recorte por
    /// data e teto de linhas. O limite existe porque histórico de preço cresce
    /// sem parar — um endpoint sem teto vira um "SELECT *" acidental em produção.
    /// </summary>
    public async Task<List<Cotacao>> ListarPorAtivoAsync(int ativoId, DateTime? desde, DateTime? ate, int limite)
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
            .Take(limite)
            .ToListAsync();
    }

    public async Task<Cotacao?> ObterPorIdAsync(int id) =>
        await _context.Cotacoes.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Cotacao?> ObterUltimaDoAtivoAsync(int ativoId) =>
        await _context.Cotacoes
            .AsNoTracking()
            .Where(c => c.AtivoId == ativoId)
            .OrderByDescending(c => c.CapturadoEm)
            .FirstOrDefaultAsync();

    public async Task AdicionarAsync(Cotacao cotacao)
    {
        _context.Cotacoes.Add(cotacao);
        await _context.SaveChangesAsync();
    }

    public async Task RemoverAsync(Cotacao cotacao)
    {
        _context.Cotacoes.Remove(cotacao);
        await _context.SaveChangesAsync();
    }
}
