using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApiCripto.dtos;
using ApiCripto.infra;
using ApiCripto.services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ApiCripto.controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService service, AppDbContext db) : ControllerBase
{
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("registrar")]
    public async Task<IActionResult> Registrar(CredenciaisDto dto)
    {
        var usuario = await service.RegistrarAsync(dto);
        return usuario is null ? Conflict(new { mensagem = "Login indisponível." })
            : Created("/api/auth/me", usuario);
    }

    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("login")]
    public async Task<IActionResult> Login(CredenciaisDto dto)
    {
        var token = await service.LoginAsync(dto);
        return token is null ? Unauthorized(new { mensagem = "Login ou senha inválidos." }) : Ok(token);
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        return Ok(await db.Usuarios.Where(u => u.Id == id)
            .Select(u => new UsuarioDto(u.Id, u.Login, u.Perfil)).SingleAsync());
    }
}
