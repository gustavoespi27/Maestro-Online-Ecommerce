using Ferreteria.Services;
using Microsoft.AspNetCore.RateLimiting;
using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

/// <summary>Reseñas de productos. Solo pueden opinar clientes con una compra pagada del producto (compra verificada).</summary>
[Authorize]
public class ResenasController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<Usuario> _userManager;

    public ResenasController(ApplicationDbContext db, UserManager<Usuario> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public static readonly EstadoPedido[] EstadosCompraVerificada =
        { EstadoPedido.Pagado, EstadoPedido.EnPreparacion, EstadoPedido.Despachado, EstadoPedido.Entregado };

    public static Task<bool> PuedeOpinarAsync(ApplicationDbContext db, string usuarioId, int productoId) =>
        db.DetallesPedido.AnyAsync(d => d.ProductoId == productoId && d.Pedido!.UsuarioId == usuarioId
                                        && EstadosCompraVerificada.Contains(d.Pedido.Estado));

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaFormularios)]
    public async Task<IActionResult> Guardar(int productoId, int calificacion, string? comentario)
    {
        var usuarioId = _userManager.GetUserId(User)!;
        if (!await _db.Productos.AnyAsync(p => p.Id == productoId)) return NotFound();

        if (!await PuedeOpinarAsync(_db, usuarioId, productoId))
        {
            TempData["Error"] = "Solo pueden opinar clientes que compraron este producto.";
        }
        else if (calificacion is < 1 or > 5)
        {
            TempData["Error"] = "Elige una calificación de 1 a 5 estrellas.";
        }
        else
        {
            var resena = await _db.Resenas.FirstOrDefaultAsync(r => r.ProductoId == productoId && r.UsuarioId == usuarioId);
            if (resena is null)
            {
                resena = new Resena { ProductoId = productoId, UsuarioId = usuarioId };
                _db.Resenas.Add(resena);
            }
            resena.Calificacion = calificacion;
            resena.Comentario = Texto.Limpiar(comentario, 600);
            resena.Fecha = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Exito"] = "¡Gracias por tu opinión, maestro!";
        }
        return Redirect(Url.Action("Detalle", "Productos", new { id = productoId }) + "#resenas");
    }
}
