using Carteira.Domain.Entidades;

namespace Carteira.Domain.Consultas;

/// <summary>
/// Critérios de busca da entidade principal.
///
/// Mora no domínio, e não junto dos DTOs da API, porque "quais ativos eu
/// quero" é uma pergunta do negócio — a interface do repositório depende
/// dela. Se ficasse na camada de aplicação, o domínio dependeria da
/// aplicação e a seta apontaria para o lado errado.
///
/// Todos os campos são opcionais e combinam entre si com AND.
/// Para acrescentar um filtro novo: uma propriedade aqui e um "if" no
/// AtivoRepository. Nada mais no sistema muda.
/// </summary>
public class AtivoFiltro
{
    /// <summary>Ativos de uma corretora específica.</summary>
    public int? CorretoraId { get; set; }

    /// <summary>Busca parcial pelo símbolo, sem diferenciar maiúsculas.</summary>
    public string? Simbolo { get; set; }

    public TipoAtivo? Tipo { get; set; }

    /// <summary>Preço mínimo, comparado contra o campo espelho UltimoPreco.</summary>
    public decimal? PrecoMin { get; set; }

    public decimal? PrecoMax { get; set; }

    /// <summary>Apenas os marcados (ou não) para coleta automática.</summary>
    public bool? Monitorado { get; set; }

    /// <summary>"simbolo" (padrão), "preco" ou "variacao".</summary>
    public string? OrdenarPor { get; set; }

    public bool Desc { get; set; }

    // ---------------------------------------------------------------------
    // Paginação. Os valores chegam do cliente, então são sanitizados aqui e
    // não no repositório: página 0, página negativa e tamanho 100000 são
    // entradas perfeitamente possíveis de quem monta a URL na mão.
    // ---------------------------------------------------------------------

    public const int TamanhoPaginaPadrao = 20;
    public const int TamanhoPaginaMaximo = 100;

    private int _pagina = 1;
    private int _tamanhoPagina = TamanhoPaginaPadrao;

    /// <summary>Página desejada, começando em 1.</summary>
    public int Pagina
    {
        get => _pagina;
        set => _pagina = value < 1 ? 1 : value;
    }

    /// <summary>
    /// Itens por página. O teto existe para ninguém contornar a paginação
    /// pedindo ?tamanhoPagina=999999 — que é exatamente o "SELECT *" que a
    /// paginação veio evitar.
    /// </summary>
    public int TamanhoPagina
    {
        get => _tamanhoPagina;
        set => _tamanhoPagina = value switch
        {
            < 1 => TamanhoPaginaPadrao,
            > TamanhoPaginaMaximo => TamanhoPaginaMaximo,
            _ => value
        };
    }

    /// <summary>Quantos registros pular. Derivado, nunca recebido do cliente.</summary>
    public int Pular => (Pagina - 1) * TamanhoPagina;
}
