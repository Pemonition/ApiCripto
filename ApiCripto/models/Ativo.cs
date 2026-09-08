using System.Text.Json.Serialization;

namespace ApiCripto.models;

/// <summary>
/// ENTIDADE PRINCIPAL da API (é sobre ela que ficam os filtros).
/// É lado "N" de Corretora e lado "1" de Cotacao.
/// </summary>
public class Ativo
{
    public int Id { get; set; }

    /// <summary>Par negociado, no formato da corretora. Ex.: "BTCUSDT".</summary>
    public string Simbolo { get; set; } = string.Empty;

    /// <summary>Moeda que está sendo comprada. Ex.: "BTC".</summary>
    public string MoedaBase { get; set; } = string.Empty;

    /// <summary>Moeda em que o preço é expresso. Ex.: "USDT".</summary>
    public string MoedaCotacao { get; set; } = string.Empty;

    public TipoAtivo Tipo { get; set; }

    /// <summary>Se true, entra na sincronização em lote com a corretora.</summary>
    public bool Monitorado { get; set; } = true;

    // ---------------------------------------------------------------------
    // Campos "espelho" da última cotação (desnormalização proposital).
    //
    // Poderiam ser calculados com um subselect na tabela Cotacoes a cada
    // consulta, mas os filtros de faixa de preço rodariam sobre esse subselect
    // em TODA listagem. Guardar o último valor aqui deixa o filtro
    // precoMin/precoMax ser um WHERE simples e indexável.
    //
    // Preço da consistência: quem gravar uma Cotacao PRECISA atualizar estes
    // campos. Hoje isso acontece em um único lugar (CotacaoService.CriarAsync
    // e SincronizacaoService) — mantenha assim.
    // ---------------------------------------------------------------------
    public decimal? UltimoPreco { get; set; }

    public decimal? Variacao24h { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    // Relacionamento N-1 com Corretora (chave estrangeira + navegação)
    public int CorretoraId { get; set; }
    public Corretora? Corretora { get; set; }

    // Relacionamento 1-N com Cotacao.
    // [JsonIgnore] porque o histórico cresce sem limite: ele é lido pelo
    // endpoint dedicado GET /api/ativo/{ativoId}/cotacao, com paginação por data,
    // e nunca embutido na listagem de ativos.
    [JsonIgnore]
    public ICollection<Cotacao> Cotacoes { get; set; } = new List<Cotacao>();
}
