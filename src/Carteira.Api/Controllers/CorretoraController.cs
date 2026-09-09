using Carteira.Application.Dtos;
using Carteira.Application.Dtos.Respostas;
using Carteira.Application.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace Carteira.Api.Controllers;

/// <summary>
/// Camada HTTP. Só traduz: requisição -> chamada de service -> status code.
/// Nenhuma regra de negócio mora aqui.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CorretoraController : ControllerBase
{
    private readonly ICorretoraService _service;

    public CorretoraController(ICorretoraService service) => _service = service;

    /// <summary>Lista todas as corretoras do usuário.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CorretoraResposta>>> Listar(CancellationToken ct) =>
        Ok(await _service.ListarAsync(ct));

    /// <summary>Busca uma corretora pelo Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CorretoraResposta>> Obter(int id, CancellationToken ct)
    {
        var corretora = await _service.ObterPorIdAsync(id, ct);
        return corretora is null ? NotFound() : Ok(corretora);
    }

    /// <summary>Cadastra uma corretora.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CorretoraResposta>> Criar(CorretoraDto dto, CancellationToken ct)
    {
        var criada = await _service.CriarAsync(dto, ct);

        // 201 + header Location apontando para o GET do recurso criado.
        return CreatedAtAction(nameof(Obter), new { id = criada.Id }, criada);
    }

    /// <summary>Atualiza uma corretora.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(int id, CorretoraDto dto, CancellationToken ct) =>
        await _service.AtualizarAsync(id, dto, ct) ? NoContent() : NotFound();

    /// <summary>Remove uma corretora — só se ela não tiver ativos vinculados.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        var (removido, erro) = await _service.RemoverAsync(id, ct);

        if (removido) return NoContent();

        // erro null  => o registro não existe (404).
        // erro cheio => existe, mas a regra de negócio proibiu (400).
        return erro is null ? NotFound() : BadRequest(new { mensagem = erro });
    }
}
