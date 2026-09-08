using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.repositories;

namespace ApiCripto.services;

public interface IAtivoService
{
    Task<List<Ativo>> ListarAsync(AtivoFiltro filtro);
    Task<Ativo?> ObterPorIdAsync(int id);
    Task<(Ativo? Criado, string? Erro)> CriarAsync(AtivoDto dto);
    Task<(bool Atualizado, string? Erro)> AtualizarAsync(int id, AtivoDto dto);
    Task<bool> RemoverAsync(int id);
}

public class AtivoService : IAtivoService
{
    private readonly IAtivoRepository _repository;
    private readonly ICorretoraRepository _corretoraRepository;

    public AtivoService(IAtivoRepository repository, ICorretoraRepository corretoraRepository)
    {
        _repository = repository;
        _corretoraRepository = corretoraRepository;
    }

    public Task<List<Ativo>> ListarAsync(AtivoFiltro filtro) => _repository.ListarAsync(filtro);

    public Task<Ativo?> ObterPorIdAsync(int id) => _repository.ObterPorIdAsync(id);

    public async Task<(Ativo? Criado, string? Erro)> CriarAsync(AtivoDto dto)
    {
        // Regra 1: o pai do relacionamento 1-N tem que existir.
        // Sem isso o banco devolveria uma violação de FK crua (erro 500).
        if (!await _corretoraRepository.ExisteAsync(dto.CorretoraId))
            return (null, $"Corretora {dto.CorretoraId} não encontrada.");

        var simbolo = dto.Simbolo.Trim().ToUpperInvariant();

        // Regra 2: não duplicar o mesmo símbolo dentro da mesma corretora.
        if (await _repository.ExisteSimboloNaCorretoraAsync(dto.CorretoraId, simbolo))
            return (null, $"O ativo '{simbolo}' já está cadastrado nesta corretora.");

        var ativo = new Ativo
        {
            Simbolo = simbolo,
            MoedaBase = dto.MoedaBase.Trim().ToUpperInvariant(),
            MoedaCotacao = dto.MoedaCotacao.Trim().ToUpperInvariant(),
            Tipo = dto.Tipo,
            Monitorado = dto.Monitorado,
            CorretoraId = dto.CorretoraId
            // UltimoPreco/Variacao24h/AtualizadoEm nascem nulos de propósito:
            // ativo recém-cadastrado ainda não tem preço até a 1ª sincronização.
        };

        await _repository.AdicionarAsync(ativo);

        // Relê para devolver o objeto já com a Corretora carregada na resposta.
        return (await _repository.ObterPorIdAsync(ativo.Id), null);
    }

    public async Task<(bool Atualizado, string? Erro)> AtualizarAsync(int id, AtivoDto dto)
    {
        var existente = await _repository.ObterPorIdAsync(id);
        if (existente is null) return (false, null); // erro null = "não achei" (vira 404)

        if (!await _corretoraRepository.ExisteAsync(dto.CorretoraId))
            return (false, $"Corretora {dto.CorretoraId} não encontrada.");

        var simbolo = dto.Simbolo.Trim().ToUpperInvariant();

        // ignorarId: o próprio registro não conta como duplicata dele mesmo.
        if (await _repository.ExisteSimboloNaCorretoraAsync(dto.CorretoraId, simbolo, id))
            return (false, $"O ativo '{simbolo}' já está cadastrado nesta corretora.");

        existente.Simbolo = simbolo;
        existente.MoedaBase = dto.MoedaBase.Trim().ToUpperInvariant();
        existente.MoedaCotacao = dto.MoedaCotacao.Trim().ToUpperInvariant();
        existente.Tipo = dto.Tipo;
        existente.Monitorado = dto.Monitorado;
        existente.CorretoraId = dto.CorretoraId;
        // Os campos espelho NÃO são tocados aqui: quem os escreve é a cotação.

        await _repository.AtualizarAsync(existente);
        return (true, null);
    }

    /// <summary>
    /// Remove o ativo. As cotações filhas somem junto por DeleteBehavior.Cascade
    /// (configurado no AppDbContext) — é intencional: histórico sem ativo é lixo.
    /// </summary>
    public async Task<bool> RemoverAsync(int id)
    {
        var ativo = await _repository.ObterPorIdAsync(id);
        if (ativo is null) return false;

        await _repository.RemoverAsync(ativo);
        return true;
    }
}
