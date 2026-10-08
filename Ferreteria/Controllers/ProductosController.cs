using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Models.ViewModels;
using Ferreteria.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

public class ProductosController : Controller
{
    private const int ProductosPorPagina = 12;
    private readonly ApplicationDbContext _db;
    private readonly RecomendacionService _recomendaciones;
    private readonly UserManager<Usuario> _userManager;

    public ProductosController(ApplicationDbContext db, RecomendacionService recomendaciones, UserManager<Usuario> userManager)
    {
        _db = db;
        _recomendaciones = recomendaciones;
        _userManager = userManager;
    }

    // GET /Productos?categoriaId=2&q=martillo&orden=precio-asc&pagina=1&ofertas=true
    public async Task<IActionResult> Index(int? categoriaId, string? q, string orden = "relevancia", int pagina = 1, bool ofertas = false)
    {
        var consulta = _db.Productos.Include(p => p.Categoria).Where(p => p.Activo);

        if (categoriaId.HasValue)
            consulta = consulta.Where(p => p.CategoriaId == categoriaId.Value);

        if (ofertas)
            consulta = consulta.Where(p => p.PrecioAnterior > p.Precio);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = q.Trim();
            consulta = consulta.Where(p =>
                p.Nombre.Contains(texto) ||
                (p.Marca != null && p.Marca.Contains(texto)) ||
                p.Sku.Contains(texto) ||
                (p.Descripcion != null && p.Descripcion.Contains(texto)));
        }

        consulta = orden switch
        {
            "precio-asc" => consulta.OrderBy(p => p.Precio),
            "precio-desc" => consulta.OrderByDescending(p => p.Precio),
            "nombre" => consulta.OrderBy(p => p.Nombre),
            "descuento" => consulta.OrderByDescending(p => p.PrecioAnterior > p.Precio
                ? (p.PrecioAnterior!.Value - p.Precio) * 100 / p.PrecioAnterior.Value : 0).ThenBy(p => p.Nombre),
            _ => consulta.OrderByDescending(p => p.Destacado).ThenBy(p => p.Nombre)
        };

        var total = await consulta.CountAsync();
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)ProductosPorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var vm = new CatalogoVM
        {
            Productos = await consulta.Skip((pagina - 1) * ProductosPorPagina).Take(ProductosPorPagina).ToListAsync(),
            Categorias = await _db.Categorias.OrderBy(c => c.Nombre).ToListAsync(),
            CategoriaId = categoriaId,
            Busqueda = q,
            SoloOfertas = ofertas,
            Orden = orden,
            Pagina = pagina,
            TotalPaginas = totalPaginas,
            TotalResultados = total
        };
        return View(vm);
    }

    // GET /Productos/Detalle/5
    public async Task<IActionResult> Detalle(int id)
    {
        var producto = await _db.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);
        if (producto is null) return NotFound();

        _recomendaciones.RegistrarVisto(id);
        ViewBag.Recomendaciones = await _recomendaciones.ParaProductoAsync(producto);

        // Reseñas (prueba social) y si el usuario actual puede opinar (compra verificada)
        ViewBag.Resenas = await _db.Resenas.Include(r => r.Usuario)
            .Where(r => r.ProductoId == id).OrderByDescending(r => r.Fecha).Take(20).ToListAsync();
        var usuarioId = _userManager.GetUserId(User);
        ViewBag.PuedeOpinar = usuarioId is not null && await Controllers.ResenasController.PuedeOpinarAsync(_db, usuarioId, id);
        ViewBag.MiResena = usuarioId is null ? null : await _db.Resenas.FirstOrDefaultAsync(r => r.ProductoId == id && r.UsuarioId == usuarioId);

        return View(producto);
    }
}
