namespace ApiCripto.models;

/// <summary>
/// Classificação do ativo. Serializado como texto ("Cripto") e não como número,
/// graças ao JsonStringEnumConverter registrado no Program.cs.
/// </summary>
public enum TipoAtivo
{
    Cripto = 0,
    Stablecoin = 1,
    Token = 2
}
