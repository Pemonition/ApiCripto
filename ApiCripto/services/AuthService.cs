using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiCripto.dtos;
using ApiCripto.infra;
using ApiCripto.models;
using ApiCripto.security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ApiCripto.services;

public class AuthService(AppDbContext db, IPasswordHasher<Usuario> hasher, JwtOptions jwt)
{
    public async Task<UsuarioDto?> RegistrarAsync(CredenciaisDto dto, string perfil = "Usuario")
    {
        var login = dto.Login.ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Login == login)) return null;
        var usuario = new Usuario { Login = login, Perfil = perfil };
        usuario.SenhaHash = hasher.HashPassword(usuario, dto.Senha);
        db.Usuarios.Add(usuario);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        {
            db.Entry(usuario).State = EntityState.Detached;
            return null;
        }
        return new(usuario.Id, usuario.Login, usuario.Perfil);
    }

    public async Task<TokenDto?> LoginAsync(CredenciaisDto dto)
    {
        var login = dto.Login.ToLowerInvariant();
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Login == login);
        if (usuario is null) return null;
        var resultado = hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, dto.Senha);
        if (resultado == PasswordVerificationResult.Failed) return null;
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = hasher.HashPassword(usuario, dto.Senha);
            await db.SaveChangesAsync();
        }
        var expira = DateTime.UtcNow.AddMinutes(jwt.Minutos);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, usuario.Id),
                new Claim("role", usuario.Perfil), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) },
            notBefore: DateTime.UtcNow, expires: expira,
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expira,
            new(usuario.Id, usuario.Login, usuario.Perfil));
    }
}
