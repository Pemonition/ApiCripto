using System.ComponentModel.DataAnnotations;

namespace ApiCripto.dtos;

/// <summary>
/// Entrada de Corretora. Existe para o cliente não conseguir mandar Id nem a
/// coleção de Ativos no corpo do POST/PUT (over-posting).
/// </summary>
public class CorretoraDto
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Site { get; set; }

    [StringLength(60)]
    public string? PaisSede { get; set; }

    /// <summary>"BINANCE" para habilitar a sincronização automática; nulo caso contrário.</summary>
    [StringLength(30)]
    public string? CodigoIntegracao { get; set; }
}
