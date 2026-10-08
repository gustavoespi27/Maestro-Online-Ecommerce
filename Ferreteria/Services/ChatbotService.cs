using System.Globalization;
using System.Text;
using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ferreteria.Services;

public record EnlaceChat(string Texto, string Url);
public record ProductoChat(int Id, string Nombre, string Precio, string? PrecioAnterior, string? Imagen, string Url);
public record RespuestaChat(string Texto, List<EnlaceChat> Enlaces, List<ProductoChat> Productos, List<string> Sugerencias);

/// <summary>
/// "Maestro Bot": asistente de atención al cliente basado en reglas (intenciones por palabras clave)
/// más búsqueda en el catálogo. Funciona sin conexión a servicios externos ni envío de datos a terceros.
/// Si no entiende la pregunta, deriva a un humano (WhatsApp / formulario de contacto).
/// </summary>
public class ChatbotService
{
    private readonly ApplicationDbContext _db;
    private readonly OpcionesTienda _tienda;

    public ChatbotService(ApplicationDbContext db, IOptions<OpcionesTienda> tienda)
    {
        _db = db;
        _tienda = tienda.Value;
    }

    private static readonly List<string> SugerenciasBase = new()
        { "¿Cuánto cuesta el despacho?", "¿Cómo sigo mi pedido?", "Quiero devolver un producto", "Ver ofertas", "Busco un taladro" };

    private record Intencion(string[] Claves, Func<ChatbotService, RespuestaChat> Responder);

    private static readonly Intencion[] Intenciones =
    {
        new(new[] { "hola", "buenas", "buenos dias", "buenas tardes", "hey" }, b => b.Saludo()),
        new(new[] { "despacho", "envio", "enviar", "llega", "demora", "region", "costo de envio", "flete", "delivery" }, b => b.Despacho()),
        new(new[] { "seguimiento", "donde esta mi pedido", "estado de mi pedido", "mi pedido", "rastrear", "tracking", "codigo de seguimiento" }, b => b.Seguimiento()),
        new(new[] { "devol", "cambio", "garantia", "retracto", "devolver", "reembolso", "falla", "defectuoso" }, b => b.Devoluciones()),
        new(new[] { "pago", "pagar", "webpay", "tarjeta", "debito", "credito", "cuotas", "transferencia" }, b => b.Pagos()),
        new(new[] { "retiro", "retirar", "tienda fisica", "direccion", "donde estan", "ubicacion", "horario", "abren", "atienden" }, b => b.Tienda()),
        new(new[] { "oferta", "descuento", "promo", "hot sale", "rebaja", "barato", "cyber" }, b => b.Ofertas()),
        new(new[] { "humano", "una persona", "ejecutivo", "contacto", "telefono", "whatsapp", "correo", "email", "reclamo", "hablar con" }, b => b.Contacto()),
        new(new[] { "privacidad", "datos personales", "mis datos", "cookies", "terminos", "condiciones" }, b => b.Legal()),
        new(new[] { "cuenta", "registr", "contrasena", "clave", "login", "iniciar sesion" }, b => b.Cuenta()),
        new(new[] { "factura", "boleta", "empresa", "cotizacion", "cotizar", "por mayor", "obra" }, b => b.Empresas()),
        new(new[] { "gracias", "genial", "perfecto", "excelente" }, b => b.Gracias()),
    };

    private static RespuestaChat R(string texto, List<EnlaceChat>? enlaces = null, List<ProductoChat>? productos = null, List<string>? sugerencias = null) =>
        new(texto, enlaces ?? new(), productos ?? new(), sugerencias ?? SugerenciasBase);

    public RespuestaChat Saludo() => R(
        "¡Hola, maestro! 👷 Soy Maestro Bot, el asistente de El Maestro Online. Puedo ayudarte con despachos, pagos, devoluciones, el estado de tu pedido o a encontrar productos. ¿Qué necesitas?");

