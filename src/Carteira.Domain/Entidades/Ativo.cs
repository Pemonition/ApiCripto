namespace Carteira.Domain.Entidades;

/// <summary>
/// ENTIDADE PRINCIPAL. Um instrumento negociável dentro de uma corretora:
/// um par de cripto (BTCUSDT), uma ação (PETR4), um FII (HGLG11).
///
/// É lado "N" de Corretora e lado "1" de Cotacao.
/// </summary>
public class Ativo
{
    public int Id { get; set; }

    /// <summary>Código do instrumento na corretora. Ex.: "BTCUSDT", "HGLG11".</summary>
    public string Simbolo { get; set; } = string.Empty;

    /// <summary>O que está sendo comprado. Ex.: "BTC".</summary>
    public string MoedaBase { get; set; } = string.Empty;

    /// <summary>Moeda em que o preço é expresso. Ex.: "USDT", "BRL".</summary>
    public string MoedaCotacao { get; set; } = string.Empty;

    public TipoAtivo Tipo { get; set; }

    /// <summary>Se true, entra na coleta automática de preços.</summary>
    public bool Monitorado { get; set; } = true;

    // ---------------------------------------------------------------------
    // Campos espelho: cópia da cotação mais recente, guardada aqui de
    // propósito. Sem eles, o filtro de faixa de preço rodaria sobre um
    // subselect na tabela de cotações em TODA listagem.
    //
    // O preço dessa escolha é o risco de divergirem do histórico. A defesa é
    // existir um único ponto de escrita: CotacaoService.RegistrarAsync.
    // ---------------------------------------------------------------------
    public decimal? UltimoPreco { get; set; }

    public decimal? Variacao24h { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public int CorretoraId { get; set; }
    public Corretora? Corretora { get; set; }

    public Guid UsuarioId { get; set; }

    public ICollection<Cotacao> Cotacoes { get; set; } = new List<Cotacao>();
}
