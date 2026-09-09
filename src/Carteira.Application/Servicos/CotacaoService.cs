using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;

namespace Carteira.Application.Servicos;

public interface ICotacaoService
{
    Task<List<CotacaoResposta>?> ListarPorAtivoAsync(int ativoId, DateTime? desde, DateTime? ate, int limite, CancellationToken ct = default);
    Task<CotacaoResposta?> ObterAsync(int id, CancellationToken ct = default);
    Task<(CotacaoResposta? Criada, string? Erro)> CriarAsync(int ativoId, CotacaoDto dto, CancellationToken ct = default);
    Task<bool> RemoverAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gravação em nível de entidade, usada pela sincronização. Não recebe DTO
    /// porque quem chama já tem o Ativo rastreado em mãos — reler pelo Id só
    /// para converter seria uma ida ao banco sem propósito.
    /// </summary>
    Task<Cotacao> RegistrarAsync(Ativo ativo, Cotacao cotacao, CancellationToken ct = default);
}

public class CotacaoService : ICotacaoService
{
    public const int LimiteMaximo = 500;
    public const int LimitePadrao = 100;

    private readonly ICotacaoRepository _repository;
    private readonly IAtivoRepository _ativoRepository;

    public CotacaoService(ICotacaoRepository repository, IAtivoRepository ativoRepository)
    {
        _repository = repository;
        _ativoRepository = ativoRepository;
    }

    /// <summary>
    /// Devolve null quando o ATIVO não existe (vira 404) e lista vazia quando
    /// o ativo existe mas ainda não tem cotações (vira 200 com []). São
    /// situações diferentes e quem consome a API precisa distinguir.
    /// </summary>
    public async Task<List<CotacaoResposta>?> ListarPorAtivoAsync(
        int ativoId, DateTime? desde, DateTime? ate, int limite, CancellationToken ct = default)
    {
        if (!await _ativoRepository.ExisteAsync(ativoId, ct)) return null;

        var limiteEfetivo = limite <= 0 ? LimitePadrao : Math.Min(limite, LimiteMaximo);
        var cotacoes = await _repository.ListarPorAtivoAsync(ativoId, desde, ate, limiteEfetivo, ct);
        return cotacoes.Select(c => c.ParaResposta()).ToList();
    }

    public async Task<CotacaoResposta?> ObterAsync(int id, CancellationToken ct = default)
    {
        var cotacao = await _repository.ObterPorIdAsync(id, ct);
        return cotacao?.ParaResposta();
    }

    public async Task<(CotacaoResposta? Criada, string? Erro)> CriarAsync(
        int ativoId, CotacaoDto dto, CancellationToken ct = default)
    {
        var ativo = await _ativoRepository.ObterPorIdAsync(ativoId, ct);
        if (ativo is null) return (null, null); // erro null = 404

        if (dto.PrecoMinimo24h > dto.PrecoMaximo24h)
            return (null, "O preço mínimo não pode ser maior que o preço máximo.");

        var cotacao = new Cotacao
        {
            AtivoId = ativo.Id,
            Preco = dto.Preco,
            Variacao24h = dto.Variacao24h,
            PrecoMaximo24h = dto.PrecoMaximo24h,
            PrecoMinimo24h = dto.PrecoMinimo24h,
            Volume24h = dto.Volume24h,
            CapturadoEm = dto.CapturadoEm ?? DateTime.UtcNow,
            Fonte = "Manual"
        };

        var criada = await RegistrarAsync(ativo, cotacao, ct);
        return (criada.ParaResposta(), null);
    }

    /// <summary>
    /// ÚNICO ponto do sistema que grava Cotacao e sincroniza os campos espelho
    /// do Ativo. Lançamento manual e sincronização automática passam os dois
    /// por aqui — é isso que impede UltimoPreco de divergir do histórico.
    ///
    /// Se precisar gravar cotação num fluxo novo, chame este método. Não
    /// repita a lógica: no dia em que existirem dois lugares que escrevem o
    /// espelho, um deles vai esquecer de atualizar e os filtros de preço
    /// passam a mentir sem lançar erro nenhum.
    /// </summary>
    public async Task<Cotacao> RegistrarAsync(Ativo ativo, Cotacao cotacao, CancellationToken ct = default)
    {
        cotacao.AtivoId = ativo.Id;
        cotacao.Ativo = null; // evita que o EF tente inserir o ativo de novo

        if (cotacao.CapturadoEm == default)
            cotacao.CapturadoEm = DateTime.UtcNow;

        await _repository.AdicionarAsync(cotacao, ct);

        // Só promove a cotação a "último preço" se ela for de fato a mais
        // recente. Sem esta comparação, importar histórico antigo faria o
        // preço atual voltar no tempo.
        if (ativo.AtualizadoEm is null || cotacao.CapturadoEm >= ativo.AtualizadoEm)
        {
            ativo.UltimoPreco = cotacao.Preco;
            ativo.Variacao24h = cotacao.Variacao24h;
            ativo.AtualizadoEm = cotacao.CapturadoEm;
            await _ativoRepository.AtualizarAsync(ativo, ct);
        }

        return cotacao;
    }

    public async Task<bool> RemoverAsync(int id, CancellationToken ct = default)
    {
        var cotacao = await _repository.ObterPorIdAsync(id, ct);
        if (cotacao is null) return false;

        await _repository.RemoverAsync(cotacao, ct);
        return true;
    }
}
