using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ApiCripto.dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class FavoritoDto
{
    [Range(1, int.MaxValue)] public int AtivoId { get; set; }
    [StringLength(300)] public string? Nota { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class NotaFavoritoDto
{
    [StringLength(300)] public string? Nota { get; set; }
}
