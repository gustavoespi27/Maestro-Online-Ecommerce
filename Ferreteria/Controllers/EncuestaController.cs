using Ferreteria.Services;
using Microsoft.AspNetCore.RateLimiting;
using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers;

/// <summary>Encuesta anónima de aceptación tecnológica (UTAUT) para usuarios potenciales del sitio.</summary>
public class EncuestaController : Controller
{
    private const string CookieRespondida = "EncuestaRespondida";
    private readonly ApplicationDbContext _db;

    public EncuestaController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.YaRespondio = Request.Cookies.ContainsKey(CookieRespondida);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaEncuesta)]
    public async Task<IActionResult> Index(IFormCollection form)
    {
        string? Opcion(string campo, string[] validas) =>
            validas.Contains(form[campo].ToString()) ? form[campo].ToString() : null;

        var r = new RespuestaEncuesta
        {
            Fecha = DateTime.Now,
            RangoEdad = Opcion("RangoEdad", EncuestaUtaut.RangosEdad),
            Genero = Opcion("Genero", EncuestaUtaut.Generos),
            FrecuenciaCompraOnline = Opcion("FrecuenciaCompraOnline", EncuestaUtaut.Frecuencias),
            Perfil = Opcion("Perfil", EncuestaUtaut.Perfiles),
            Comentario = form["Comentario"].ToString() is { Length: > 0 } c ? c[..Math.Min(c.Length, 500)] : null
        };

        var faltantes = new List<string>();
        foreach (var item in EncuestaUtaut.Constructos.SelectMany(k => k.Items))
        {
            if (int.TryParse(form[item.Codigo], out var v) && v is >= 1 and <= 5)
                EncuestaUtaut.Asignar(r, item.Codigo, v);
            else
                faltantes.Add(item.Codigo);
        }
        if (r.RangoEdad is null || r.FrecuenciaCompraOnline is null || r.Perfil is null) faltantes.Add("datos");

        if (faltantes.Count > 0)
        {
            ViewBag.Error = "Responde todas las preguntas para poder registrar tu opinión.";
            ViewBag.Faltantes = faltantes;
            ViewBag.Form = form;
            ViewBag.YaRespondio = false;
            return View();
        }

        _db.RespuestasEncuesta.Add(r);
        await _db.SaveChangesAsync();
        // Solo marca que respondió desde este navegador (no identifica a la persona)
        Response.Cookies.Append(CookieRespondida, "1", new CookieOptions { Expires = DateTimeOffset.Now.AddDays(60), HttpOnly = true, IsEssential = true });
        return RedirectToAction(nameof(Gracias));
    }

    public IActionResult Gracias() => View();
}
