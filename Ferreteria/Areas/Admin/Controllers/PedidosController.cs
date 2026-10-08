using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class PedidosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PedidoService _pedidos;

    public PedidosController(ApplicationDbContext db, PedidoService pedidos)
    {
        _db = db;
        _pedidos = pedidos;
    }

    public async Task<IActionResult> Index(EstadoPedido? estado, string? q)
    {
        var consulta = _db.Pedidos.Include(p => p.Usuario).AsQueryable();
        if (estado.HasValue)
            consulta = consulta.Where(p => p.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(q))
            consulta = consulta.Where(p => p.OrdenCompra.Contains(q) || p.NombreReceptor.Contains(q)
                                           || (p.Usuario != null && p.Usuario.Email!.Contains(q))
                                           || (p.EmailInvitado != null && p.EmailInvitado.Contains(q)));

        ViewBag.Estado = estado;
        ViewBag.Q = q;
        return View(await consulta.OrderByDescending(p => p.Fecha).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var pedido = await _db.Pedidos
            .Include(p => p.Usuario)
            .Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);
        return pedido is null ? NotFound() : View(pedido);
    }

    /// <summary>Avanza el pedido: Pagado → En preparación → Despachado → Entregado.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id, string? transportista, string? codigoSeguimiento)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido is null) return NotFound();

        var siguiente = SiguienteEstado(pedido);
        if (siguiente is null)
        {
            TempData["Error"] = "Este pedido no se puede avanzar de estado.";
        }
        else
        {
            // Logística: al despachar se registra el transportista y el código de seguimiento
            if (siguiente == EstadoPedido.Despachado)
            {
                pedido.Transportista = Envios.Transportistas.Contains(transportista) ? transportista : Envios.Transportistas[0];
                pedido.CodigoSeguimiento = string.IsNullOrWhiteSpace(codigoSeguimiento)
                    ? Envios.GenerarCodigoSeguimiento()
                    : codigoSeguimiento.Trim().ToUpperInvariant()[..Math.Min(codigoSeguimiento.Trim().Length, 40)];
                pedido.FechaDespacho = DateTime.Now;
                pedido.FechaEstimadaEntrega = Envios.RangoEntrega(pedido.Region, DateTime.Now).Hasta;
            }
            if (siguiente == EstadoPedido.Entregado)
                pedido.FechaEntrega = DateTime.Now;

            pedido.Estado = siguiente.Value;
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Pedido {pedido.OrdenCompra} ahora está \"{pedido.Estado.Nombre()}\".";
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido is null) return NotFound();

        if (pedido.Estado is EstadoPedido.Despachado or EstadoPedido.Entregado or EstadoPedido.Anulado or EstadoPedido.Rechazado)
        {
            TempData["Error"] = "Este pedido ya no se puede anular.";
        }
        else
        {
            var estabaPagado = pedido.Estado != EstadoPedido.PendientePago;
            await _pedidos.AnularAsync(pedido);
            TempData["Aviso"] = estabaPagado
                ? "Pedido anulado y stock devuelto. Recuerda gestionar la devolución del dinero en el portal de Transbank."
                : "Pedido anulado y stock devuelto.";
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    public static EstadoPedido? SiguienteEstado(Pedido p) => p.Estado switch
    {
        EstadoPedido.Pagado => EstadoPedido.EnPreparacion,
        EstadoPedido.EnPreparacion => p.TipoEntrega == TipoEntrega.Despacho ? EstadoPedido.Despachado : EstadoPedido.Entregado,
        EstadoPedido.Despachado => EstadoPedido.Entregado,
        _ => null
    };
}
