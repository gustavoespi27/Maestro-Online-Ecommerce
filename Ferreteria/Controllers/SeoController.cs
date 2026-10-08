using System.Text;
using System.Xml.Linq;
using Ferreteria.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

/// <summary>Archivos para buscadores: robots.txt y sitemap.xml (generados con la URL real del sitio).</summary>
public class SeoController : Controller
{
    private readonly ApplicationDbContext _db;

    public SeoController(ApplicationDbContext db) => _db = db;

    private string UrlBase => $"{Request.Scheme}://{Request.Host}";

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 86400)]
    public IActionResult Robots()
    {
        var texto = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Allow: /")
            .AppendLine("Disallow: /Admin/")
            .AppendLine("Disallow: /Carrito")
            .AppendLine("Disallow: /Checkout")
            .AppendLine("Disallow: /Cuenta/")
            .AppendLine("Disallow: /Pago/")
            .AppendLine("Disallow: /Pedidos")
            .AppendLine("Disallow: /Productos?*q=")
            .AppendLine()
            .AppendLine($"Sitemap: {UrlBase}/sitemap.xml")
            .ToString();
        return Content(texto, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace img = "http://www.google.com/schemas/sitemap-image/1.1";

        XElement Url(string ruta, string frecuencia, string prioridad, string? imagen = null, DateTime? modificado = null)
        {
            var e = new XElement(ns + "url",
                new XElement(ns + "loc", UrlBase + ruta),
                new XElement(ns + "changefreq", frecuencia),
                new XElement(ns + "priority", prioridad));
            if (modificado is not null) e.Add(new XElement(ns + "lastmod", modificado.Value.ToString("yyyy-MM-dd")));
            if (imagen is not null) e.Add(new XElement(img + "image", new XElement(img + "loc", UrlBase + imagen)));
            return e;
        }

        var urlset = new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "image", img),
            Url("/", "daily", "1.0"),
            Url("/Productos", "daily", "0.9"),
            Url("/Productos?ofertas=true", "daily", "0.9"),
            Url("/Home/Nosotros", "monthly", "0.4"),
            Url("/Contacto", "monthly", "0.5"),
            Url("/Info/Envios", "monthly", "0.5"),
            Url("/Info/Devoluciones", "monthly", "0.4"),
            Url("/Info/Terminos", "yearly", "0.3"),
            Url("/Info/Privacidad", "yearly", "0.3"));

        foreach (var c in await _db.Categorias.OrderBy(c => c.Nombre).ToListAsync())
            urlset.Add(Url($"/Productos?categoriaId={c.Id}", "weekly", "0.8", c.Icono is null ? null : $"/img/categorias/{c.Icono}.jpg"));

        var productos = await _db.Productos.Where(p => p.Activo).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.ImagenUrl, p.FechaCreacion }).ToListAsync();
        foreach (var p in productos)
            urlset.Add(Url($"/Productos/Detalle/{p.Id}", "weekly", "0.7", p.ImagenUrl, p.FechaCreacion));

        var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return Content(xml.Declaration + Environment.NewLine + xml.Root, "application/xml", Encoding.UTF8);
    }
}
