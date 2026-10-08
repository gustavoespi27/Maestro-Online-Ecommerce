using Ferreteria.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

/// <summary>Bandeja de mensajes del formulario de contacto.</summary>
[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class MensajesController : Controller
{
    private readonly ApplicationDbContext _db;

    public MensajesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(bool pendientes = false)
    {
        var consulta = _db.MensajesContacto.AsQueryable();
        if (pendientes) consulta = consulta.Where(m => !m.Atendido);
        ViewBag.Pendientes = pendientes;
        return View(await consulta.OrderBy(m => m.Atendido).ThenByDescending(m => m.Fecha).Take(300).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var m = await _db.MensajesContacto.FindAsync(id);
        if (m is null) return NotFound();
        m.Atendido = !m.Atendido;
        await _db.SaveChangesAsync();
        TempData["Exito"] = m.Atendido ? "Mensaje marcado como atendido." : "Mensaje marcado como pendiente.";
        return RedirectToAction(nameof(Index));
    }
}
