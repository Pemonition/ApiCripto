using Carteira.Domain.Entidades;

namespace Carteira.Domain.Repositorios;

/// <summary>
/// Contrato de persistência de Corretora.
///
/// A interface fica no domínio e a implementação na infraestrutura — é a
/// inversão de dependência na prática. O domínio diz "eu preciso saber se
/// existe uma corretora com este Id"; quem responde usando EF Core, Dapper ou
/// um arquivo texto é problema de outra camada.
///
/// Na aula, interface e implementação moram no mesmo arquivo. Funciona, mas
/// no mesmo assembly nada impede o domínio de importar o EF Core sem querer.
/// Separados em projetos, isso vira erro de compilação.
/// </summary>
public interface ICorretoraRepository
{
    Task<List<Corretora>> ListarAsync(CancellationToken ct = default);
    Task<Corretora?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAsync(int id, CancellationToken ct = default);
    Task<bool> PossuiAtivosAsync(int id, CancellationToken ct = default);
    Task AdicionarAsync(Corretora corretora, CancellationToken ct = default);
    Task AtualizarAsync(Corretora corretora, CancellationToken ct = default);
    Task RemoverAsync(Corretora corretora, CancellationToken ct = default);
}
