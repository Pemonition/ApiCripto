using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Application.Servicos;
using Carteira.Domain.Consultas;
using Microsoft.AspNetCore.Mvc;

namespace Carteira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AtivoController : ControllerBase
{
    private readonly IAtivoService _service;

    public AtivoController(IAtivoService service) => _service = service;

    /// <summary>
    /// Lista os ativos aplicando os filtros da query string, paginado.
    ///
    /// [FromQuery] em um objeto faz o ASP.NET mapear cada propriedade de
    /// AtivoFiltro para um parâmetro da URL automaticamente. Exemplos:
    ///   /api/ativo?corretoraId=1
    ///   /api/ativo?simbolo=btc
    ///   /api/ativo?tipo=Stablecoin
    ///   /api/ativo?precoMin=100&amp;precoMax=5000
    ///   /api/ativo?monitorado=true&amp;ordenarPor=preco&amp;desc=true
    ///   /api/ativo?pagina=2&amp;tamanhoPagina=50
    /// Todos combináveis entre si.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AtivoResposta>>> Listar(
        [FromQuery] AtivoFiltro filtro, CancellationToken ct) =>
        Ok(await _service.ListarAsync(filtro, ct));

    /// <summary>Busca um ativo pelo Id, com a corretora dele.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AtivoResposta>> Obter(int id, CancellationToken ct)
    {
        var ativo = await _service.ObterPorIdAsync(id, ct);
        return ativo is null ? NotFound() : Ok(ativo);
    }

    /// <summary>Cadastra um ativo em uma corretora.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AtivoResposta>> Criar(AtivoDto dto, CancellationToken ct)
    {
        var (criado, erro) = await _service.CriarAsync(dto, ct);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        return CreatedAtAction(nameof(Obter), new { id = criado!.Id }, criado);
    }

    /// <summary>Atualiza o cadastro do ativo. Não mexe em preço.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(int id, AtivoDto dto, CancellationToken ct)
    {
        var (atualizado, erro) = await _service.AtualizarAsync(id, dto, ct);

        if (atualizado) return NoContent();
        return erro is null ? NotFound() : BadRequest(new { mensagem = erro });
    }

    /// <summary>Remove o ativo e, em cascata, todo o histórico de cotações dele.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct) =>
        await _service.RemoverAsync(id, ct) ? NoContent() : NotFound();
}
