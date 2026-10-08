using Ferreteria.Data;
using Ferreteria.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

public class CarritoController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CarritoService _carrito;
    private readonly PedidoService _pedidos;
    private readonly RecomendacionService _recomendaciones;

    public CarritoController(ApplicationDbContext db, CarritoService carrito, PedidoService pedidos, RecomendacionService recomendaciones)
    {
        _recomendaciones = recomendaciones;
        _db = db;
        _carrito = carrito;
        _pedidos = pedidos;
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _pedidos.ObtenerCarritoAsync();
        vm.Recomendaciones = await _recomendaciones.ParaCarritoAsync(vm.Lineas.Select(l => l.Producto.Id).ToList());
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Agregar(int productoId, int cantidad = 1, string? returnUrl = null)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.Activo);
        if (producto is null) return NotFound();

        var enCarrito = _carrito.ObtenerItems().FirstOrDefault(i => i.ProductoId == productoId)?.Cantidad ?? 0;
        if (producto.Stock <= 0)
        {
            TempData["Error"] = $"\"{producto.Nombre}\" está agotado.";
        }
        else if (enCarrito + cantidad > producto.Stock)
        {
            var disponible = producto.Stock - enCarrito;
            if (disponible > 0)
            {
                _carrito.Agregar(productoId, disponible);
                TempData["Aviso"] = $"Solo quedaban {producto.Stock} unidades de \"{producto.Nombre}\"; se agregaron las disponibles.";
            }
            else
            {
                TempData["Error"] = $"Ya tienes en el carrito todo el stock disponible de \"{producto.Nombre}\".";
            }
        }
        else
        {
            _carrito.Agregar(productoId, cantidad);
            TempData["Exito"] = $"Agregaste \"{producto.Nombre}\" al carrito.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(int productoId, int cantidad)
    {
        var producto = await _db.Productos.FindAsync(productoId);
        if (producto is not null && cantidad > producto.Stock)
        {
            cantidad = producto.Stock;
            TempData["Aviso"] = $"Solo hay {producto.Stock} unidades disponibles de \"{producto.Nombre}\".";
        }
        _carrito.Actualizar(productoId, cantidad);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Quitar(int productoId)
    {
        _carrito.Quitar(productoId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Vaciar()
    {
        _carrito.Vaciar();
        return RedirectToAction(nameof(Index));
    }
}
