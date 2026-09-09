using Carteira.Domain.Consultas;
using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Carteira.Infrastructure.Persistencia.Repositorios;

public class AtivoRepository : IAtivoRepository
{
    private readonly AppDbContext _context;

    public AtivoRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Include da corretora num lugar só: se a listagem precisar de outro
    /// relacionamento amanhã, muda aqui e vale para todas as consultas.
    /// </summary>
    private IQueryable<Ativo> ComRelacionamento() =>
        _context.Ativos.Include(a => a.Corretora);

    /// <summary>
    /// CORAÇÃO DOS FILTROS.
    ///
    /// A consulta é montada como IQueryable — nada foi ao banco ainda — e cada
    /// filtro informado acrescenta um WHERE. Só o ToListAsync() no final
    /// dispara SQL. Trocar isso por .ToList() antes dos Where traria a tabela
    /// inteira para a memória: funciona com 7 ativos, derruba com 700 mil.
    /// </summary>
    public async Task<PagedResult<Ativo>> ListarAsync(AtivoFiltro filtro, CancellationToken ct = default)
    {
        var query = ComRelacionamento().AsNoTracking();

        if (filtro.CorretoraId.HasValue)
            query = query.Where(a => a.CorretoraId == filtro.CorretoraId.Value);

        if (!string.IsNullOrWhiteSpace(filtro.Simbolo))
        {
            // Os símbolos são gravados em maiúsculas pelo AtivoService, então
            // comparar contra o termo em maiúsculas dá busca case-insensitive
            // sem depender do collation do banco — que muda entre SQLite,
            // PostgreSQL e SQL Server.
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

        // Conta ANTES de paginar: o total precisa refletir o filtro inteiro,
        // não a página. São duas idas ao banco (COUNT + SELECT) e isso é o
        // preço normal da paginação com total — a alternativa seria não
        // informar quantas páginas existem.
        var total = await query.CountAsync(ct);

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

        // Desempate pelo Id: sem uma ordenação total, dois ativos com o mesmo
        // preço podem trocar de lugar entre uma página e outra, e aí um item
        // aparece duas vezes enquanto outro some. É o bug clássico de
        // paginação, e ele não dá erro — só entrega dados errados.
        query = query is IOrderedQueryable<Ativo> ordenada
            ? ordenada.ThenBy(a => a.Id)
            : query.OrderBy(a => a.Id);

        var itens = await query
            .Skip(filtro.Pular)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(ct);

        return PagedResult<Ativo>.Criar(itens, total, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<List<Ativo>> ListarMonitoradosPorIntegracaoAsync(
        string codigoIntegracao, CancellationToken ct = default) =>
        await _context.Ativos
            .Include(a => a.Corretora)
            .Where(a => a.Monitorado && a.Corretora!.CodigoIntegracao == codigoIntegracao)
            .OrderBy(a => a.Simbolo)
            .ToListAsync(ct); // SEM AsNoTracking: estes objetos serão atualizados

    public async Task<Ativo?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        await ComRelacionamento().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<bool> ExisteAsync(int id, CancellationToken ct = default) =>
        await _context.Ativos.AnyAsync(a => a.Id == id, ct);

    public async Task<bool> ExisteSimboloNaCorretoraAsync(
        int corretoraId, string simbolo, int? ignorarId = null, CancellationToken ct = default)
    {
        var termo = simbolo.Trim().ToUpperInvariant();
        return await _context.Ativos.AnyAsync(a =>
            a.CorretoraId == corretoraId &&
            a.Simbolo == termo &&
            (ignorarId == null || a.Id != ignorarId), ct);
    }

    public async Task AdicionarAsync(Ativo ativo, CancellationToken ct = default)
    {
        _context.Ativos.Add(ativo);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Se o objeto já está rastreado pelo DbContext (caso normal: veio de
    /// ObterPorIdAsync na mesma requisição), o EF já sabe o que mudou e basta
    /// salvar. Update() só é necessário para objetos soltos (Detached) e, se
    /// chamado à toa, marca o grafo inteiro como alterado — incluindo a
    /// Corretora carregada pelo Include, gerando UPDATE desnecessário nela.
    /// </summary>
    public async Task AtualizarAsync(Ativo ativo, CancellationToken ct = default)
    {
        if (_context.Entry(ativo).State == EntityState.Detached)
            _context.Ativos.Update(ativo);

        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Ativo ativo, CancellationToken ct = default)
    {
        _context.Ativos.Remove(ativo);
        await _context.SaveChangesAsync(ct);
    }
}
