namespace Carteira.Domain.Entidades;

/// <summary>
/// Foto do mercado em um instante. Lado "N" do segundo relacionamento 1-N.
///
/// Nunca é editada depois de gravada: histórico é imutável por natureza — e é
/// por isso que não existe operação de UPDATE para cotação em lugar nenhum do
/// sistema, só INSERT e DELETE.
/// </summary>
public class Cotacao
{
    public int Id { get; set; }

    public decimal Preco { get; set; }

    /// <summary>Variação percentual nas últimas 24h (-0.347 = -0,347%).</summary>
    public decimal Variacao24h { get; set; }

    public decimal PrecoMaximo24h { get; set; }

    public decimal PrecoMinimo24h { get; set; }

    public decimal Volume24h { get; set; }

    public DateTime CapturadoEm { get; set; }

    /// <summary>Origem do dado: "Binance", "Manual" ou "Seed".</summary>
    public string Fonte { get; set; } = "Manual";

    public int AtivoId { get; set; }
    public Ativo? Ativo { get; set; }
}
