using Ferreteria.Models;
using Ferreteria.Models.ViewModels;
using Ferreteria.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Ferreteria.Controllers;

/// <summary>
/// Formulario de compra: datos de entrega → crea el pedido → pasa al pago.
/// Se puede comprar con cuenta o como invitado (solo con un correo de contacto).
/// </summary>
public class CheckoutController : Controller
{
    private readonly PedidoService _pedidos;
    private readonly UserManager<Usuario> _userManager;
    private readonly OpcionesTienda _tienda;

    public CheckoutController(PedidoService pedidos, UserManager<Usuario> userManager, IOptions<OpcionesTienda> tienda)
    {
        _pedidos = pedidos;
        _userManager = userManager;
        _tienda = tienda.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var carrito = await _pedidos.ObtenerCarritoAsync();
        if (carrito.EstaVacio) return RedirectToAction("Index", "Carrito");

        var u = User.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var vm = new CheckoutVM
        {
            Carrito = carrito,
            EsInvitado = u is null,
            NombreReceptor = u?.NombreCompleto ?? "",
            Telefono = u?.PhoneNumber ?? "",
            Direccion = u?.Direccion,
            Comuna = u?.Comuna,
            CostoDespachoConfigurado = _tienda.CostoDespacho
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaPagos)]
    public async Task<IActionResult> Index(CheckoutVM vm)
    {
        var u = User.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        vm.EsInvitado = u is null;

        if (vm.EsInvitado && string.IsNullOrWhiteSpace(vm.EmailInvitado))
            ModelState.AddModelError(nameof(vm.EmailInvitado), "Ingresa tu correo para enviarte el comprobante y el seguimiento");
        if (vm.TipoEntrega == TipoEntrega.Despacho)
        {
            if (string.IsNullOrWhiteSpace(vm.Direccion))
                ModelState.AddModelError(nameof(vm.Direccion), "Ingresa la dirección de despacho");
            if (string.IsNullOrWhiteSpace(vm.Comuna))
                ModelState.AddModelError(nameof(vm.Comuna), "Ingresa la comuna");
            if (!Envios.EsRegionValida(vm.Region))
                ModelState.AddModelError(nameof(vm.Region), "Selecciona una región");
        }

        if (!ModelState.IsValid)
        {
            vm.Carrito = await _pedidos.ObtenerCarritoAsync(vm.TipoEntrega, vm.Region);
            vm.CostoDespachoConfigurado = _tienda.CostoDespacho;
            return View(vm);
        }

        var (pedido, error) = await _pedidos.CrearPedidoAsync(u?.Id, vm);
        if (pedido is null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Index", "Carrito");
        }

        // Guardar la dirección en el perfil para la próxima compra (solo clientes con cuenta)
        if (u is not null && vm.TipoEntrega == TipoEntrega.Despacho)
        {
            u.Direccion = vm.Direccion;
            u.Comuna = vm.Comuna;
            u.PhoneNumber ??= vm.Telefono;
            await _userManager.UpdateAsync(u);
        }

        return pedido.EsInvitado
            ? RedirectToAction("Iniciar", "Pago", new { id = pedido.Id, acceso = pedido.TokenAcceso })
            : RedirectToAction("Iniciar", "Pago", new { id = pedido.Id });
    }
}
