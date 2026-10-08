using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class DashboardController : Controller
{
    /// <summary>Estados que cuentan como venta concretada.</summary>
    public static readonly EstadoPedido[] EstadosVenta =
        { EstadoPedido.Pagado, EstadoPedido.EnPreparacion, EstadoPedido.Despachado, EstadoPedido.Entregado };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<Usuario> _userManager;

    public DashboardController(ApplicationDbContext db, UserManager<Usuario> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var hace7Dias = hoy.AddDays(-6);

        var ventas = _db.Pedidos.Where(p => EstadosVenta.Contains(p.Estado));

        var ventasSemana = await ventas
            .Where(p => p.Fecha >= hace7Dias)
            .Select(p => new { p.Fecha, p.Total })
            .ToListAsync();

        var masVendidos = await _db.DetallesPedido
            .Where(d => EstadosVenta.Contains(d.Pedido!.Estado))
            .GroupBy(d => d.NombreProducto)
            .Select(g => new
            {
                Nombre = g.Key,
                Unidades = g.Sum(x => x.Cantidad),
                Monto = g.Sum(x => x.PrecioUnitario * x.Cantidad)
            })
            .OrderByDescending(x => x.Unidades)
            .Take(5)
            .ToListAsync();

        var vm = new DashboardVM
        {
            VentasTotales = await ventas.SumAsync(p => (int?)p.Total) ?? 0,
            VentasMes = await ventas.Where(p => p.Fecha >= inicioMes).SumAsync(p => (int?)p.Total) ?? 0,
            PedidosPagadosMes = await ventas.CountAsync(p => p.Fecha >= inicioMes),
            PedidosPorPreparar = await _db.Pedidos.CountAsync(p => p.Estado == EstadoPedido.Pagado || p.Estado == EstadoPedido.EnPreparacion),
            TotalProductos = await _db.Productos.CountAsync(p => p.Activo),
            TotalClientes = (await _userManager.GetUsersInRoleAsync(SeedData.RolCliente)).Count,
            StockBajo = await _db.Productos
                .Where(p => p.Activo && p.Stock <= p.StockMinimo)
                .OrderBy(p => p.Stock)
                .Take(8)
                .ToListAsync(),
            UltimosPedidos = await _db.Pedidos
                .Include(p => p.Usuario)
                .OrderByDescending(p => p.Fecha)
                .Take(8)
                .ToListAsync(),
            MasVendidos = masVendidos.Select(x => (x.Nombre, x.Unidades, x.Monto)).ToList(),
            VentasUltimos7Dias = Enumerable.Range(0, 7)
                .Select(i => hace7Dias.AddDays(i))
                .Select(dia => (dia, ventasSemana.Where(v => v.Fecha.Date == dia).Sum(v => v.Total)))
                .ToList()
        };

        return View(vm);
    }
}
