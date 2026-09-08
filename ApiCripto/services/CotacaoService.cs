using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.repositories;

namespace ApiCripto.services;

public interface ICotacaoService
{
    Task<List<Cotacao>?> ListarPorAtivoAsync(int ativoId, DateTime? desde, DateTime? ate, int limite);
    Task<Cotacao?> ObterAsync(int id);
    Task<(Cotacao? Criada, string? Erro)> CriarAsync(int ativoId, CotacaoDto dto);
    Task<Cotacao> RegistrarAsync(Ativo ativo, Cotacao cotacao);
    Task<bool> RemoverAsync(int id);
}

public class CotacaoService : ICotacaoService
{
    /// <summary>Teto de linhas devolvidas pelo histórico, para proteger a API.</summary>
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
    /// Devolve null quando o ATIVO não existe (vira 404) e lista vazia quando o
    /// ativo existe mas ainda não tem cotações (vira 200 com []). São situações
    /// diferentes e o cliente da API precisa conseguir distinguir as duas.
    /// </summary>
    public async Task<List<Cotacao>?> ListarPorAtivoAsync(int ativoId, DateTime? desde, DateTime? ate, int limite)
    {
        if (!await _ativoRepository.ExisteAsync(ativoId)) return null;

        var limiteEfetivo = limite <= 0 ? LimitePadrao : Math.Min(limite, LimiteMaximo);
        return await _repository.ListarPorAtivoAsync(ativoId, desde, ate, limiteEfetivo);
    }

    public Task<Cotacao?> ObterAsync(int id) => _repository.ObterPorIdAsync(id);

    /// <summary>Lançamento manual de cotação (corretora sem integração, correção histórica...).</summary>
    public async Task<(Cotacao? Criada, string? Erro)> CriarAsync(int ativoId, CotacaoDto dto)
    {
        var ativo = await _ativoRepository.ObterPorIdAsync(ativoId);
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

        return (await RegistrarAsync(ativo, cotacao), null);
    }

    /// <summary>
    /// ÚNICO ponto do sistema que grava Cotacao e sincroniza os campos espelho
    /// do Ativo. Tanto o lançamento manual quanto o SincronizacaoService passam
    /// por aqui — é isso que impede UltimoPreco de divergir do histórico.
    ///
    /// Se um dia precisar gravar cotação em outro fluxo, chame este método;
    /// não repita a lógica.
    /// </summary>
    public async Task<Cotacao> RegistrarAsync(Ativo ativo, Cotacao cotacao)
    {
        cotacao.AtivoId = ativo.Id;
        cotacao.Ativo = null; // evita que o EF tente inserir o ativo de novo

        if (cotacao.CapturadoEm == default)
            cotacao.CapturadoEm = DateTime.UtcNow;

        await _repository.AdicionarAsync(cotacao);

        // Só promove a cotação a "último preço" se ela for de fato a mais
        // recente. Sem essa comparação, importar um histórico antigo faria o
        // preço atual voltar no tempo.
        if (ativo.AtualizadoEm is null || cotacao.CapturadoEm >= ativo.AtualizadoEm)
        {
            ativo.UltimoPreco = cotacao.Preco;
            ativo.Variacao24h = cotacao.Variacao24h;
            ativo.AtualizadoEm = cotacao.CapturadoEm;
            await _ativoRepository.AtualizarAsync(ativo);
        }

        return cotacao;
    }

    public async Task<bool> RemoverAsync(int id)
    {
        var cotacao = await _repository.ObterPorIdAsync(id);
        if (cotacao is null) return false;

        await _repository.RemoverAsync(cotacao);
        return true;
    }
}
