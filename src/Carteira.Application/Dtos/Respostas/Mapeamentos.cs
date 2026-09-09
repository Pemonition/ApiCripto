using Carteira.Domain.Consultas;
using Carteira.Domain.Entidades;

namespace Carteira.Application.Dtos.Respostas;

/// <summary>
/// Conversão entidade → resposta, escrita à mão.
///
/// Poderia ser AutoMapper. Não é, de propósito: com três entidades, a
/// biblioteca adiciona uma dependência, configuração e erros que só aparecem
/// em tempo de execução ("Unmapped members were found") para economizar umas
/// vinte linhas que o compilador verifica sozinho. Mapeamento manual só fica
/// caro quando há dezenas de tipos — aí sim vale reconsiderar.
/// </summary>
public static class Mapeamentos
{
    public static CorretoraResposta ParaResposta(this Corretora c) => new(
        c.Id,
        c.Nome,
        c.Site,
        c.PaisSede,
        c.CodigoIntegracao,
        !string.IsNullOrWhiteSpace(c.CodigoIntegracao));

    public static AtivoResposta ParaResposta(this Ativo a) => new(
        a.Id,
        a.Simbolo,
        a.MoedaBase,
        a.MoedaCotacao,
        a.Tipo,
        a.Monitorado,
        a.UltimoPreco,
        a.Variacao24h,
        a.AtualizadoEm,
        a.Corretora is null ? null : new CorretoraResumo(a.Corretora.Id, a.Corretora.Nome));

    public static CotacaoResposta ParaResposta(this Cotacao c) => new(
        c.Id,
        c.AtivoId,
        c.Preco,
        c.Variacao24h,
        c.PrecoMaximo24h,
        c.PrecoMinimo24h,
        c.Volume24h,
        c.CapturadoEm,
        c.Fonte);

    /// <summary>Converte a página inteira preservando os metadados de paginação.</summary>
    public static PagedResult<TDestino> Converter<TOrigem, TDestino>(
        this PagedResult<TOrigem> pagina, Func<TOrigem, TDestino> conversor) =>
        PagedResult<TDestino>.Criar(
            pagina.Itens.Select(conversor).ToList(),
            pagina.Total,
            pagina.Pagina,
            pagina.TamanhoPagina);
}
