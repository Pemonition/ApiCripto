using System.ComponentModel.DataAnnotations;

namespace Carteira.Application.Dtos;

/// <summary>
/// Entrada de Corretora. Existe para o cliente não conseguir enviar Id,
/// UsuarioId nem a coleção de Ativos no corpo da requisição (over-posting).
/// </summary>
public class CorretoraDto
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Site { get; set; }

    [StringLength(60)]
    public string? PaisSede { get; set; }

    /// <summary>"BINANCE" habilita a coleta automática; nulo deixa só manual.</summary>
    [StringLength(30)]
    public string? CodigoIntegracao { get; set; }
}
