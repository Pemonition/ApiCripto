using Carteira.Application.Comum;
using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Domain.Consultas;
using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;

namespace Carteira.Application.Servicos;

public interface IAtivoService
{
    Task<PagedResult<AtivoResposta>> ListarAsync(AtivoFiltro filtro, CancellationToken ct = default);
    Task<AtivoResposta?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<(AtivoResposta? Criado, string? Erro)> CriarAsync(AtivoDto dto, CancellationToken ct = default);
    Task<(bool Atualizado, string? Erro)> AtualizarAsync(int id, AtivoDto dto, CancellationToken ct = default);
    Task<bool> RemoverAsync(int id, CancellationToken ct = default);
}

public class AtivoService : IAtivoService
{
    private readonly IAtivoRepository _repository;
    private readonly ICorretoraRepository _corretoraRepository;
    private readonly IUsuarioAtual _usuario;

    public AtivoService(
        IAtivoRepository repository,
        ICorretoraRepository corretoraRepository,
        IUsuarioAtual usuario)
    {
        _repository = repository;
        _corretoraRepository = corretoraRepository;
        _usuario = usuario;
    }

    public async Task<PagedResult<AtivoResposta>> ListarAsync(AtivoFiltro filtro, CancellationToken ct = default)
    {
        var pagina = await _repository.ListarAsync(filtro, ct);
        return pagina.Converter(a => a.ParaResposta());
    }

    public async Task<AtivoResposta?> ObterPorIdAsync(int id, CancellationToken ct = default)
    {
        var ativo = await _repository.ObterPorIdAsync(id, ct);
        return ativo?.ParaResposta();
    }

    public async Task<(AtivoResposta? Criado, string? Erro)> CriarAsync(AtivoDto dto, CancellationToken ct = default)
    {
        // Regra 1: o pai do relacionamento 1-N tem que existir. Sem esta
        // checagem o banco devolveria violação de chave estrangeira crua, que
        // vira um 500 sem explicação para quem chamou.
        if (!await _corretoraRepository.ExisteAsync(dto.CorretoraId, ct))
            return (null, $"Corretora {dto.CorretoraId} não encontrada.");

        var simbolo = dto.Simbolo.Trim().ToUpperInvariant();

        // Regra 2: não duplicar o mesmo símbolo dentro da mesma corretora.
        if (await _repository.ExisteSimboloNaCorretoraAsync(dto.CorretoraId, simbolo, null, ct))
            return (null, $"O ativo '{simbolo}' já está cadastrado nesta corretora.");

        var ativo = new Ativo
        {
            Simbolo = simbolo,
            MoedaBase = dto.MoedaBase.Trim().ToUpperInvariant(),
            MoedaCotacao = dto.MoedaCotacao.Trim().ToUpperInvariant(),
            Tipo = dto.Tipo,
            Monitorado = dto.Monitorado,
            CorretoraId = dto.CorretoraId,
            UsuarioId = _usuario.Id
            // UltimoPreco / Variacao24h / AtualizadoEm nascem nulos: ativo
            // recém-cadastrado não tem preço até a primeira sincronização.
        };

        await _repository.AdicionarAsync(ativo, ct);

        // Relê para a resposta já sair com a corretora carregada.
        var criado = await _repository.ObterPorIdAsync(ativo.Id, ct);
        return (criado?.ParaResposta(), null);
    }

    public async Task<(bool Atualizado, string? Erro)> AtualizarAsync(int id, AtivoDto dto, CancellationToken ct = default)
    {
        var existente = await _repository.ObterPorIdAsync(id, ct);
        if (existente is null) return (false, null); // erro null = "não achei" (vira 404)

        if (!await _corretoraRepository.ExisteAsync(dto.CorretoraId, ct))
            return (false, $"Corretora {dto.CorretoraId} não encontrada.");

        var simbolo = dto.Simbolo.Trim().ToUpperInvariant();

        // ignorarId: o próprio registro não conta como duplicata dele mesmo.
        if (await _repository.ExisteSimboloNaCorretoraAsync(dto.CorretoraId, simbolo, id, ct))
            return (false, $"O ativo '{simbolo}' já está cadastrado nesta corretora.");

        existente.Simbolo = simbolo;
        existente.MoedaBase = dto.MoedaBase.Trim().ToUpperInvariant();
        existente.MoedaCotacao = dto.MoedaCotacao.Trim().ToUpperInvariant();
        existente.Tipo = dto.Tipo;
        existente.Monitorado = dto.Monitorado;
        existente.CorretoraId = dto.CorretoraId;
        // Campos espelho não são tocados aqui: quem os escreve é a cotação.

        await _repository.AtualizarAsync(existente, ct);
        return (true, null);
    }

    /// <summary>
    /// Remove o ativo. As cotações filhas somem junto por DeleteBehavior.Cascade
    /// — intencional: histórico sem ativo é linha órfã.
    /// </summary>
    public async Task<bool> RemoverAsync(int id, CancellationToken ct = default)
    {
        var ativo = await _repository.ObterPorIdAsync(id, ct);
        if (ativo is null) return false;

        await _repository.RemoverAsync(ativo, ct);
        return true;
    }
}
