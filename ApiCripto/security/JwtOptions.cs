using System.Text;
namespace ApiCripto.security;

public class JwtOptions
{
    public string Issuer { get; set; } = "ApiCripto";
    public string Audience { get; set; } = "ApiCripto.Clientes";
    public string Key { get; set; } = "";
    public int Minutos { get; set; } = 30;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Key) || Encoding.UTF8.GetByteCount(Key) < 32 || string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience) || Minutos is < 1 or > 60)
            throw new InvalidOperationException("Configure Jwt:Key (ao menos 32 bytes), Issuer, Audience e Minutos (1–60). Use variáveis de ambiente ou user-secrets.");
    }
}
