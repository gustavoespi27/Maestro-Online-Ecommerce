using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class ProductosController : Controller
{
    private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long TamanoMaximoImagen = 2 * 1024 * 1024; // 2 MB

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductosController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index(string? q, int? categoriaId, bool stockBajo = false)
    {
        var consulta = _db.Productos.Include(p => p.Categoria).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            consulta = consulta.Where(p => p.Nombre.Contains(q) || p.Sku.Contains(q) || (p.Marca != null && p.Marca.Contains(q)));
        if (categoriaId.HasValue)
            consulta = consulta.Where(p => p.CategoriaId == categoriaId);
        if (stockBajo)
            consulta = consulta.Where(p => p.Stock <= p.StockMinimo);

        ViewBag.Q = q;
        ViewBag.CategoriaId = categoriaId;
        ViewBag.StockBajo = stockBajo;
        ViewBag.Categorias = new SelectList(await _db.Categorias.OrderBy(c => c.Nombre).ToListAsync(), "Id", "Nombre", categoriaId);

        return View(await consulta.OrderBy(p => p.Nombre).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Crear() =>
        View("Formulario", new ProductoFormVM { Categorias = await ListaCategoriasAsync() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ProductoFormVM vm)
    {
        await ValidarAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.Categorias = await ListaCategoriasAsync();
            return View("Formulario", vm);
        }

        // Se copian solo los campos editables: el Id, la fecha y la URL de la imagen nunca vienen del formulario
        var f = vm.Producto;
        var producto = new Producto
        {
            Sku = f.Sku.Trim(),
            Nombre = f.Nombre.Trim(),
            Marca = f.Marca?.Trim(),
            Descripcion = f.Descripcion?.Trim(),
            Precio = f.Precio,
            PrecioAnterior = f.PrecioAnterior,
            Stock = f.Stock,
            StockMinimo = f.StockMinimo,
            CategoriaId = f.CategoriaId,
            Destacado = f.Destacado,
            Activo = f.Activo,
            FechaCreacion = DateTime.Now,
            ImagenUrl = vm.Imagen is not null ? await GuardarImagenAsync(vm.Imagen) : null
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        TempData["Exito"] = $"Producto \"{producto.Nombre}\" creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();
        return View("Formulario", new ProductoFormVM { Producto = producto, Categorias = await ListaCategoriasAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ProductoFormVM vm)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();
        vm.Producto.Id = id;

        await ValidarAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.Producto.ImagenUrl = producto.ImagenUrl;
            vm.Categorias = await ListaCategoriasAsync();
            return View("Formulario", vm);
        }

        // Se copian campo a campo para no sobrescribir datos que no vienen en el formulario (ej: FechaCreacion).
        producto.Sku = vm.Producto.Sku.Trim();
        producto.Nombre = vm.Producto.Nombre.Trim();
        producto.Marca = vm.Producto.Marca;
        producto.Descripcion = vm.Producto.Descripcion;
        producto.Precio = vm.Producto.Precio;
        producto.PrecioAnterior = vm.Producto.PrecioAnterior;
        producto.Stock = vm.Producto.Stock;
        producto.StockMinimo = vm.Producto.StockMinimo;
        producto.CategoriaId = vm.Producto.CategoriaId;
        producto.Destacado = vm.Producto.Destacado;
        producto.Activo = vm.Producto.Activo;
        if (vm.Imagen is not null)
            producto.ImagenUrl = await GuardarImagenAsync(vm.Imagen);

        await _db.SaveChangesAsync();
        TempData["Exito"] = $"Producto \"{producto.Nombre}\" actualizado.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Suma o resta unidades al stock (ej: llegó mercadería).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AjustarStock(int id, int cantidad)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Stock = Math.Max(0, producto.Stock + cantidad);
        await _db.SaveChangesAsync();
        TempData["Exito"] = $"Stock de \"{producto.Nombre}\" actualizado a {producto.Stock} unidades.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        // Si el producto ya aparece en pedidos no se puede borrar (se perdería el historial): se desactiva.
        if (await _db.DetallesPedido.AnyAsync(d => d.ProductoId == id))
        {
            producto.Activo = false;
            TempData["Aviso"] = $"\"{producto.Nombre}\" tiene ventas asociadas, así que se desactivó en vez de eliminarse.";
        }
        else
        {
            _db.Productos.Remove(producto);
            TempData["Exito"] = $"Producto \"{producto.Nombre}\" eliminado.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ---------- Auxiliares ----------

    private async Task ValidarAsync(ProductoFormVM vm)
    {
        var sku = vm.Producto.Sku?.Trim() ?? "";
        if (await _db.Productos.AnyAsync(p => p.Sku == sku && p.Id != vm.Producto.Id))
            ModelState.AddModelError("Producto.Sku", "Ya existe un producto con ese SKU.");

        if (vm.Producto.PrecioAnterior is int anterior && anterior <= vm.Producto.Precio)
            ModelState.AddModelError("Producto.PrecioAnterior", "Para una oferta, el precio anterior debe ser mayor al precio actual. Déjalo vacío si no hay oferta.");

        if (vm.Imagen is not null)
        {
            var ext = Path.GetExtension(vm.Imagen.FileName).ToLowerInvariant();
            if (!ExtensionesPermitidas.Contains(ext))
                ModelState.AddModelError(nameof(vm.Imagen), "Formato no permitido. Usa JPG, PNG, WEBP o GIF.");
            if (vm.Imagen.Length > TamanoMaximoImagen)
                ModelState.AddModelError(nameof(vm.Imagen), "La imagen no puede pesar más de 2 MB.");
            else if (!await EsImagenRealAsync(vm.Imagen))
                ModelState.AddModelError(nameof(vm.Imagen), "El archivo no es una imagen válida.");
        }
    }

    /// <summary>
    /// Revisa la "firma" (primeros bytes) del archivo: la extensión se puede falsificar, el contenido no.
    /// Evita que se suba, por ejemplo, un HTML o script renombrado como .jpg.
    /// </summary>
    private static async Task<bool> EsImagenRealAsync(IFormFile archivo)
    {
        var cabecera = new byte[12];
        await using var stream = archivo.OpenReadStream();
        var leidos = await stream.ReadAsync(cabecera);
        if (leidos < 12) return false;
        bool Empieza(params byte[] firma) => cabecera.Take(firma.Length).SequenceEqual(firma);
        return Empieza(0xFF, 0xD8, 0xFF)                                                    // JPG
            || Empieza(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)                       // PNG
            || Empieza(0x47, 0x49, 0x46, 0x38)                                              // GIF
            || (Empieza(0x52, 0x49, 0x46, 0x46) && cabecera[8..12].SequenceEqual("WEBP"u8.ToArray())); // WEBP
    }

    private async Task<string> GuardarImagenAsync(IFormFile archivo)
    {
        var carpeta = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(carpeta);
        var nombre = $"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName).ToLowerInvariant()}";
        await using var stream = System.IO.File.Create(Path.Combine(carpeta, nombre));
        await archivo.CopyToAsync(stream);
        return $"/uploads/{nombre}";
    }

    private async Task<IEnumerable<SelectListItem>> ListaCategoriasAsync() =>
        (await _db.Categorias.OrderBy(c => c.Nombre).ToListAsync())
            .Select(c => new SelectListItem(c.Nombre, c.Id.ToString()));
}
