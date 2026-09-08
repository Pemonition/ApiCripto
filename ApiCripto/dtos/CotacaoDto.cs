using System.ComponentModel.DataAnnotations;

namespace ApiCripto.dtos;

/// <summary>
/// Entrada de Cotacao manual (POST /api/ativo/{ativoId}/cotacao).
/// O AtivoId não está aqui de propósito: ele vem da rota, que é a fonte da
/// verdade sobre a qual ativo a cotação pertence.
/// </summary>
public class CotacaoDto
{
    [Range(0, double.MaxValue)]
    public decimal Preco { get; set; }

    public decimal Variacao24h { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecoMaximo24h { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecoMinimo24h { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Volume24h { get; set; }

    /// <summary>Opcional. Se omitido, usa o instante da gravação (UTC).</summary>
    public DateTime? CapturadoEm { get; set; }
}
