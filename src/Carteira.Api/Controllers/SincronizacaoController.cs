using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Application.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace Carteira.Api.Controllers;

/// <summary>
/// Endpoints que puxam preço real da corretora e gravam no banco.
///
/// São POST e não GET porque MUDAM o estado do servidor: cada chamada insere
/// uma cotação. GET, por contrato HTTP, não pode ter efeito colateral e pode
/// ser cacheado por qualquer proxy no caminho.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SincronizacaoController : ControllerBase
{
    private readonly ISincronizacaoService _service;

    public SincronizacaoController(ISincronizacaoService service) => _service = service;

    /// <summary>Busca na corretora o preço atual de UM ativo e grava a cotação.</summary>
    [HttpPost("ativo/{ativoId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CotacaoResposta>> SincronizarAtivo(int ativoId, CancellationToken ct)
    {
        var (cotacao, status, erro) = await _service.SincronizarAtivoAsync(ativoId, ct);

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
    /// Sincroniza todos os ativos monitorados de um provedor.
    /// Sempre 200 com o relatório: falha em um símbolo não invalida o lote.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SincronizacaoResultadoDto>> SincronizarTodos(
        [FromQuery] string provedor = "BINANCE",
        CancellationToken ct = default) =>
        Ok(await _service.SincronizarTodosAsync(provedor, ct));
}
