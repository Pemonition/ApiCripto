using System.ComponentModel.DataAnnotations;
using ApiCripto.models;

namespace ApiCripto.dtos;

/// <summary>
/// Entrada de Ativo. Repare no que NÃO está aqui: Id, UltimoPreco, Variacao24h,
/// AtualizadoEm e Cotacoes. Esses campos são derivados do mercado — quem os
/// escreve é a sincronização, nunca o cliente HTTP.
/// </summary>
public class AtivoDto
{
    [Required, StringLength(20, MinimumLength = 3)]
    public string Simbolo { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string MoedaBase { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string MoedaCotacao { get; set; } = string.Empty;

    public TipoAtivo Tipo { get; set; } = TipoAtivo.Cripto;

    public bool Monitorado { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "Informe a corretora dona do ativo.")]
    public int CorretoraId { get; set; }
}
