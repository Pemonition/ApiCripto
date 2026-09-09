using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Application.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace Carteira.Api.Controllers;

/// <summary>
/// Rota ANINHADA: a cotação não existe fora de um ativo, e a URL diz isso.
/// /api/ativo/3/cotacao já deixa claro "as cotações do ativo 3".
/// </summary>
[ApiController]
[Route("api/ativo/{ativoId:int}/[controller]")]
[Produces("application/json")]
public class CotacaoController : ControllerBase
{
    private readonly ICotacaoService _service;

    public CotacaoController(ICotacaoService service) => _service = service;

    /// <summary>
    /// Histórico do ativo, do mais recente para o mais antigo.
    /// Ex.: /api/ativo/1/cotacao?desde=2026-09-01&amp;limite=50
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CotacaoResposta>>> Listar(
        int ativoId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        [FromQuery] int limite,
        CancellationToken ct)
    {
        var cotacoes = await _service.ListarPorAtivoAsync(ativoId, desde, ate, limite, ct);

        // null = ativo inexistente (404); lista vazia = ativo sem histórico (200 []).
        return cotacoes is null ? NotFound() : Ok(cotacoes);
    }

    /// <summary>Busca uma cotação específica do ativo.</summary>
    [HttpGet("{id:int}", Name = "ObterCotacao")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CotacaoResposta>> Obter(int ativoId, int id, CancellationToken ct)
    {
        var cotacao = await _service.ObterAsync(id, ct);

        // Confere se a cotação pertence mesmo ao ativo da rota — sem isso,
        // /api/ativo/1/cotacao/99 devolveria a cotação do ativo 7.
        if (cotacao is null || cotacao.AtivoId != ativoId) return NotFound();

        return Ok(cotacao);
    }

    /// <summary>Lança uma cotação manualmente (corretora sem integração automática).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CotacaoResposta>> Criar(int ativoId, CotacaoDto dto, CancellationToken ct)
    {
        var (criada, erro) = await _service.CriarAsync(ativoId, dto, ct);

        if (erro is not null) return BadRequest(new { mensagem = erro });
        if (criada is null) return NotFound(new { mensagem = $"Ativo {ativoId} não encontrado." });

        return CreatedAtRoute("ObterCotacao", new { ativoId, id = criada.Id }, criada);
    }

    /// <summary>Remove uma cotação do histórico.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int ativoId, int id, CancellationToken ct)
    {
        var cotacao = await _service.ObterAsync(id, ct);
        if (cotacao is null || cotacao.AtivoId != ativoId) return NotFound();

        await _service.RemoverAsync(id, ct);
        return NoContent();
    }
}
