namespace Carteira.Application.Comum;

/// <summary>
/// Quem é o dono dos dados desta requisição.
///
/// Hoje existe uma implementação fixa (sempre o mesmo usuário), porque ainda
/// não há autenticação. O ponto é que o resto do sistema já pergunta "quem é
/// o usuário?" para uma abstração em vez de assumir que só existe um.
///
/// Quando entrar JWT, muda-se UMA classe na infraestrutura — que passa a ler
/// a claim do token — e nenhuma outra linha do sistema. Sem essa costura, a
/// mesma mudança viraria uma varredura por todos os repositórios e services.
/// </summary>
public interface IUsuarioAtual
{
    Guid Id { get; }
}
