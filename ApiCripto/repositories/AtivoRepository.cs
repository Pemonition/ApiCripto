using ApiCripto.dtos;
using ApiCripto.infra;
using ApiCripto.models;
using Microsoft.EntityFrameworkCore;

namespace ApiCripto.repositories;

public interface IAtivoRepository
{
    Task<List<Ativo>> ListarAsync(AtivoFiltro filtro);
    Task<List<Ativo>> ListarMonitoradosPorIntegracaoAsync(string codigoIntegracao);
    Task<Ativo?> ObterPorIdAsync(int id);
    Task<bool> ExisteAsync(int id);
    Task<bool> ExisteSimboloNaCorretoraAsync(int corretoraId, string simbolo, int? ignorarId = null);
    Task AdicionarAsync(Ativo ativo);
    Task AtualizarAsync(Ativo ativo);
    Task RemoverAsync(Ativo ativo);
}

public class AtivoRepository : IAtivoRepository
{
    private readonly AppDbContext _context;

    public AtivoRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Include da corretora em um único lugar: se amanhã a listagem precisar de
    /// mais um relacionamento, muda aqui e vale para todas as consultas.
    /// </summary>
    private IQueryable<Ativo> ComRelacionamento() =>
        _context.Ativos.Include(a => a.Corretora);

    /// <summary>
    /// CORAÇÃO DOS FILTROS.
    ///
    /// A query é montada em memória como IQueryable (nada foi ao banco ainda) e
    /// cada filtro informado adiciona um WHERE. Só o ToListAsync() no final
    /// dispara UM único SELECT com todas as condições — não existe filtragem
    /// em C# depois de trazer tudo do banco.
    ///
    /// Para adicionar um filtro novo: crie a propriedade em AtivoFiltro e
    /// acrescente um "if" aqui. Nada mais no projeto precisa mudar.
    /// </summary>
    public async Task<List<Ativo>> ListarAsync(AtivoFiltro filtro)
    {
        var query = ComRelacionamento().AsNoTracking();

        if (filtro.CorretoraId.HasValue)
            query = query.Where(a => a.CorretoraId == filtro.CorretoraId.Value);

        if (!string.IsNullOrWhiteSpace(filtro.Simbolo))
        {
            // Símbolos são gravados em maiúsculas pelo AtivoService, então
            // comparar contra o termo em maiúsculas dá busca case-insensitive
            // sem depender do collation do banco.
            var termo = filtro.Simbolo.Trim().ToUpperInvariant();
            query = query.Where(a => a.Simbolo.Contains(termo));
        }

        if (filtro.Tipo.HasValue)
            query = query.Where(a => a.Tipo == filtro.Tipo.Value);

        if (filtro.PrecoMin.HasValue)
            query = query.Where(a => a.UltimoPreco != null && a.UltimoPreco >= filtro.PrecoMin.Value);

        if (filtro.PrecoMax.HasValue)
            query = query.Where(a => a.UltimoPreco != null && a.UltimoPreco <= filtro.PrecoMax.Value);

        if (filtro.Monitorado.HasValue)
            query = query.Where(a => a.Monitorado == filtro.Monitorado.Value);

        query = (filtro.OrdenarPor?.ToLowerInvariant()) switch
        {
            "preco" => filtro.Desc
                ? query.OrderByDescending(a => a.UltimoPreco)
                : query.OrderBy(a => a.UltimoPreco),
            "variacao" => filtro.Desc
                ? query.OrderByDescending(a => a.Variacao24h)
                : query.OrderBy(a => a.Variacao24h),
            _ => filtro.Desc
                ? query.OrderByDescending(a => a.Simbolo)
                : query.OrderBy(a => a.Simbolo)
        };

        return await query.ToListAsync();
    }

    /// <summary>Ativos monitorados cuja corretora tem integração automática.</summary>
    public async Task<List<Ativo>> ListarMonitoradosPorIntegracaoAsync(string codigoIntegracao) =>
        await _context.Ativos
            .Include(a => a.Corretora)
            .Where(a => a.Monitorado && a.Corretora!.CodigoIntegracao == codigoIntegracao)
            .OrderBy(a => a.Simbolo)
            .ToListAsync(); // SEM AsNoTracking: estes objetos serão atualizados

    public async Task<Ativo?> ObterPorIdAsync(int id) =>
        await ComRelacionamento().FirstOrDefaultAsync(a => a.Id == id);

    public async Task<bool> ExisteAsync(int id) =>
        await _context.Ativos.AnyAsync(a => a.Id == id);

    /// <summary>
    /// Verificação de duplicidade em C#. O índice único no banco é a garantia
    /// final (contra duas requisições simultâneas); esta checagem existe só
    /// para devolver 400 com uma mensagem legível em vez de 500.
    /// </summary>
    public async Task<bool> ExisteSimboloNaCorretoraAsync(int corretoraId, string simbolo, int? ignorarId = null)
    {
        var termo = simbolo.Trim().ToUpperInvariant();
        return await _context.Ativos.AnyAsync(a =>
            a.CorretoraId == corretoraId &&
            a.Simbolo == termo &&
            (ignorarId == null || a.Id != ignorarId));
    }

    public async Task AdicionarAsync(Ativo ativo)
    {
        _context.Ativos.Add(ativo);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Se o objeto já está sendo rastreado pelo DbContext (caso normal: veio de
    /// ObterPorIdAsync na mesma requisição), o EF já sabe o que mudou e basta
    /// salvar. Update() só é necessário para objetos "soltos" (Detached) e, se
    /// chamado à toa, marca também os relacionamentos carregados como alterados,
    /// gerando UPDATEs desnecessários na tabela Corretoras.
    /// </summary>
    public async Task AtualizarAsync(Ativo ativo)
    {
        if (_context.Entry(ativo).State == EntityState.Detached)
            _context.Ativos.Update(ativo);

        await _context.SaveChangesAsync();
    }

    public async Task RemoverAsync(Ativo ativo)
    {
        _context.Ativos.Remove(ativo);
        await _context.SaveChangesAsync();
    }
}
