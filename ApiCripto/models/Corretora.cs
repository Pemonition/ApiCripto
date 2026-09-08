using System.Text.Json.Serialization;

namespace ApiCripto.models;

/// <summary>
/// Lado "1" do primeiro relacionamento 1-N: uma Corretora possui vários Ativos.
/// </summary>
public class Corretora
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Site { get; set; }

    public string? PaisSede { get; set; }

    /// <summary>
    /// Código usado para escolher qual cliente HTTP externo sabe buscar preços
    /// desta corretora (ex.: "BINANCE"). Nulo = corretora sem integração,
    /// os preços só entram manualmente. É o que permite plugar uma segunda
    /// corretora no futuro sem mexer no resto do sistema.
    /// </summary>
    public string? CodigoIntegracao { get; set; }

    // [JsonIgnore] evita que a lista de ativos volte junto em toda resposta de Ativo
    // (Ativo -> Corretora -> Ativos -> ...). A navegação continua existindo para o EF.
    [JsonIgnore]
    public ICollection<Ativo> Ativos { get; set; } = new List<Ativo>();
}
