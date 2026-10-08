using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

/// <summary>Historial de compras del cliente.</summary>
[Authorize]
public class PedidosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<Usuario> _userManager;

    public PedidosController(ApplicationDbContext db, UserManager<Usuario> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var usuarioId = _userManager.GetUserId(User);
        var pedidos = await _db.Pedidos
            .Include(p => p.Detalles)
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.Fecha)
            .ToListAsync();
        return View(pedidos);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var usuarioId = _userManager.GetUserId(User);
        var pedido = await _db.Pedidos
            .Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId);
        if (pedido is null) return NotFound();
        return View(pedido);
    }
}
