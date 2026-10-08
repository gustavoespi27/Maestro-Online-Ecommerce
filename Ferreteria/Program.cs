using System.Globalization;
using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var esDesarrollo = builder.Environment.IsDevelopment();

// No revelar el servidor web en las respuestas
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

// ---------- Base de datos (SQL Server + Entity Framework Core) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection' en appsettings.json");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

// ---------- Usuarios y roles (ASP.NET Core Identity) ----------
builder.Services
    .AddIdentity<Usuario, IdentityRole>(options =>
    {
        // Contraseñas: mínimo 8 caracteres con mayúscula, minúscula y número (se guardan con hash PBKDF2)
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        // Bloqueo ante fuerza bruta: 5 intentos fallidos → 15 minutos bloqueada (también para cuentas nuevas)
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<IdentityErrorDescriberEs>();

// Si cambia la contraseña o el rol, las sesiones abiertas se invalidan en máximo 5 minutos
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

var politicaCookieSegura = esDesarrollo ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.LogoutPath = "/Cuenta/Logout";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.Name = ".ElMaestro.Sesion";
    options.Cookie.HttpOnly = true;                 // JavaScript no puede leer la cookie de sesión
    options.Cookie.SameSite = SameSiteMode.Lax;     // no se envía en solicitudes cruzadas (CSRF)
    options.Cookie.SecurePolicy = politicaCookieSegura;
});

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = ".ElMaestro.Antiforgery";
    o.Cookie.SecurePolicy = politicaCookieSegura;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.HeaderName = "RequestVerificationToken";      // lo usa el chatbot (fetch)
});

// ---------- Carrito en sesión ----------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Ferreteria.Carrito";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = politicaCookieSegura;
});
builder.Services.AddHttpContextAccessor();

// ---------- Seguridad: límites por IP, nonce CSP ----------
builder.Services.AgregarLimitesDeSolicitudes();
builder.Services.AddScoped<CspNonce>();

// ---------- Servicios de la aplicación ----------
builder.Services.Configure<OpcionesTienda>(builder.Configuration.GetSection("Tienda"));
builder.Services.Configure<OpcionesPago>(builder.Configuration.GetSection("Pagos"));
builder.Services.AddScoped<CarritoService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<RecomendacionService>();
builder.Services.AddScoped<ChatbotService>();

// Pasarela de pago: Webpay (Transbank) o simulada, según appsettings.json → Pagos:Modo
var modoPago = builder.Configuration["Pagos:Modo"] ?? "Simulado";
if (modoPago.Equals("Simulado", StringComparison.OrdinalIgnoreCase))
{
    // El simulador permite aprobar pagos sin banco: jamás debe usarse fuera de desarrollo
    if (!esDesarrollo)
        throw new InvalidOperationException("Pagos:Modo = \"Simulado\" solo está permitido en el ambiente Development.");
    builder.Services.AddScoped<IPasarelaPago, PagoSimuladoService>();
}
else
{
    // Las llaves de Transbank nunca van en el repositorio: secretos de usuario (desarrollo) o variables de entorno
    if (string.IsNullOrWhiteSpace(builder.Configuration["Pagos:CodigoComercio"]) || string.IsNullOrWhiteSpace(builder.Configuration["Pagos:ApiKeySecret"]))
        throw new InvalidOperationException("Pagos:Modo = \"Webpay\" requiere Pagos:CodigoComercio y Pagos:ApiKeySecret (ver README, sección Webpay).");
    builder.Services.AddHttpClient<IPasarelaPago, WebpayPlusService>(c => c.Timeout = TimeSpan.FromSeconds(30));
}

// Todas las solicitudes POST/PUT/DELETE exigen token antiforgery (protección CSRF global)
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

// ---------- Crear BD, roles, admin y catálogo de ejemplo ----------
using (var scope = app.Services.CreateScope())
{
    await SeedData.InicializarAsync(scope.ServiceProvider, app.Configuration);
}

// ---------- Pipeline HTTP ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UsarCabecerasDeSeguridad(app.Configuration);

// Formatos de fecha y número chilenos
var chile = new CultureInfo("es-CL");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(chile),
    SupportedCultures = new[] { chile },
    SupportedUICultures = new[] { chile }
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
