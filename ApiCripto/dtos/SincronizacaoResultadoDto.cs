namespace ApiCripto.dtos;

/// <summary>Resumo devolvido pela sincronização em lote.</summary>
public class SincronizacaoResultadoDto
{
    public int TotalConsiderados { get; set; }
    public int Sincronizados { get; set; }
    public int ComFalha { get; set; }
    public DateTime ExecutadoEm { get; set; } = DateTime.UtcNow;
    public List<ItemSincronizacaoDto> Itens { get; set; } = new();
}

public class ItemSincronizacaoDto
{
    public int AtivoId { get; set; }
    public string Simbolo { get; set; } = string.Empty;
    public bool Sucesso { get; set; }
    public decimal? Preco { get; set; }
    public string? Erro { get; set; }
}
