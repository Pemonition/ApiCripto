using ApiCripto.dtos;
using ApiCripto.infra;
using ApiCripto.models;
using Microsoft.EntityFrameworkCore;

namespace ApiCripto.services;

// Todas as consultas ficam limitadas ao dono. Nem Admin ignora esse filtro.
public class FavoritoService(AppDbContext db)
{
    public Task<List<Favorito>> ListarAsync(string dono) =>
        db.Favoritos.AsNoTracking().Where(f => f.UsuarioId == dono).OrderBy(f => f.Id).ToListAsync();

    public Task<Favorito?> ObterAsync(string dono, int id) =>
        db.Favoritos.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id && f.UsuarioId == dono);

    public async Task<(Favorito? Item, string? Erro)> CriarAsync(string dono, FavoritoDto dto)
    {
        if (!await db.Ativos.AnyAsync(a => a.Id == dto.AtivoId)) return (null, "Ativo inexistente.");
        var item = new Favorito { UsuarioId = dono, AtivoId = dto.AtivoId, Nota = dto.Nota };
        db.Favoritos.Add(item);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        {
            db.Entry(item).State = EntityState.Detached;
            return (null, "Favorito duplicado ou ativo indisponível.");
        }
        return (item, null);
    }

    public async Task<bool> AtualizarAsync(string dono, int id, string? nota)
    {
        var item = await db.Favoritos.SingleOrDefaultAsync(f => f.Id == id && f.UsuarioId == dono);
        if (item is null) return false;
        item.Nota = nota;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoverAsync(string dono, int id)
    {
        var item = await db.Favoritos.SingleOrDefaultAsync(f => f.Id == id && f.UsuarioId == dono);
        if (item is null) return false;
        db.Favoritos.Remove(item);
        await db.SaveChangesAsync();
        return true;
    }
}
