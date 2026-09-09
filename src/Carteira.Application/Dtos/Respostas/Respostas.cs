using Carteira.Domain.Entidades;

namespace Carteira.Application.Dtos.Respostas;

/// <summary>
/// Formatos de SAÍDA da API.
///
/// Por que não devolver a entidade direto, como no projeto de aula:
///
/// 1. UsuarioId nunca pode vazar na resposta. Devolvendo a entidade, basta
///    alguém adicionar um campo sensível no futuro para ele ir junto sem
///    ninguém perceber.
/// 2. As entidades tinham [JsonIgnore] espalhado só para quebrar ciclos de
///    serialização — ou seja, uma decisão de API contaminando o domínio.
///    Com DTO de saída esses atributos somem, e o ReferenceHandler.IgnoreCycles
///    do Program.cs deixa de ser necessário.
/// 3. Mudar o contrato da API deixa de exigir mudar o banco. Hoje são quase
///    iguais; é justamente na primeira vez que precisarem divergir que a
///    separação se paga.
///
/// São records porque resposta é dado imutável: montou, serializou, acabou.
/// </summary>
public record CorretoraResposta(
    int Id,
    string Nome,
    string? Site,
    string? PaisSede,
    string? CodigoIntegracao,
    bool TemIntegracaoAutomatica);

/// <summary>Resumo da corretora embutido na resposta do ativo — sem repetir a lista de ativos.</summary>
public record CorretoraResumo(int Id, string Nome);

public record AtivoResposta(
    int Id,
    string Simbolo,
    string MoedaBase,
    string MoedaCotacao,
    TipoAtivo Tipo,
    bool Monitorado,
    decimal? UltimoPreco,
    decimal? Variacao24h,
    DateTime? AtualizadoEm,
    CorretoraResumo? Corretora);

public record CotacaoResposta(
    int Id,
    int AtivoId,
    decimal Preco,
    decimal Variacao24h,
    decimal PrecoMaximo24h,
    decimal PrecoMinimo24h,
    decimal Volume24h,
    DateTime CapturadoEm,
    string Fonte);
