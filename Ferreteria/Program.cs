using System.Globalization;
using System.Threading.RateLimiting;
using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var esDesarrollo = builder.Environment.IsDevelopment();

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;                          // no revelar el servidor web
    k.Limits.MaxRequestBodySize = 10 * 1024 * 1024;     // 10 MB por solicitud como máximo
});
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 4 * 1024 * 1024;       // archivos subidos: 4 MB (las imágenes se limitan a 2 MB)
    o.ValueCountLimit = 200;                            // evita formularios con miles de campos
});

// ---------- Base de datos (SQL Server + Entity Framework Core) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection' en appsettings.json");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

// ---------- Llaves de cifrado (cookies, tokens y datos personales cifrados) ----------
// En Windows se guardan cifradas con DPAPI en el perfil del usuario. En servidores/Docker se indica una carpeta
// persistente con Seguridad:DirectorioLlaves; si se pierden, los datos cifrados no se pueden leer.
var proteccion = builder.Services.AddDataProtection().SetApplicationName("ElMaestroOnline");
if (builder.Configuration["Seguridad:DirectorioLlaves"] is { Length: > 0 } dirLlaves)
    proteccion.PersistKeysToFileSystem(new DirectoryInfo(dirLlaves));

// ---------- Usuarios y roles (ASP.NET Core Identity) ----------
builder.Services
    .AddIdentity<Usuario, IdentityRole>(options =>
    {
        // Contraseñas: mínimo 8 caracteres con mayúscula, minúscula y número.
        // Se guardan con hash PBKDF2-HMAC-SHA512, sal aleatoria y 100.000 iteraciones (Identity v3), nunca en texto plano.
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

// ---------- Cookies ----------
// En producción: solo HTTPS (Secure) y prefijo "__Host-" (el navegador exige HTTPS, ruta "/" y sin dominio,
// así ningún subdominio puede sobrescribirlas). En desarrollo se permite http://localhost.
var politicaCookieSegura = esDesarrollo ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
string NombreCookie(string nombre) => esDesarrollo ? "." + nombre : "__Host-" + nombre;

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Cuenta/Login";
    options.LogoutPath = "/Cuenta/Logout";
    options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.Name = NombreCookie("ElMaestro.Sesion");
    options.Cookie.HttpOnly = true;                 // JavaScript no puede leer la cookie de sesión
    options.Cookie.SameSite = SameSiteMode.Lax;     // no se envía en solicitudes cruzadas (CSRF)
    options.Cookie.SecurePolicy = politicaCookieSegura;
    options.Cookie.Path = "/";
});

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = NombreCookie("ElMaestro.Antiforgery");
    o.Cookie.SecurePolicy = politicaCookieSegura;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.Path = "/";
    o.HeaderName = "RequestVerificationToken";      // lo usa el chatbot (fetch)
});

// ---------- Carrito en sesión ----------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = NombreCookie("ElMaestro.Carrito");
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = politicaCookieSegura;
    options.Cookie.Path = "/";
});
builder.Services.AddHttpContextAccessor();

// ---------- Seguridad: límites por IP, nonce CSP, anti-bots ----------
builder.Services.AgregarLimitesDeSolicitudes();
builder.Services.AddScoped<CspNonce>();
builder.Services.AddSingleton<ProteccionBots>();
builder.Services.AddHostedService<LimpiezaPedidosPendientes>();

// HTTPS detrás de un proxy inverso (Nginx, Azure, Render…): solo si se configura, para que nadie pueda
// falsificar su IP o el protocolo enviando estas cabeceras directamente.
if (builder.Configuration.GetValue<bool>("Seguridad:DetrasDeProxy"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var ip in (builder.Configuration["Seguridad:ProxiesConfiables"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            o.KnownProxies.Add(System.Net.IPAddress.Parse(ip.Trim()));
    });
}

builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
});
builder.Services.AddHttpsRedirection(o => o.RedirectStatusCode = StatusCodes.Status308PermanentRedirect);

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

builder.Services.AddControllersWithViews(o =>
{
    // Todas las solicitudes POST/PUT/DELETE exigen token antiforgery (protección CSRF global)
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Todo controlador del área Admin exige el rol Admin, aunque alguien olvide poner [Authorize]
    o.Conventions.Add(new AreaRequiereRol("Admin", SeedData.RolAdmin));
});

var app = builder.Build();

// ---------- Crear BD, roles, admin y catálogo de ejemplo ----------
using (var scope = app.Services.CreateScope())
{
    await SeedData.InicializarAsync(scope.ServiceProvider, app.Configuration);
}

// ---------- Pipeline HTTP ----------
if (app.Configuration.GetValue<bool>("Seguridad:DetrasDeProxy"))
    app.UseForwardedHeaders();

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
