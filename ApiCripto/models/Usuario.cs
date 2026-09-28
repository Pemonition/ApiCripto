using System.Text.Json.Serialization;
namespace ApiCripto.models;

public class Usuario
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Login { get; set; } = "";
    [JsonIgnore] public string SenhaHash { get; set; } = "";
    public string Perfil { get; set; } = "Usuario";
}
