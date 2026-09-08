using ApiCripto.models;

namespace ApiCripto.dtos;

/// <summary>
/// Filtros da entidade principal, recebidos por query string
/// ([FromQuery] no controller). Agrupar em uma classe em vez de espalhar
/// seis parâmetros na assinatura mantém o controller legível e faz o
/// próximo filtro ser uma propriedade nova aqui, sem mudar assinatura nenhuma.
///
/// Todos são opcionais e combináveis (AND entre eles).
/// Ex.: /api/ativo?corretoraId=1&amp;tipo=Cripto&amp;precoMin=1000&amp;ordenarPor=preco
/// </summary>
public class AtivoFiltro
{
    /// <summary>Filtro 1 — ativos de uma corretora específica.</summary>
    public int? CorretoraId { get; set; }

    /// <summary>Filtro 2 — busca parcial pelo símbolo (case-insensitive). Ex.: "btc".</summary>
    public string? Simbolo { get; set; }

    /// <summary>Filtro 3 — classificação do ativo (Cripto, Stablecoin, Token).</summary>
    public TipoAtivo? Tipo { get; set; }

    /// <summary>Filtro 4 — preço mínimo (usa o campo espelho Ativo.UltimoPreco).</summary>
    public decimal? PrecoMin { get; set; }

    /// <summary>Filtro 5 — preço máximo.</summary>
    public decimal? PrecoMax { get; set; }

    /// <summary>Filtro 6 — apenas ativos marcados (ou não) para sincronização.</summary>
    public bool? Monitorado { get; set; }

    /// <summary>Ordenação: "simbolo" (padrão), "preco", "variacao".</summary>
    public string? OrdenarPor { get; set; }

    /// <summary>true = ordem decrescente.</summary>
    public bool Desc { get; set; }
}
