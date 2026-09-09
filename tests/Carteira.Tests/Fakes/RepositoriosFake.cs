using Carteira.Application.Comum;
using Carteira.Domain.Consultas;
using Carteira.Domain.Entidades;
using Carteira.Domain.Repositorios;

namespace Carteira.Tests.Fakes;

/// <summary>
/// Repositórios de mentira, guardando tudo em lista na memória.
///
/// Por que fake escrito à mão e não Moq: aqui os testes exercitam REGRA DE
/// NEGÓCIO, e regra depende de estado ("existe outro ativo com esse símbolo?").
/// Com mock, cada teste precisaria configurar o retorno de cada chamada e o
/// teste vira uma descrição da implementação — muda a ordem das chamadas no
/// service e o teste quebra sem nenhum bug ter aparecido.
///
/// Com fake, o teste diz o que importa: "dado que já existe BTCUSDT nesta
/// corretora, cadastrar de novo deve falhar".
///
/// E note que isto só é possível porque as interfaces de repositório moram no
/// domínio, longe do EF Core. Nenhum destes testes sobe banco.
/// </summary>
public class CorretoraRepositoryFake : ICorretoraRepository
{
    public List<Corretora> Itens { get; } = new();
    private int _proximoId = 1;

    public Task<List<Corretora>> ListarAsync(CancellationToken ct = default) =>
        Task.FromResult(Itens.OrderBy(c => c.Nome).ToList());

    public Task<Corretora?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExisteAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Itens.Any(c => c.Id == id));

    /// <summary>Definido pelo teste, já que aqui não há tabela de ativos ligada.</summary>
    public Func<int, bool> PossuiAtivos { get; set; } = _ => false;

    public Task<bool> PossuiAtivosAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(PossuiAtivos(id));

    public Task AdicionarAsync(Corretora corretora, CancellationToken ct = default)
    {
        corretora.Id = _proximoId++;
        Itens.Add(corretora);
        return Task.CompletedTask;
    }

    public Task AtualizarAsync(Corretora corretora, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoverAsync(Corretora corretora, CancellationToken ct = default)
    {
        Itens.Remove(corretora);
        return Task.CompletedTask;
    }
}

public class AtivoRepositoryFake : IAtivoRepository
{
    public List<Ativo> Itens { get; } = new();
    private int _proximoId = 1;

    public Task<PagedResult<Ativo>> ListarAsync(AtivoFiltro filtro, CancellationToken ct = default)
    {
        var itens = Itens.Skip(filtro.Pular).Take(filtro.TamanhoPagina).ToList();
        return Task.FromResult(PagedResult<Ativo>.Criar(itens, Itens.Count, filtro.Pagina, filtro.TamanhoPagina));
    }

    public Task<List<Ativo>> ListarMonitoradosPorIntegracaoAsync(string codigoIntegracao, CancellationToken ct = default) =>
        Task.FromResult(Itens
            .Where(a => a.Monitorado && a.Corretora?.CodigoIntegracao == codigoIntegracao)
            .ToList());

    public Task<Ativo?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(a => a.Id == id));

    public Task<bool> ExisteAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Itens.Any(a => a.Id == id));

    public Task<bool> ExisteSimboloNaCorretoraAsync(
        int corretoraId, string simbolo, int? ignorarId = null, CancellationToken ct = default) =>
        Task.FromResult(Itens.Any(a =>
            a.CorretoraId == corretoraId &&
            a.Simbolo == simbolo.ToUpperInvariant() &&
            (ignorarId == null || a.Id != ignorarId)));

    public Task AdicionarAsync(Ativo ativo, CancellationToken ct = default)
    {
        ativo.Id = _proximoId++;
        Itens.Add(ativo);
        return Task.CompletedTask;
    }

    /// <summary>Conta as gravações, para o teste conferir que o espelho foi (ou não) persistido.</summary>
    public int Atualizacoes { get; private set; }

    public Task AtualizarAsync(Ativo ativo, CancellationToken ct = default)
    {
        Atualizacoes++;
        return Task.CompletedTask;
    }

    public Task RemoverAsync(Ativo ativo, CancellationToken ct = default)
    {
        Itens.Remove(ativo);
        return Task.CompletedTask;
    }
}

public class CotacaoRepositoryFake : ICotacaoRepository
{
    public List<Cotacao> Itens { get; } = new();
    private int _proximoId = 1;

    public Task<List<Cotacao>> ListarPorAtivoAsync(
        int ativoId, DateTime? desde, DateTime? ate, int limite, CancellationToken ct = default) =>
        Task.FromResult(Itens
            .Where(c => c.AtivoId == ativoId)
            .Where(c => !desde.HasValue || c.CapturadoEm >= desde.Value)
            .Where(c => !ate.HasValue || c.CapturadoEm <= ate.Value)
            .OrderByDescending(c => c.CapturadoEm)
            .Take(limite)
            .ToList());

    public Task<Cotacao?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(c => c.Id == id));

    public Task<Cotacao?> ObterUltimaDoAtivoAsync(int ativoId, CancellationToken ct = default) =>
        Task.FromResult(Itens.Where(c => c.AtivoId == ativoId)
            .OrderByDescending(c => c.CapturadoEm).FirstOrDefault());

    public Task AdicionarAsync(Cotacao cotacao, CancellationToken ct = default)
    {
        cotacao.Id = _proximoId++;
        Itens.Add(cotacao);
        return Task.CompletedTask;
    }

    public Task RemoverAsync(Cotacao cotacao, CancellationToken ct = default)
    {
        Itens.Remove(cotacao);
        return Task.CompletedTask;
    }
}

public class UsuarioAtualFake : IUsuarioAtual
{
    public Guid Id { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
}
