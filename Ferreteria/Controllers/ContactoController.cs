using Ferreteria.Services;
using Microsoft.AspNetCore.RateLimiting;
using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers;

/// <summary>Formulario de contacto: los mensajes se guardan y el equipo los atiende desde el panel admin.</summary>
public class ContactoController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<Usuario> _userManager;

    public ContactoController(ApplicationDbContext db, UserManager<Usuario> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(AsuntoContacto? asunto, string? orden)
    {
        var mensaje = new MensajeContacto { Asunto = asunto ?? AsuntoContacto.Producto, OrdenCompra = orden };
        if (User.Identity?.IsAuthenticated == true && await _userManager.GetUserAsync(User) is { } u)
        {
            mensaje.Nombre = u.NombreCompleto;
            mensaje.Email = u.Email ?? "";
            mensaje.Telefono = u.PhoneNumber;
        }
        return View(mensaje);
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaFormularios)]
    public async Task<IActionResult> Index(MensajeContacto datos, bool aceptaPrivacidad, string? sitioWeb)
    {
        // Campo trampa invisible: los bots lo llenan, las personas no (anti-spam sin captcha)
        if (!string.IsNullOrEmpty(sitioWeb)) return RedirectToAction(nameof(Gracias));

        if (!aceptaPrivacidad)
            ModelState.AddModelError("aceptaPrivacidad", "Debes aceptar la Política de Privacidad para que podamos responderte");
        if (!ModelState.IsValid) return View(datos);

        datos.Id = 0;
        datos.Fecha = DateTime.Now;
        datos.Atendido = false;
        datos.Nombre = datos.Nombre.Trim();
        datos.Email = datos.Email.Trim();
        _db.MensajesContacto.Add(datos);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Gracias), new { id = datos.Id });
    }

    public IActionResult Gracias(int? id)
    {
        ViewBag.Numero = id;
        return View();
    }
}
