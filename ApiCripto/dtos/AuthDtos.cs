using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ApiCripto.dtos;

// Rejeitar campos extras impede que o cliente tente escolher perfil ou identidade.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CredenciaisDto
{
    [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,80}$")]
    public string Login { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)]
    public string Senha { get; set; } = "";
}
public record UsuarioDto(string Id, string Login, string Perfil);
public record TokenDto(string Token, DateTime ExpiraEm, UsuarioDto Usuario);
