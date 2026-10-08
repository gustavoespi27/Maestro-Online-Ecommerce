using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ferreteria.Services;

/// <summary>Nonce (número de un solo uso) de la política CSP para los scripts en línea de la página actual.</summary>
public class CspNonce
{
    public string Valor { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
}

/// <summary>
/// Configuración de seguridad de la aplicación:
///  • Límite de solicitudes por IP (fuerza bruta en el login, spam en formularios, abuso del chatbot y del pago).
///  • Cabeceras HTTP de seguridad y Content-Security-Policy con nonce (mitiga XSS y clickjacking).
/// </summary>
public static class Seguridad
{
    public const string PoliticaLogin = "login";
    public const string PoliticaFormularios = "formularios";
    public const string PoliticaChatbot = "chatbot";
    public const string PoliticaPagos = "pagos";
    public const string PoliticaEncuesta = "encuesta";
    public const string PoliticaConsultas = "consultas";

    private static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    private static void Ventana(RateLimiterOptions o, string nombre, int permitidas, TimeSpan ventana) =>
        o.AddPolicy(nombre, c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitidas,
            Window = ventana,
            QueueLimit = 0
        }));

    public static IServiceCollection AgregarLimitesDeSolicitudes(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            Ventana(o, PoliticaLogin, 10, TimeSpan.FromMinutes(5));        // intentos de login / registro por IP
            Ventana(o, PoliticaFormularios, 10, TimeSpan.FromMinutes(10)); // contacto, reseñas
            Ventana(o, PoliticaChatbot, 30, TimeSpan.FromMinutes(1));
            Ventana(o, PoliticaPagos, 120, TimeSpan.FromMinutes(5));     // páginas de pago (incluye recargas y reintentos)
            Ventana(o, PoliticaEncuesta, 40, TimeSpan.FromHours(1));      // permite aplicar la encuesta en un mismo equipo
            Ventana(o, PoliticaConsultas, 20, TimeSpan.FromMinutes(5));    // seguimiento de pedidos (evita adivinar códigos)
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await ctx.HttpContext.Response.WriteAsync("Demasiados intentos. Espera unos minutos y vuelve a intentarlo.", token);
            };
        });

    /// <summary>Agrega las cabeceras de seguridad a todas las respuestas.</summary>
    public static IApplicationBuilder UsarCabecerasDeSeguridad(this IApplicationBuilder app, IConfiguration config) =>
        app.Use(async (contexto, siguiente) =>
        {
            var nonce = contexto.RequestServices.GetRequiredService<CspNonce>().Valor;
            var webpay = config["Pagos:WebpayUrlBase"] ?? "https://webpay3gint.transbank.cl";
            contexto.Response.OnStarting(() =>
            {
                var h = contexto.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    $"script-src 'self' 'nonce-{nonce}'; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                    "font-src 'self' https://fonts.gstatic.com; " +
                    "img-src 'self' data: blob:; " +
                    "connect-src 'self'; " +
                    // El formulario de pago se envía a Transbank (integración y producción)
                    $"form-action 'self' {webpay} https://webpay3gint.transbank.cl https://webpay3g.transbank.cl; " +
                    "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
                // Las páginas con datos personales no se guardan en caché compartida
                if (contexto.User.Identity?.IsAuthenticated == true && !h.ContainsKey("Cache-Control")
                    && contexto.Response.ContentType?.StartsWith("text/html") == true)
                    h["Cache-Control"] = "no-store";
                return Task.CompletedTask;
            });
            await siguiente();
        });
}
