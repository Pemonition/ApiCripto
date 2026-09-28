using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApiCripto.dtos;
using ApiCripto.services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiCripto.controllers;

[Authorize]
[ApiController]
[Route("api/favoritos")]
public class FavoritosController(FavoritoService service) : ControllerBase
{
    private string Dono => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await service.ListarAsync(Dono));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id)
    {
        var item = await service.ObterAsync(Dono, id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(FavoritoDto dto)
    {
        var (item, erro) = await service.CriarAsync(Dono, dto);
        return item is null ? BadRequest(new { mensagem = erro })
            : CreatedAtAction(nameof(Obter), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, NotaFavoritoDto dto) =>
        await service.AtualizarAsync(Dono, id, dto.Nota) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id) =>
        await service.RemoverAsync(Dono, id) ? NoContent() : NotFound();
}
