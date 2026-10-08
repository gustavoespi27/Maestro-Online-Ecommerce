using Ferreteria.Services;
using Microsoft.AspNetCore.RateLimiting;
using Ferreteria.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

/// <summary>Páginas informativas y legales: envíos, seguimiento, términos, privacidad, devoluciones y créditos.</summary>
public class InfoController : Controller
{
    private readonly ApplicationDbContext _db;

    public InfoController(ApplicationDbContext db) => _db = db;

    public IActionResult Envios() => View();
    public IActionResult Terminos() => View();
    public IActionResult Privacidad() => View();
    public IActionResult Devoluciones() => View();
    public IActionResult Creditos() => View();

    /// <summary>
    /// Seguimiento público de un envío. Se pide el código de seguimiento o la orden de compra
    /// y además el correo de la cuenta, para no exponer datos de otras personas.
    /// </summary>
    [EnableRateLimiting(Seguridad.PoliticaConsultas)] // evita adivinar códigos por fuerza bruta
    public async Task<IActionResult> Seguimiento(string? codigo, string? email)
    {
        ViewBag.Codigo = codigo;
        ViewBag.Email = email;
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(email)) return View(null);

        var c = codigo.Trim().ToUpperInvariant();
        var e = email.Trim().ToLowerInvariant();
        var pedido = await _db.Pedidos
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => (p.CodigoSeguimiento == c || p.OrdenCompra == c)
                                      && ((p.Usuario != null && p.Usuario.NormalizedEmail == e.ToUpperInvariant())
                                          || p.EmailInvitado == e));
        if (pedido is null) ViewBag.NoEncontrado = true;
        return View(pedido);
    }
}
