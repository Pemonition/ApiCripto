using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.repositories;

namespace ApiCripto.services;

public interface ICorretoraService
{
    Task<List<Corretora>> ListarAsync();
    Task<Corretora?> ObterPorIdAsync(int id);
    Task<Corretora> CriarAsync(CorretoraDto dto);
    Task<bool> AtualizarAsync(int id, CorretoraDto dto);
    Task<(bool Removido, string? Erro)> RemoverAsync(int id);
}

/// <summary>
/// Camada de REGRA DE NEGÓCIO. Não conhece HTTP (não retorna 404, retorna null)
/// e não conhece EF Core (fala com repositórios). É a camada testável.
/// </summary>
public class CorretoraService : ICorretoraService
{
    private readonly ICorretoraRepository _repository;

    public CorretoraService(ICorretoraRepository repository) => _repository = repository;

    public Task<List<Corretora>> ListarAsync() => _repository.ListarAsync();

    public Task<Corretora?> ObterPorIdAsync(int id) => _repository.ObterPorIdAsync(id);

    public async Task<Corretora> CriarAsync(CorretoraDto dto)
    {
        var corretora = new Corretora
        {
            Nome = dto.Nome.Trim(),
            Site = dto.Site?.Trim(),
            PaisSede = dto.PaisSede?.Trim(),
            CodigoIntegracao = string.IsNullOrWhiteSpace(dto.CodigoIntegracao)
                ? null
                : dto.CodigoIntegracao.Trim().ToUpperInvariant()
        };

        await _repository.AdicionarAsync(corretora);
        return corretora;
    }

    public async Task<bool> AtualizarAsync(int id, CorretoraDto dto)
    {
        var existente = await _repository.ObterPorIdAsync(id);
        if (existente is null) return false;

        // Atualização campo a campo no objeto que veio do banco.
        // Nunca troque o objeto inteiro: o Id e a coleção de Ativos precisam
        // sobreviver à edição.
        existente.Nome = dto.Nome.Trim();
        existente.Site = dto.Site?.Trim();
        existente.PaisSede = dto.PaisSede?.Trim();
        existente.CodigoIntegracao = string.IsNullOrWhiteSpace(dto.CodigoIntegracao)
            ? null
            : dto.CodigoIntegracao.Trim().ToUpperInvariant();

        await _repository.AtualizarAsync(existente);
        return true;
    }

    /// <summary>
    /// Devolve (removido, erro) para o controller distinguir três desfechos com
    /// um único retorno: sucesso, "não existe" (404) e "existe mas não pode"
    /// (400 com motivo). Um bool sozinho não conseguiria separar os dois últimos.
    /// </summary>
    public async Task<(bool Removido, string? Erro)> RemoverAsync(int id)
    {
        var corretora = await _repository.ObterPorIdAsync(id);
        if (corretora is null) return (false, null);

        // Espelha a regra DeleteBehavior.Restrict do banco, só que com mensagem.
        if (await _repository.PossuiAtivosAsync(id))
            return (false, "Não é possível remover uma corretora que possui ativos vinculados.");

        await _repository.RemoverAsync(corretora);
        return (true, null);
    }
}
