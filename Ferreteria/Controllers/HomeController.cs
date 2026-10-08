using System.Diagnostics;
using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Services;
using Microsoft.AspNetCore.Identity;
using Ferreteria.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly RecomendacionService _recomendaciones;
    private readonly CarritoService _carrito;
    private readonly UserManager<Usuario> _userManager;

    public HomeController(ApplicationDbContext db, RecomendacionService recomendaciones, CarritoService carrito, UserManager<Usuario> userManager)
    {
        _db = db;
        _recomendaciones = recomendaciones;
        _carrito = carrito;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new InicioVM
        {
            Categorias = await _db.Categorias.OrderBy(c => c.Nombre).ToListAsync(),
            Destacados = await _db.Productos
                .Where(p => p.Activo && p.Destacado)
                .OrderBy(p => p.Nombre)
                .Take(8)
                .ToListAsync(),
            Nuevos = await _db.Productos
                .Where(p => p.Activo)
                .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.Id)
                .Take(4)
                .ToListAsync(),
            Ofertas = await _db.Productos
                .Include(p => p.Categoria)
                .Where(p => p.Activo && p.Stock > 0 && p.PrecioAnterior > p.Precio)
                .OrderByDescending(p => (p.PrecioAnterior!.Value - p.Precio) * 100 / p.PrecioAnterior.Value)
                .Take(10)
                .ToListAsync(),
            ProductosPorCategoria = await _db.Productos
                .Where(p => p.Activo)
                .GroupBy(p => p.CategoriaId)
                .ToDictionaryAsync(g => g.Key, g => g.Count()),
            TotalProductos = await _db.Productos.CountAsync(p => p.Activo),
            TotalCategorias = await _db.Categorias.CountAsync(),
            TotalMarcas = await _db.Productos.Where(p => p.Activo && p.Marca != null).Select(p => p.Marca).Distinct().CountAsync(),
            Recomendados = await _recomendaciones.ParaClienteAsync(
                _userManager.GetUserId(User), _carrito.ObtenerItems().Select(i => i.ProductoId), 4),
            HayHistorial = User.Identity?.IsAuthenticated == true || _recomendaciones.ObtenerVistos().Count > 0
        };
        return View(vm);
    }

    public IActionResult Nosotros() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
