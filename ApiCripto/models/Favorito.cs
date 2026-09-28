namespace ApiCripto.models;

public class Favorito
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public int AtivoId { get; set; }
    public string? Nota { get; set; }
}
