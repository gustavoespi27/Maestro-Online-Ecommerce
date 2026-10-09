using Ferreteria.Services;
using System.Globalization;
using System.Text;
using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Areas.Admin.Controllers;

/// <summary>Resultados de la encuesta UTAUT: promedios por constructo, distribución por ítem y exportación a Excel (CSV).</summary>
[Area("Admin")]
[Authorize(Roles = SeedData.RolAdmin)]
public class EncuestaController : Controller
{
    private readonly ApplicationDbContext _db;

    public EncuestaController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.RespuestasEncuesta.OrderBy(r => r.Fecha).ToListAsync());

    /// <summary>
    /// Descarga todas las respuestas en CSV separado por punto y coma (lo abre Excel en español directamente).
    /// Incluye una columna con el promedio de cada constructo.
    /// </summary>
    public async Task<IActionResult> Exportar()
    {
        var respuestas = await _db.RespuestasEncuesta.OrderBy(r => r.Fecha).ToListAsync();
        var items = EncuestaUtaut.Constructos.SelectMany(c => c.Items).ToList();
        var cl = CultureInfo.GetCultureInfo("es-CL");
        string Celda(string? v) => Texto.CeldaCsv(v); // evita que Excel ejecute fórmulas escritas por encuestados

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", new[] { "N", "Fecha", "Edad", "Genero", "Frecuencia compra online", "Perfil" }
            .Concat(items.Select(i => i.Codigo))
            .Concat(EncuestaUtaut.Constructos.Select(c => "Promedio " + c.Sigla))
            .Append("Comentario")));

        var n = 0;
        foreach (var r in respuestas)
        {
            sb.AppendLine(string.Join(";", new[]
                {
                    (++n).ToString(), r.Fecha.ToString("dd-MM-yyyy HH:mm"), Celda(r.RangoEdad), Celda(r.Genero), Celda(r.FrecuenciaCompraOnline), Celda(r.Perfil)
                }
                .Concat(items.Select(i => EncuestaUtaut.Valor(r, i.Codigo).ToString()))
                .Concat(EncuestaUtaut.Constructos.Select(c => EncuestaUtaut.Promedio(r, c).ToString("0.00", cl)))
                .Append(Celda(r.Comentario))));
        }

        // BOM UTF-8 para que Excel muestre bien las tildes
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"encuesta_utaut_{DateTime.Now:yyyyMMdd}.csv");
    }
}
