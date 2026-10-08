using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class CategoriasController : Controller
{
    private readonly ApplicationDbContext _db;

    public CategoriasController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var categorias = await _db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(c => new { Categoria = c, Cantidad = c.Productos.Count })
            .ToListAsync();
        ViewBag.Conteos = categorias.ToDictionary(x => x.Categoria.Id, x => x.Cantidad);
        return View(categorias.Select(x => x.Categoria).ToList());
    }

    [HttpGet]
    public IActionResult Crear() => View("Formulario", new Categoria());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(Categoria categoria)
    {
        await ValidarNombreAsync(categoria);
        if (!ModelState.IsValid) return View("Formulario", categoria);

        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        TempData["Exito"] = $"Categoría \"{categoria.Nombre}\" creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var categoria = await _db.Categorias.FindAsync(id);
        return categoria is null ? NotFound() : View("Formulario", categoria);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, Categoria datos)
    {
        var categoria = await _db.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();
        datos.Id = id;

        await ValidarNombreAsync(datos);
        if (!ModelState.IsValid) return View("Formulario", datos);

        categoria.Nombre = datos.Nombre.Trim();
        categoria.Descripcion = datos.Descripcion;
        categoria.Icono = datos.Icono;
        await _db.SaveChangesAsync();
        TempData["Exito"] = "Categoría actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var categoria = await _db.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();

        if (await _db.Productos.AnyAsync(p => p.CategoriaId == id))
        {
            TempData["Error"] = $"No se puede eliminar \"{categoria.Nombre}\" porque tiene productos. Muévelos a otra categoría primero.";
            return RedirectToAction(nameof(Index));
        }

        _db.Categorias.Remove(categoria);
        await _db.SaveChangesAsync();
        TempData["Exito"] = "Categoría eliminada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarNombreAsync(Categoria c)
    {
        var nombre = c.Nombre?.Trim() ?? "";
        if (await _db.Categorias.AnyAsync(x => x.Nombre == nombre && x.Id != c.Id))
            ModelState.AddModelError(nameof(c.Nombre), "Ya existe una categoría con ese nombre.");
    }
}
