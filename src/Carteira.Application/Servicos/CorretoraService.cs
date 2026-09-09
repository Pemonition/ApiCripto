using Carteira.Application.Comum;
using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;

namespace Carteira.Application.Servicos;

public interface ICorretoraService
{
    Task<List<CorretoraResposta>> ListarAsync(CancellationToken ct = default);
    Task<CorretoraResposta?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<CorretoraResposta> CriarAsync(CorretoraDto dto, CancellationToken ct = default);
    Task<bool> AtualizarAsync(int id, CorretoraDto dto, CancellationToken ct = default);
    Task<(bool Removido, string? Erro)> RemoverAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Regra de negócio de Corretora.
///
/// Esta camada não conhece HTTP (devolve null, não 404) e não conhece EF Core
/// (fala com interfaces de repositório). É por isso que ela é a única que dá
/// para testar sem subir banco nem servidor.
/// </summary>
public class CorretoraService : ICorretoraService
{
    private readonly ICorretoraRepository _repository;
    private readonly IUsuarioAtual _usuario;

    public CorretoraService(ICorretoraRepository repository, IUsuarioAtual usuario)
    {
        _repository = repository;
        _usuario = usuario;
    }

    public async Task<List<CorretoraResposta>> ListarAsync(CancellationToken ct = default)
    {
        var corretoras = await _repository.ListarAsync(ct);
        return corretoras.Select(c => c.ParaResposta()).ToList();
    }

    public async Task<CorretoraResposta?> ObterPorIdAsync(int id, CancellationToken ct = default)
    {
        var corretora = await _repository.ObterPorIdAsync(id, ct);
        return corretora?.ParaResposta();
    }

    public async Task<CorretoraResposta> CriarAsync(CorretoraDto dto, CancellationToken ct = default)
    {
        var corretora = new Corretora
        {
            Nome = dto.Nome.Trim(),
            Site = dto.Site?.Trim(),
            PaisSede = dto.PaisSede?.Trim(),
            CodigoIntegracao = Normalizar(dto.CodigoIntegracao),
            UsuarioId = _usuario.Id
        };

        await _repository.AdicionarAsync(corretora, ct);
        return corretora.ParaResposta();
    }

    public async Task<bool> AtualizarAsync(int id, CorretoraDto dto, CancellationToken ct = default)
    {
        var existente = await _repository.ObterPorIdAsync(id, ct);
        if (existente is null) return false;

        // Atualização campo a campo no objeto que veio do banco. Nunca troque
        // o objeto inteiro: Id, UsuarioId e a coleção de Ativos precisam
        // sobreviver à edição.
        existente.Nome = dto.Nome.Trim();
        existente.Site = dto.Site?.Trim();
        existente.PaisSede = dto.PaisSede?.Trim();
        existente.CodigoIntegracao = Normalizar(dto.CodigoIntegracao);

        await _repository.AtualizarAsync(existente, ct);
        return true;
    }

    /// <summary>
    /// Devolve (removido, erro) para o controller distinguir três desfechos
    /// com um retorno só: sucesso, "não existe" (404) e "existe mas não pode"
    /// (400 com motivo). Um bool sozinho não separa os dois últimos.
    /// </summary>
    public async Task<(bool Removido, string? Erro)> RemoverAsync(int id, CancellationToken ct = default)
    {
        var corretora = await _repository.ObterPorIdAsync(id, ct);
        if (corretora is null) return (false, null);

        // Espelha o DeleteBehavior.Restrict do banco, só que com mensagem.
        if (await _repository.PossuiAtivosAsync(id, ct))
            return (false, "Não é possível remover uma corretora que possui ativos vinculados.");

        await _repository.RemoverAsync(corretora, ct);
        return (true, null);
    }

    private static string? Normalizar(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();
}
