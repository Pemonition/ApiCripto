namespace Carteira.Domain.Consultas;

/// <summary>
/// Uma página de resultados mais o total, para o cliente saber quantas páginas
/// existem sem precisar buscar tudo.
///
/// Mantive o nome em inglês (PagedResult) porque é o termo do mercado e é o
/// que o professor usa na aula nova — vale mais casar o vocabulário do que
/// forçar tradução.
///
/// POR QUE PAGINAR: sem isso, GET /api/ativo é um "SELECT *" disfarçado. Hoje
/// com 7 ativos ninguém sente; com 5 mil, a API monta 5 mil objetos em
/// memória, serializa alguns megabytes de JSON e o cliente recebe uma lista
/// que ninguém vai ler inteira. Paginação não é feature de conforto, é o que
/// impede uma consulta de derrubar o processo.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Itens { get; init; } = Array.Empty<T>();

    /// <summary>Total de registros que casam com o filtro, ignorando a página.</summary>
    public int Total { get; init; }

    public int Pagina { get; init; }

    public int TamanhoPagina { get; init; }

    /// <summary>
    /// Calculado, não armazenado: guardar um valor que sempre pode ser
    /// derivado de outros dois é convite para os três divergirem.
    /// </summary>
    public int TotalPaginas => TamanhoPagina <= 0
        ? 0
        : (int)Math.Ceiling(Total / (double)TamanhoPagina);

    public bool TemProxima => Pagina < TotalPaginas;

    public bool TemAnterior => Pagina > 1;

    public static PagedResult<T> Criar(IReadOnlyList<T> itens, int total, int pagina, int tamanhoPagina) =>
        new() { Itens = itens, Total = total, Pagina = pagina, TamanhoPagina = tamanhoPagina };
}
