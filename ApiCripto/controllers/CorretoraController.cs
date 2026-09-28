using Microsoft.AspNetCore.Authorization;
using ApiCripto.security;
using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.services;
using Microsoft.AspNetCore.Mvc;

namespace ApiCripto.controllers;

/// <summary>
/// Camada HTTP. Só traduz: request -> chamada de service -> status code.
/// Nenhuma regra de negócio mora aqui.
/// </summary>
[ApiController]
[Route("api/[controller]")] // -> /api/corretora
[Produces("application/json")]
public class CorretoraController : ControllerBase
{
    private readonly ICorretoraService _service;

    public CorretoraController(ICorretoraService service) => _service = service;

    /// <summary>Lista todas as corretoras.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Corretora>>> Listar() =>
        Ok(await _service.ListarAsync());

    /// <summary>Busca uma corretora pelo Id.</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Corretora>> Obter(int id)
    {
        var corretora = await _service.ObterPorIdAsync(id);
        return corretora is null ? NotFound() : Ok(corretora);
    }

    /// <summary>Cadastra uma corretora.</summary>
    [Authorize(Policy = SegurancaExtensions.Administrador)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Corretora>> Criar(CorretoraDto dto)
    {
        var criada = await _service.CriarAsync(dto);

        // 201 + header Location apontando para o GET do recurso criado.
        return CreatedAtAction(nameof(Obter), new { id = criada.Id }, criada);
    }

    /// <summary>Atualiza uma corretora.</summary>
    [Authorize(Policy = SegurancaExtensions.Administrador)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(int id, CorretoraDto dto)
    {
        var atualizado = await _service.AtualizarAsync(id, dto);
        return atualizado ? NoContent() : NotFound();
    }

    /// <summary>Remove uma corretora (só se ela não tiver ativos).</summary>
    [Authorize(Policy = SegurancaExtensions.Administrador)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int id)
    {
        var (removido, erro) = await _service.RemoverAsync(id);

        if (removido) return NoContent();

        // erro null => o registro não existe (404).
        // erro preenchido => existe, mas a regra de negócio proibiu (400).
        return erro is null ? NotFound() : BadRequest(new { mensagem = erro });
    }
}
