using Carteira.Domain.Consultas;
using Carteira.Domain.Entidades;

namespace Carteira.Domain.Repositorios;

public interface IAtivoRepository
{
    Task<PagedResult<Ativo>> ListarAsync(AtivoFiltro filtro, CancellationToken ct = default);

    /// <summary>Ativos monitorados cuja corretora usa o código de integração informado.</summary>
    Task<List<Ativo>> ListarMonitoradosPorIntegracaoAsync(string codigoIntegracao, CancellationToken ct = default);

    Task<Ativo?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Duplicidade de símbolo dentro da mesma corretora. O índice único no
    /// banco é a garantia real (contra duas requisições simultâneas); esta
    /// checagem existe para devolver 400 com mensagem legível em vez de 500.
    /// </summary>
    Task<bool> ExisteSimboloNaCorretoraAsync(int corretoraId, string simbolo, int? ignorarId = null, CancellationToken ct = default);

    Task AdicionarAsync(Ativo ativo, CancellationToken ct = default);
    Task AtualizarAsync(Ativo ativo, CancellationToken ct = default);
    Task RemoverAsync(Ativo ativo, CancellationToken ct = default);
}
