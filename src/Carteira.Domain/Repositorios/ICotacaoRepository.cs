using Carteira.Domain.Entidades;

namespace Carteira.Domain.Repositorios;

public interface ICotacaoRepository
{
    Task<List<Cotacao>> ListarPorAtivoAsync(int ativoId, DateTime? desde, DateTime? ate, int limite, CancellationToken ct = default);
    Task<Cotacao?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<Cotacao?> ObterUltimaDoAtivoAsync(int ativoId, CancellationToken ct = default);
    Task AdicionarAsync(Cotacao cotacao, CancellationToken ct = default);
    Task RemoverAsync(Cotacao cotacao, CancellationToken ct = default);
}
