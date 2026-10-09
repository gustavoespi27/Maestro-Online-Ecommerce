using Ferreteria.Services;
using Microsoft.AspNetCore.RateLimiting;
using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers;

public class CuentaController : Controller
{
    private readonly UserManager<Usuario> _userManager;
    private readonly SignInManager<Usuario> _signInManager;

    public CuentaController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginVM { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaLogin)]
    public async Task<IActionResult> Login(LoginVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var resultado = await _signInManager.PasswordSignInAsync(vm.Email, vm.Password, vm.Recordarme, lockoutOnFailure: true);
        if (resultado.Succeeded)
        {
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return LocalRedirect(vm.ReturnUrl);

            var usuario = await _userManager.FindByEmailAsync(vm.Email);
            if (usuario is not null && await _userManager.IsInRoleAsync(usuario, SeedData.RolAdmin))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, resultado.IsLockedOut
            ? "Cuenta bloqueada por demasiados intentos. Intenta de nuevo en unos minutos."
            : "Correo o contraseña incorrectos.");
        return View(vm);
    }

    [HttpGet]
    public IActionResult Registro(string? returnUrl = null) => View(new RegistroVM { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaLogin), ValidarHumano]
    public async Task<IActionResult> Registro(RegistroVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var usuario = new Usuario
        {
            UserName = vm.Email,
            Email = vm.Email,
            NombreCompleto = vm.NombreCompleto.Trim(),
            Rut = vm.Rut?.ToUpperInvariant(),
            PhoneNumber = vm.Telefono,
            FechaAceptacionTerminos = DateTime.Now,
            AceptaComunicaciones = vm.AceptaComunicaciones
        };

        var resultado = await _userManager.CreateAsync(usuario, vm.Password);
        if (!resultado.Succeeded)
        {
            foreach (var descripcion in resultado.Errors.Select(e => e.Description).Distinct())
                ModelState.AddModelError(string.Empty, descripcion);
            return View(vm);
        }

        await _userManager.AddToRoleAsync(usuario, SeedData.RolCliente);
        await _signInManager.SignInAsync(usuario, isPersistent: false);
        TempData["Exito"] = $"¡Bienvenido/a, {usuario.NombreCompleto}! Tu cuenta fue creada.";

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return LocalRedirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u is null) return Challenge();
        return View(new PerfilVM
        {
            Email = u.Email ?? "",
            NombreCompleto = u.NombreCompleto,
            Rut = u.Rut,
            Telefono = u.PhoneNumber,
            Direccion = u.Direccion,
            Comuna = u.Comuna,
            AceptaComunicaciones = u.AceptaComunicaciones
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Perfil(PerfilVM vm)
    {
        var u = await _userManager.GetUserAsync(User);
        if (u is null) return Challenge();
        vm.Email = u.Email ?? "";
        if (!ModelState.IsValid) return View(vm);

        u.NombreCompleto = vm.NombreCompleto.Trim();
        u.Rut = vm.Rut?.ToUpperInvariant();
        u.PhoneNumber = vm.Telefono;
        u.Direccion = vm.Direccion;
        u.Comuna = vm.Comuna;
        u.AceptaComunicaciones = vm.AceptaComunicaciones;
        await _userManager.UpdateAsync(u);

        TempData["Exito"] = "Tus datos fueron actualizados.";
        return RedirectToAction(nameof(Perfil));
    }

    public IActionResult AccesoDenegado() => View();
}