    public RespuestaChat Despacho() => R(
        $"🚚 Despachamos a todo Chile. En la Región Metropolitana llega en 1 a 2 días hábiles ($4.990) y en regiones entre 2 y 9 días hábiles según la zona. " +
        $"El despacho es GRATIS sobre $50.000 (en regiones extremas, 50% de descuento). También puedes retirar gratis en tienda desde el día hábil siguiente.",
        new() { new("Ver tarifas por región", "/Info/Envios") });

    public RespuestaChat Seguimiento() => R(
        "📦 Puedes revisar tu pedido en \"Mis pedidos\" si tienes cuenta, o con tu código de seguimiento (lo enviamos cuando el pedido sale de bodega).",
        new() { new("Seguir mi envío", "/Info/Seguimiento"), new("Mis pedidos", "/Pedidos") });

    public RespuestaChat Devoluciones() => R(
        "↩️ Tienes 10 días desde que recibes tu compra para retractarte (Ley 19.496, art. 3 bis) si el producto está sin uso y con su embalaje. " +
        "Si el producto viene con falla, la garantía legal es de 6 meses: puedes elegir cambio, reparación o devolución del dinero.",
        new() { new("Política de cambios y devoluciones", "/Info/Devoluciones"), new("Iniciar una solicitud", "/Contacto?asunto=Devolucion") });

    public RespuestaChat Pagos() => R(
        "💳 Pagas con Webpay Plus de Transbank: tarjetas de débito, crédito (con cuotas según tu banco) y prepago. Nosotros nunca vemos ni guardamos los datos de tu tarjeta.",
        new() { new("Términos y condiciones", "/Info/Terminos") });

    public RespuestaChat Tienda() => R(
        $"🏪 Estamos en {_tienda.Direccion}. Atendemos de lunes a sábado de 9:00 a 19:00. Al comprar elige \"Retiro en tienda\" y es gratis.");

    public RespuestaChat Ofertas()
    {
        var ofertas = _db.Productos.Where(p => p.Activo && p.Stock > 0 && p.PrecioAnterior > p.Precio)
            .OrderByDescending(p => (p.PrecioAnterior!.Value - p.Precio) * 100 / p.PrecioAnterior.Value).Take(3).ToList();
        return R("🔥 ¡Estas son las mejores ofertas del Hot Sale de esta semana!", new() { new("Ver todas las ofertas", "/Productos?ofertas=true") }, ofertas.Select(AProductoChat).ToList());
    }

    public RespuestaChat Contacto() => R(
        $"🙋 Claro, te derivo con una persona del equipo. Escríbenos por WhatsApp al {_tienda.TelefonoVisible} o déjanos un mensaje y te respondemos en menos de 24 horas hábiles.",
        new() { new("WhatsApp", $"https://wa.me/{_tienda.WhatsApp}"), new("Formulario de contacto", "/Contacto") });

    public RespuestaChat Legal() => R(
        "🔒 Tus datos se usan solo para procesar tus compras y, si lo autorizas, para enviarte ofertas. Puedes pedir acceso, rectificación o eliminación cuando quieras.",
        new() { new("Política de privacidad", "/Info/Privacidad"), new("Términos y condiciones", "/Info/Terminos") });

    public RespuestaChat Cuenta() => R(
        "👤 Con una cuenta guardas tus direcciones, ves tus pedidos y compras más rápido. Crearla es gratis.",
        new() { new("Crear cuenta", "/Cuenta/Registro"), new("Ingresar", "/Cuenta/Login") });

    public RespuestaChat Empresas() => R(
        "🏗️ Hacemos cotizaciones para empresas, contratistas y obras, con precios por volumen. Cuéntanos qué necesitas en el formulario y te respondemos con la cotización.",
        new() { new("Pedir cotización", "/Contacto?asunto=Cotizacion") });

    public RespuestaChat Gracias() => R("¡A ti, maestro! 🧡 Si necesitas algo más, aquí estoy.");

