using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Controllers;

/// <summary>
/// Flujo de pago con Webpay Plus:
///   1. Iniciar   → crea la transacción en Transbank y envía al usuario al formulario de pago.
///   2. Retorno   → Transbank devuelve al usuario aquí (con token_ws o TBK_TOKEN).
///   3. Resultado → muestra el comprobante o el error.
///
/// Seguridad:
///  • El monto SIEMPRE sale de la base de datos, nunca del navegador.
///  • El resultado se confirma servidor a servidor con Transbank (commit) y se valida monto + orden.
///  • Un pedido solo lo puede ver o pagar su dueño: el cliente con sesión iniciada o, si compró como invitado,
///    quien tenga el token secreto del pedido (256 bits, comparado en tiempo constante).
///  • Las anulaciones que llegan al retorno se aceptan solo si traen el token de la transacción o el session_id secreto.
/// </summary>
[EnableRateLimiting(Seguridad.PoliticaPagos)]
public class PagoController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPasarelaPago _pasarela;
    private readonly PedidoService _pedidos;
    private readonly UserManager<Usuario> _userManager;
    private readonly ILogger<PagoController> _logger;

    public PagoController(ApplicationDbContext db, IPasarelaPago pasarela, PedidoService pedidos,
        UserManager<Usuario> userManager, ILogger<PagoController> logger)
    {
        _db = db;
        _pasarela = pasarela;
        _pedidos = pedidos;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>¿Quien hace la solicitud puede ver/pagar este pedido?</summary>
    private bool PuedeAcceder(Pedido pedido, string? acceso)
    {
        if (pedido.UsuarioId is not null)
            return pedido.UsuarioId == _userManager.GetUserId(User) || User.IsInRole(SeedData.RolAdmin);
        return PedidoService.TokenValido(pedido, acceso) || User.IsInRole(SeedData.RolAdmin);
    }

    /// <summary>Respuesta cuando no hay acceso: pide iniciar sesión si el pedido es de una cuenta; si no, 404 (no revela que existe).</summary>
    private IActionResult SinAcceso(Pedido? pedido) =>
        pedido?.UsuarioId is not null && User.Identity?.IsAuthenticated != true ? Challenge() : NotFound();

    private object Ruta(Pedido p) => p.EsInvitado ? new { id = p.Id, acceso = p.TokenAcceso } : new { id = p.Id };

    // GET /Pago/Iniciar/5
    public async Task<IActionResult> Iniciar(int id, string? acceso)
    {
        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null || !PuedeAcceder(pedido, acceso)) return SinAcceso(pedido);
        if (pedido.Estado != EstadoPedido.PendientePago)
            return RedirectToAction(nameof(Resultado), Ruta(pedido));

        // Pedidos antiguos sin token: se genera uno para usarlo como session_id
        if (string.IsNullOrEmpty(pedido.TokenAcceso))
        {
            pedido.TokenAcceso = PedidoService.GenerarTokenAcceso();
            await _db.SaveChangesAsync();
        }

        var urlRetorno = Url.Action(nameof(Retorno), "Pago", null, Request.Scheme)!;
        try
        {
            // session_id = secreto del pedido: Transbank lo devuelve en las anulaciones por tiempo y así se valida
            var transaccion = await _pasarela.CrearTransaccionAsync(pedido.OrdenCompra, pedido.TokenAcceso!, pedido.Total, urlRetorno);
            pedido.TokenPago = transaccion.Token;
            await _db.SaveChangesAsync();

            // La vista hace un POST automático con token_ws hacia la URL de Webpay.
            ViewBag.UrlPago = transaccion.Url;
            ViewBag.Token = transaccion.Token;
            ViewBag.Pasarela = _pasarela.Nombre;
            return View(pedido);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar pago del pedido {Id}", id);
            TempData["Error"] = "No pudimos conectar con el medio de pago. Intenta nuevamente en unos minutos.";
            return RedirectToAction(nameof(Resultado), Ruta(pedido));
        }
    }

    // Transbank vuelve por GET o POST según el caso, por eso se aceptan ambos.
    // No lleva [Authorize] ni antiforgery: la petición viene desde el sitio de Transbank.
    [HttpGet, HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Retorno()
    {
        string? Leer(string clave) =>
            Request.HasFormContentType && Request.Form.ContainsKey(clave) ? Request.Form[clave].ToString()
            : Request.Query.ContainsKey(clave) ? Request.Query[clave].ToString()
            : null;

        var tokenWs = Leer("token_ws");
        var tbkToken = Leer("TBK_TOKEN");
        var tbkOrden = Leer("TBK_ORDEN_COMPRA");
        var tbkSesion = Leer("TBK_ID_SESION");

        // Caso 1: pago normal (aprobado o rechazado por el banco) → hay que confirmar (commit) con Transbank.
        if (!string.IsNullOrEmpty(tokenWs) && string.IsNullOrEmpty(tbkToken))
        {
            var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.TokenPago == tokenWs);
            if (pedido is null) return NotFound();

            if (pedido.Estado == EstadoPedido.PendientePago)
            {
                var resultado = await _pasarela.ConfirmarTransaccionAsync(tokenWs);
                await _pedidos.AplicarResultadoPagoAsync(pedido, resultado, _pasarela.Nombre);
                if (resultado.Estado == ResultadoPago.ErrorComunicacion)
                    TempData["Aviso"] = "Estamos verificando tu pago con el banco. Si se hizo el cargo, tu pedido quedará pagado: puedes revisarlo en unos minutos.";
            }
            return RedirectToAction(nameof(Resultado), Ruta(pedido));
        }

        // Caso 2: el usuario anuló en Webpay (TBK_TOKEN) o hubo un error en el formulario (token_ws + TBK_TOKEN):
        //         el TBK_TOKEN debe ser el token que creamos para ese pedido.
        // Caso 3: se agotó el tiempo (TBK_ORDEN_COMPRA + TBK_ID_SESION): el session_id debe ser el secreto del pedido.
        // Así nadie puede anular el pedido de otra persona conociendo solo su número de orden.
        Pedido? anulado = null;
        if (!string.IsNullOrEmpty(tbkToken))
        {
            anulado = await _db.Pedidos.FirstOrDefaultAsync(p => p.TokenPago == tbkToken);
        }
        else if (!string.IsNullOrEmpty(tbkOrden) && !string.IsNullOrEmpty(tbkSesion))
        {
            var candidato = await _db.Pedidos.FirstOrDefaultAsync(p => p.OrdenCompra == tbkOrden);
            if (candidato is not null && PedidoService.TokenValido(candidato, tbkSesion)) anulado = candidato;
        }

        if (anulado is null)
        {
            _logger.LogWarning("Retorno de pago con datos inválidos (orden {Orden}) desde {Ip}", tbkOrden, HttpContext.Connection.RemoteIpAddress);
            return RedirectToAction("Index", "Home");
        }

        if (anulado.Estado == EstadoPedido.PendientePago)
        {
            await _pedidos.AnularAsync(anulado, EstadoPedido.Anulado);
            TempData["Error"] = "El pago fue anulado. No se realizó ningún cargo.";
        }
        return RedirectToAction(nameof(Resultado), Ruta(anulado));
    }

    // GET /Pago/Resultado/5
    public async Task<IActionResult> Resultado(int id, string? acceso)
    {
        var pedido = await _db.Pedidos.Include(p => p.Detalles).FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null || !PuedeAcceder(pedido, acceso)) return SinAcceso(pedido);
        ViewBag.Acceso = pedido.EsInvitado ? pedido.TokenAcceso : null;
        return View(pedido);
    }

    /// <summary>Vuelve a consultar a la pasarela un pago que quedó "verificando" (sin respuesta del banco).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Verificar(int id, string? acceso)
    {
        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null || !PuedeAcceder(pedido, acceso)) return SinAcceso(pedido);
        if (pedido.Estado == EstadoPedido.PendientePago && !string.IsNullOrEmpty(pedido.TokenPago))
        {
            var resultado = await _pasarela.ConfirmarTransaccionAsync(pedido.TokenPago);
            await _pedidos.AplicarResultadoPagoAsync(pedido, resultado, _pasarela.Nombre);
        }
        return RedirectToAction(nameof(Resultado), Ruta(pedido));
    }

    // ---------- Simulador (solo si Pagos:Modo = "Simulado"; prohibido en producción, ver Program.cs) ----------

    [HttpGet]
    public IActionResult Simulador(string token_ws)
    {
        if (_pasarela is not PagoSimuladoService) return NotFound();
        var datos = PagoSimuladoService.ObtenerDatos(token_ws);
        if (datos is null) return NotFound();
        ViewBag.Token = token_ws;
        ViewBag.Orden = datos.Value.Orden;
        ViewBag.Monto = datos.Value.Monto;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulador(string token_ws, string accion)
    {
        if (_pasarela is not PagoSimuladoService) return NotFound();
        var datos = PagoSimuladoService.ObtenerDatos(token_ws);
        if (datos is null) return NotFound();

        // Imita lo que envía Webpay de vuelta en cada caso
        if (accion is "aprobar" or "rechazar")
        {
            if (!PagoSimuladoService.RegistrarDecision(token_ws, accion == "aprobar")) return NotFound();
            return RedirectToAction(nameof(Retorno), new { token_ws });
        }
        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.TokenPago == token_ws);
        return RedirectToAction(nameof(Retorno), new { TBK_TOKEN = token_ws, TBK_ORDEN_COMPRA = datos.Value.Orden, TBK_ID_SESION = pedido?.TokenAcceso });
    }
}
