using Microsoft.AspNetCore.Authorization;
using ApiCripto.security;
using ApiCripto.dtos;
using ApiCripto.models;
using ApiCripto.services;
using Microsoft.AspNetCore.Mvc;

namespace ApiCripto.controllers;

[ApiController]
[Route("api/[controller]")] // -> /api/ativo
[Produces("application/json")]
public class AtivoController : ControllerBase
{
    private readonly IAtivoService _service;

    public AtivoController(IAtivoService service) => _service = service;

    /// <summary>
    /// Lista os ativos aplicando os filtros da query string.
    ///
    /// [FromQuery] em um objeto faz o ASP.NET mapear cada propriedade de
    /// AtivoFiltro para um parâmetro da URL automaticamente. Exemplos:
    ///   /api/ativo?corretoraId=1
    ///   /api/ativo?simbolo=btc
    ///   /api/ativo?tipo=Stablecoin
    ///   /api/ativo?precoMin=100&amp;precoMax=5000
    ///   /api/ativo?monitorado=true&amp;ordenarPor=preco&amp;desc=true
    /// Todos combináveis entre si.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Ativo>>> Listar([FromQuery] AtivoFiltro filtro) =>
        Ok(await _service.ListarAsync(filtro));

    /// <summary>Busca um ativo pelo Id (com a corretora dele).</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Ativo>> Obter(int id)
    {
        var ativo = await _service.ObterPorIdAsync(id);
        return ativo is null ? NotFound() : Ok(ativo);
    }

    /// <summary>Cadastra um ativo em uma corretora.</summary>
    [Authorize(Policy = SegurancaExtensions.Administrador)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Ativo>> Criar(AtivoDto dto)
    {
        var (criado, erro) = await _service.CriarAsync(dto);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        return CreatedAtAction(nameof(Obter), new { id = criado!.Id }, criado);
    }

    /// <summary>Atualiza o cadastro do ativo (não mexe em preço).</summary>
    [Authorize(Policy = SegurancaExtensions.Administrador)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(int id, AtivoDto dto)
    {
        var (atualizado, erro) = await _service.AtualizarAsync(id, dto);

        if (atualizado) return NoContent();
        return erro is null ? NotFound() : BadRequest(new { mensagem = erro });
    }

    /// <summary>Remove o ativo e, em cascata, todo o histórico de cotações dele.</summary>
    [Authorize(Policy = SegurancaExtensions.Administrador)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int id)
    {
        var removido = await _service.RemoverAsync(id);
        return removido ? NoContent() : NotFound();
    }
}