    private static ProductoChat AProductoChat(Producto p) => new(
        p.Id, p.Nombre, p.Precio.Clp(), p.EnOferta ? p.PrecioAnterior!.Value.Clp() : null, p.ImagenUrl, $"/Productos/Detalle/{p.Id}");

    /// <summary>Quita tildes y pasa a minúsculas para comparar palabras clave.</summary>
    public static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static readonly HashSet<string> PalabrasVacias = new()
    {
        "busco", "buscando", "quiero", "necesito", "tienen", "tienes", "hay", "venden", "un", "una", "unos", "unas", "el", "la", "los", "las",
        "de", "del", "para", "con", "por", "me", "mi", "que", "algun", "alguna", "precio", "cuanto", "cuesta", "vale", "comprar", "y", "o", "en", "a"
    };

    public async Task<RespuestaChat> ResponderAsync(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return Saludo();
        var texto = Normalizar(mensaje.Trim());
        if (texto.Length > 300) texto = texto[..300];

        // Si pide explícitamente un producto ("busco un taladro", "tienen pintura") se busca en el catálogo primero;
        // si no, se responde la pregunta frecuente que corresponda.
        var pideProducto = new[] { "busco", "tienen", "venden", "precio de", "cuanto cuesta", "cuanto vale", "stock de" }.Any(texto.Contains);
        var productos = pideProducto ? await BuscarProductosAsync(texto) : new List<Producto>();

        var intencion = Intenciones.FirstOrDefault(i => i.Claves.Any(c => texto.Contains(c)));
        if (intencion is not null && productos.Count == 0)
            return intencion.Responder(this);

        if (productos.Count == 0) productos = await BuscarProductosAsync(texto);
        if (productos.Count > 0)
            return R($"🔎 Encontré esto en el catálogo para \"{mensaje.Trim()}\":",
                new() { new("Ver todos los resultados", $"/Productos?q={Uri.EscapeDataString(TerminosBusqueda(texto).FirstOrDefault() ?? mensaje.Trim())}") },
                productos.Select(AProductoChat).ToList());

        return R("🤔 No estoy seguro de haber entendido. Puedo ayudarte con despachos, pagos, devoluciones, seguimiento o buscar productos (ej: \"busco un martillo\"). " +
                 "Si prefieres, te contacto con una persona del equipo.",
            new() { new("Hablar con una persona", "/Contacto"), new("WhatsApp", $"https://wa.me/{_tienda.WhatsApp}") });
    }

    private static List<string> TerminosBusqueda(string texto) =>
        texto.Split(new[] { ' ', ',', '.', '?', '¿', '!', '¡' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !PalabrasVacias.Contains(t))
            .Select(t => t.EndsWith("es") && t.Length > 5 ? t[..^2] : t.EndsWith('s') && t.Length > 4 ? t[..^1] : t) // plural simple
            .Distinct().Take(4).ToList();

    private async Task<List<Producto>> BuscarProductosAsync(string texto)
    {
        var terminos = TerminosBusqueda(texto);
        if (terminos.Count == 0) return new List<Producto>();

        // La intercalación por defecto de SQL Server (CI_AS) no ignora tildes ("electrico" ≠ "eléctrico"),
        // así que se compara en memoria con textos normalizados (el catálogo es pequeño).
        var catalogo = await _db.Productos.Include(p => p.Categoria).Where(p => p.Activo).ToListAsync();
        return catalogo
            .Select(p => new
            {
                Producto = p,
                Puntaje = terminos.Count(t =>
                    Normalizar(p.Nombre).Contains(t) ||
                    Normalizar(p.Marca ?? "").Contains(t) ||
                    Normalizar(p.Categoria?.Nombre ?? "").Contains(t) ||
                    Normalizar(p.Descripcion ?? "").Contains(t))
            })
            .Where(x => x.Puntaje > 0)
            .OrderByDescending(x => x.Puntaje).ThenByDescending(x => x.Producto.Stock > 0).ThenByDescending(x => x.Producto.Destacado)
            .Take(3).Select(x => x.Producto).ToList();
    }
}
