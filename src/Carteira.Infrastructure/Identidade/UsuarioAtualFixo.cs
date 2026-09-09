using Carteira.Application.Comum;

namespace Carteira.Infrastructure.Identidade;

/// <summary>
/// Implementação provisória de <see cref="IUsuarioAtual"/>: devolve sempre o
/// mesmo usuário, porque ainda não há autenticação.
///
/// Isto é uma COSTURA, não uma gambiarra. O sistema inteiro já se comporta
/// como multiusuário — o filtro global do DbContext, o UsuarioId nos
/// agregados, os services carimbando o dono ao criar. Só a origem do Id é
/// falsa.
///
/// Quando entrar JWT, esta classe é substituída por uma que lê a claim do
/// token (via IHttpContextAccessor) e o registro no Program.cs muda de linha.
/// Nada mais no sistema precisa saber que isso aconteceu.
/// </summary>
public class UsuarioAtualFixo : IUsuarioAtual
{
    /// <summary>
    /// Guid fixo e conhecido. O seed usa o mesmo valor, senão os dados
    /// iniciais nasceriam invisíveis para o filtro global.
    /// </summary>
    public static readonly Guid UsuarioPadrao = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid Id => UsuarioPadrao;
}
