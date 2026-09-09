namespace Carteira.Domain.Entidades;

/// <summary>
/// Classificação do instrumento. Os valores são numerados explicitamente
/// porque o número é o que vai para o banco: deixar o compilador escolher
/// significa que inserir um item no meio do enum reclassifica silenciosamente
/// todas as linhas já gravadas.
/// </summary>
public enum TipoAtivo
{
    Cripto = 0,
    Stablecoin = 1,
    Token = 2,
    Acao = 3,
    FII = 4,
    RendaFixa = 5
}
