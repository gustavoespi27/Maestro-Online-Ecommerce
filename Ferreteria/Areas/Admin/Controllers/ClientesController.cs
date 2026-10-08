using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<Usuario> _userManager;

    public ClientesController(ApplicationDbContext db, UserManager<Usuario> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var idsAdmin = (await _userManager.GetUsersInRoleAsync(SeedData.RolAdmin)).Select(u => u.Id).ToList();
        var clientes = await _db.Users
            .Where(u => !idsAdmin.Contains(u.Id))
            .OrderByDescending(u => u.FechaRegistro)
            .Select(u => new ClienteFilaVM
            {
                Nombre = u.NombreCompleto,
                Email = u.Email ?? "",
                Rut = u.Rut,
                Telefono = u.PhoneNumber,
                FechaRegistro = u.FechaRegistro,
                Pedidos = u.Pedidos.Count(p => DashboardController.EstadosVenta.Contains(p.Estado)),
                TotalComprado = u.Pedidos.Where(p => DashboardController.EstadosVenta.Contains(p.Estado)).Sum(p => (int?)p.Total) ?? 0
            })
            .ToListAsync();
        return View(clientes);
    }
}

public class ClienteFilaVM
{
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Rut { get; set; }
    public string? Telefono { get; set; }
    public DateTime FechaRegistro { get; set; }
    public int Pedidos { get; set; }
    public int TotalComprado { get; set; }
}
