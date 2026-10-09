using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Services;

/// <summary>
/// Protección contra bots sin captcha (no requiere servicios externos ni recolecta datos):
///  1. Campo trampa ("sitioWeb"): invisible para las personas; los bots que rellenan todo lo completan.
///  2. Marca de tiempo firmada: el formulario debe haberse mostrado hace más de 2 segundos y menos de 2 horas
///     (los bots envían al instante y no pueden falsificar la firma).
/// Se usa con el partial _AntiBot en el formulario y el atributo [ValidarHumano] en la acción POST.
/// </summary>
public class ProteccionBots
{
    public const string CampoTrampa = "sitioWeb";
    public const string CampoMarca = "marcaFormulario";
    private static readonly TimeSpan Minimo = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Maximo = TimeSpan.FromHours(2);
    private readonly ITimeLimitedDataProtector _protector;

    public ProteccionBots(IDataProtectionProvider proveedor) =>
        _protector = proveedor.CreateProtector("ElMaestro.AntiBot").ToTimeLimitedDataProtector();

    public string GenerarMarca() => _protector.Protect(DateTime.UtcNow.Ticks.ToString(), Maximo);

    public bool EsHumano(IFormCollection form)
    {
        if (!string.IsNullOrEmpty(form[CampoTrampa])) return false;
        try
        {
            var ticks = long.Parse(_protector.Unprotect(form[CampoMarca].ToString()));
            return DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) >= Minimo;
        }
        catch
        {
            return false; // marca ausente, alterada o vencida
        }
    }
}

/// <summary>Rechaza envíos automatizados (ver <see cref="ProteccionBots"/>). El formulario debe incluir el partial _AntiBot.</summary>
public class ValidarHumanoAttribute : TypeFilterAttribute
{
    public ValidarHumanoAttribute() : base(typeof(FiltroValidarHumano)) { }

    private class FiltroValidarHumano : IAsyncActionFilter
    {
        private readonly ProteccionBots _bots;
        private readonly ILogger<FiltroValidarHumano> _logger;

        public FiltroValidarHumano(ProteccionBots bots, ILogger<FiltroValidarHumano> logger)
        {
            _bots = bots;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate siguiente)
        {
            var req = ctx.HttpContext.Request;
            if (HttpMethods.IsPost(req.Method) && req.HasFormContentType && !_bots.EsHumano(await req.ReadFormAsync()))
            {
                _logger.LogWarning("Envío bloqueado por protección anti-bots en {Ruta} desde {Ip}", req.Path, ctx.HttpContext.Connection.RemoteIpAddress);
                ctx.ModelState.AddModelError(string.Empty, "No pudimos verificar el envío. Espera unos segundos, recarga la página e inténtalo de nuevo.");
            }
            await siguiente();
        }
    }
}

/// <summary>Exige un rol para todos los controladores de un área (defensa en profundidad para el panel admin).</summary>
public class AreaRequiereRol : IControllerModelConvention
{
    private readonly string _area;
    private readonly AuthorizationPolicy _politica;

    public AreaRequiereRol(string area, string rol)
    {
        _area = area;
        _politica = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(rol).Build();
    }

    public void Apply(ControllerModel controlador)
    {
        var area = controlador.Attributes.OfType<Microsoft.AspNetCore.Mvc.AreaAttribute>().FirstOrDefault()?.RouteValue;
        if (string.Equals(area, _area, StringComparison.OrdinalIgnoreCase))
            controlador.Filters.Add(new AuthorizeFilter(_politica));
    }
}

/// <summary>
/// Anula los pedidos que quedaron "pendientes de pago" por más de 60 minutos y devuelve su stock.
/// Evita que alguien (o un bot) bloquee el inventario creando pedidos que nunca paga.
/// Si el pago no se confirmó (commit), Transbank no hace el cargo, así que anularlo es seguro.
/// </summary>
public class LimpiezaPedidosPendientes : BackgroundService
{
    public static readonly TimeSpan Vencimiento = TimeSpan.FromMinutes(60);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LimpiezaPedidosPendientes> _logger;

    public LimpiezaPedidosPendientes(IServiceScopeFactory scopes, ILogger<LimpiezaPedidosPendientes> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var pedidos = scope.ServiceProvider.GetRequiredService<PedidoService>();
                var limite = DateTime.Now - Vencimiento;
                var vencidos = await db.Pedidos.Where(p => p.Estado == EstadoPedido.PendientePago && p.Fecha < limite).ToListAsync(detener);
                foreach (var p in vencidos) await pedidos.AnularAsync(p, EstadoPedido.Anulado);
                if (vencidos.Count > 0) _logger.LogInformation("Se anularon {Cantidad} pedidos sin pagar y se devolvió su stock", vencidos.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al anular pedidos pendientes vencidos");
            }
        } while (await reloj.WaitForNextTickAsync(detener));
    }
}
