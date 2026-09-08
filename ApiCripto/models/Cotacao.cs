using System.Text.Json.Serialization;

namespace ApiCripto.models;

/// <summary>
/// Lado "N" do segundo relacionamento 1-N: um Ativo possui várias Cotacoes.
/// Cada linha é uma foto do mercado em um instante (snapshot), nunca é editada
/// depois de gravada — histórico é imutável por natureza.
/// </summary>
public class Cotacao
{
    public int Id { get; set; }

    public decimal Preco { get; set; }

    /// <summary>Variação percentual nas últimas 24h (ex.: -0.347 = -0,347%).</summary>
    public decimal Variacao24h { get; set; }

    public decimal PrecoMaximo24h { get; set; }

    public decimal PrecoMinimo24h { get; set; }

    public decimal Volume24h { get; set; }

    public DateTime CapturadoEm { get; set; }

    /// <summary>Origem do dado: "Binance", "Manual" ou "Seed".</summary>
    public string Fonte { get; set; } = "Manual";

    public int AtivoId { get; set; }

    [JsonIgnore]
    public Ativo? Ativo { get; set; }
}
