namespace Carteira.Domain.Entidades;

/// <summary>
/// Instituição onde os ativos são negociados e custodiados: uma exchange de
/// cripto (Binance) ou uma corretora tradicional (XP, Rico, Clear).
///
/// Lado "1" do primeiro relacionamento 1-N.
/// </summary>
public class Corretora
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Site { get; set; }

    public string? PaisSede { get; set; }

    /// <summary>
    /// Código que escolhe qual provedor de cotação sabe buscar preços desta
    /// corretora (ex.: "BINANCE"). Nulo = sem integração automática, os preços
    /// entram manualmente.
    ///
    /// É este campo, e não um "if" espalhado pelo código, que permite plugar
    /// uma segunda fonte de dados sem tocar no resto do sistema.
    /// </summary>
    public string? CodigoIntegracao { get; set; }

    /// <summary>
    /// Dono do registro. Existe desde a primeira versão, antes de haver
    /// autenticação, porque acrescentar isolamento por usuário depois que a
    /// base tem dados é uma migration cara e arriscada — e o filtro global
    /// no AppDbContext depende dele.
    /// </summary>
    public Guid UsuarioId { get; set; }

    public ICollection<Ativo> Ativos { get; set; } = new List<Ativo>();
}
