using Microsoft.AspNetCore.Authorization;
using ApiCripto.security;
using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.services;
using Microsoft.AspNetCore.Mvc;

namespace ApiCripto.controllers;

/// <summary>
/// Endpoints que puxam preço real da corretora e gravam no banco.
///
/// São POST (e não GET) porque MUDAM o estado do servidor: cada chamada
/// insere uma Cotacao nova. GET, por contrato HTTP, não pode ter efeito
/// colateral e pode ser cacheado por proxies.
/// </summary>
[Authorize(Policy = SegurancaExtensions.Sincronizador)]
[ApiController]
[Route("api/[controller]")] // -> /api/sincronizacao
[Produces("application/json")]
public class SincronizacaoController : ControllerBase
{
    private readonly ISincronizacaoService _service;

    public SincronizacaoController(ISincronizacaoService service) => _service = service;

    /// <summary>
    /// Busca na Binance o preço atual de UM ativo e grava a cotação.
    /// POST /api/sincronizacao/ativo/1
    /// </summary>
    [HttpPost("ativo/{ativoId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<Cotacao>> SincronizarAtivo(
        int ativoId, CancellationToken cancellationToken)
    {
        var (cotacao, status, erro) =
            await _service.SincronizarAtivoAsync(ativoId, cancellationToken);

        // Cada desfecho do domínio vira um status HTTP com significado próprio.
        // 502 Bad Gateway é o certo quando ESTA API está de pé mas o serviço
        // externo do qual ela depende falhou: 500 diria "o bug é meu" e 400
        // diria "a culpa é do cliente" — nenhum dos dois seria verdade.
        return status switch
        {
            StatusSincronizacao.Sucesso => Ok(cotacao),
            StatusSincronizacao.AtivoNaoEncontrado => NotFound(new { mensagem = erro }),
            StatusSincronizacao.SemIntegracao => BadRequest(new { mensagem = erro }),
            _ => StatusCode(StatusCodes.Status502BadGateway, new { mensagem = erro })
        };
    }

    /// <summary>
    /// Sincroniza todos os ativos monitorados de corretoras integradas.
    /// POST /api/sincronizacao
    /// Sempre devolve 200 com o relatório: falha em um símbolo não invalida o lote.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SincronizacaoResultadoDto>> SincronizarTodos(
        CancellationToken cancellationToken) =>
        Ok(await _service.SincronizarTodosAsync(cancellationToken));
}
