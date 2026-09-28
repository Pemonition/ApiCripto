using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using ApiCripto.infra;
using ApiCripto.models;
using ApiCripto.services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ApiCripto.security;

public static class SegurancaExtensions
{
    public const string Administrador = "Administrador";
    public const string Sincronizador = "Sincronizador";

    public static IServiceCollection AdicionarSeguranca(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new();
        jwt.Validar();
        services.AddSingleton(jwt);
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddScoped<AuthService>();
        services.AddScoped<FavoritoService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateLifetime = true, RequireExpirationTime = true,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role",
                ClockSkew = TimeSpan.Zero
            };
            // Identidade e perfil precisam continuar válidos no banco, mesmo com token assinado.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var id = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var usuario = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
                    if (usuario is null || usuario.Perfil != context.Principal?.FindFirstValue("role"))
                        context.Fail("Identidade inválida.");
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(Administrador, p => p.RequireRole("Admin"));
            // Negar quando não houver identidade configurada; ser Admin não basta.
            options.AddPolicy(Sincronizador, p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
            {
                var permitido = config["Autorizacao:SincronizadorUsuarioId"];
                return !string.IsNullOrWhiteSpace(permitido)
                    && ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) == permitido;
            }));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
        });
        return services;
    }
}
