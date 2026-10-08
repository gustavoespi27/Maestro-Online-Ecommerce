using Ferreteria.Data;
using Ferreteria.Models;
using Ferreteria.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Services;

public class OpcionesTienda
{
    public string Nombre { get; set; } = "El Maestro Online";
    public string Email { get; set; } = "contacto@elmaestroonline.cl";
    /// <summary>Número de WhatsApp en formato internacional sin "+" ni espacios (ej: 56912345678).</summary>
    public string WhatsApp { get; set; } = "56912345678";
    public string TelefonoVisible { get; set; } = "+56 9 1234 5678";
    public string Direccion { get; set; } = "Santiago, Chile";
    public int CostoDespacho { get; set; } = 4990;
    public int DespachoGratisDesde { get; set; } = 50000;
}

/// <summary>
/// Lógica de negocio de pedidos: armar el carrito, crear el pedido reservando stock,
/// aplicar el resultado del pago y anular pedidos devolviendo el stock.
/// </summary>
public class PedidoService
{
    private readonly ApplicationDbContext _db;
    private readonly CarritoService _carrito;
    private readonly OpcionesTienda _opciones;

    private readonly ILogger<PedidoService> _logger;

    public PedidoService(ApplicationDbContext db, CarritoService carrito, Microsoft.Extensions.Options.IOptions<OpcionesTienda> opciones,
        ILogger<PedidoService> logger)
    {
        _logger = logger;
        _db = db;
        _carrito = carrito;
        _opciones = opciones.Value;
    }

    /// <summary>Costo de despacho según región (ver <see cref="Envios"/>). Retiro en tienda siempre es gratis.</summary>
    public int CalcularDespacho(int subtotal, TipoEntrega tipo, string? region = null)
    {
        if (tipo == TipoEntrega.RetiroEnTienda || subtotal == 0) return 0;
        return Envios.Costo(region, subtotal, _opciones.DespachoGratisDesde);
    }

    /// <summary>Arma el carrito con los datos actuales de cada producto (precio y stock).</summary>
    public async Task<CarritoVM> ObtenerCarritoAsync(TipoEntrega tipo = TipoEntrega.Despacho, string? region = null)
    {
        var items = _carrito.ObtenerItems();
        var ids = items.Select(i => i.ProductoId).ToList();
        var productos = await _db.Productos.Where(p => ids.Contains(p.Id)).ToListAsync();

        var vm = new CarritoVM { DespachoGratisDesde = _opciones.DespachoGratisDesde };
        foreach (var item in items)
        {
            var producto = productos.FirstOrDefault(p => p.Id == item.ProductoId);
            if (producto is null) { _carrito.Quitar(item.ProductoId); continue; } // producto eliminado
            vm.Lineas.Add(new LineaCarritoVM { Producto = producto, Cantidad = item.Cantidad });
        }
        vm.CostoDespacho = CalcularDespacho(vm.Subtotal, tipo, region);
        return vm;
    }

    /// <summary>
    /// Crea el pedido en estado "Pendiente de pago" y descuenta (reserva) el stock.
    /// Si el pago se rechaza o se anula, el stock se devuelve en <see cref="AnularAsync"/>.
    /// </summary>
    public async Task<(Pedido? Pedido, string? Error)> CrearPedidoAsync(string? usuarioId, CheckoutVM datos)
    {
        var carrito = await ObtenerCarritoAsync(datos.TipoEntrega, datos.Region);
        if (carrito.EstaVacio) return (null, "Tu carrito está vacío.");
        if (carrito.TieneProblemasDeStock) return (null, "Algunos productos ya no tienen stock suficiente. Revisa tu carrito.");

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        // Descuento atómico: solo se actualiza si todavía hay stock (evita vender de más si dos personas compran a la vez).
        foreach (var linea in carrito.Lineas)
        {
            var id = linea.Producto.Id;
            var cantidad = linea.Cantidad;
            var filas = await _db.Productos
                .Where(p => p.Id == id && p.Stock >= cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - cantidad));
            if (filas == 0)
            {
                await transaccion.RollbackAsync();
                return (null, $"No queda stock suficiente de \"{linea.Producto.Nombre}\".");
            }
        }

        var pedido = new Pedido
        {
            OrdenCompra = GenerarOrdenCompra(),
            UsuarioId = usuarioId,
            EmailInvitado = usuarioId is null ? datos.EmailInvitado?.Trim().ToLowerInvariant() : null,
            TokenAcceso = GenerarTokenAcceso(),
            Fecha = DateTime.Now,
            Estado = EstadoPedido.PendientePago,
            TipoEntrega = datos.TipoEntrega,
            NombreReceptor = datos.NombreReceptor.Trim(),
            Telefono = datos.Telefono.Trim(),
            Direccion = datos.TipoEntrega == TipoEntrega.Despacho ? datos.Direccion?.Trim() : null,
            Comuna = datos.TipoEntrega == TipoEntrega.Despacho ? datos.Comuna?.Trim() : null,
            Region = datos.TipoEntrega == TipoEntrega.Despacho ? datos.Region : null,
            FechaEstimadaEntrega = datos.TipoEntrega == TipoEntrega.Despacho
                ? Envios.RangoEntrega(datos.Region, DateTime.Now).Hasta
                : Envios.SumarDiasHabiles(DateTime.Now, Envios.DiasPreparacionRetiro),
            Notas = datos.Notas?.Trim(),
            Subtotal = carrito.Subtotal,
            CostoDespacho = carrito.CostoDespacho,
            Total = carrito.Total,
            Detalles = carrito.Lineas.Select(l => new DetallePedido
            {
                ProductoId = l.Producto.Id,
                NombreProducto = l.Producto.Nombre,
                PrecioUnitario = l.Producto.Precio,
                Cantidad = l.Cantidad
            }).ToList()
        };

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();
        await transaccion.CommitAsync();

        _carrito.Vaciar();
        return (pedido, null);
    }

    /// <summary>
    /// Guarda el resultado que devolvió la pasarela de pago.
    /// Los cambios de estado son atómicos (UPDATE ... WHERE Estado = PendientePago): si llegan dos confirmaciones
    /// a la vez (doble clic, recarga o reintento), solo una puede cambiar el pedido.
    /// </summary>
    public async Task AplicarResultadoPagoAsync(Pedido pedido, ResultadoPago resultado, string metodo)
    {
        if (pedido.Estado != EstadoPedido.PendientePago) return; // ya procesado (ej: el usuario recargó la página)

        // Si no pudimos comunicarnos con la pasarela, no se sabe si hubo cargo: el pedido queda pendiente
        // y se puede volver a verificar (nunca se anula a ciegas un pago que podría estar aprobado).
        if (resultado.Estado == ResultadoPago.ErrorComunicacion) return;

        // Validación de seguridad: el monto y la orden deben coincidir con lo que guardamos.
        var coincide = resultado.Monto == pedido.Total && resultado.OrdenCompra == pedido.OrdenCompra;
        if (resultado.Aprobado && !coincide)
            _logger.LogCritical("Pago aprobado que NO coincide con el pedido {Orden}: monto {Monto} vs {Total}, orden {OrdenPasarela}",
                pedido.OrdenCompra, resultado.Monto, pedido.Total, resultado.OrdenCompra);

        if (resultado.Aprobado && coincide)
        {
            var ahora = DateTime.Now;
            var filas = await _db.Pedidos
                .Where(p => p.Id == pedido.Id && p.Estado == EstadoPedido.PendientePago)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Estado, EstadoPedido.Pagado)
                    .SetProperty(p => p.FechaPago, ahora)
                    .SetProperty(p => p.MetodoPago, metodo)
                    .SetProperty(p => p.CodigoAutorizacion, resultado.CodigoAutorizacion)
                    .SetProperty(p => p.UltimosDigitosTarjeta, resultado.UltimosDigitosTarjeta)
                    .SetProperty(p => p.TipoPagoWebpay, resultado.TipoPago)
                    .SetProperty(p => p.Cuotas, resultado.Cuotas));
            await _db.Entry(pedido).ReloadAsync();
            if (filas == 0) _logger.LogInformation("El pedido {Orden} ya había sido procesado por otra solicitud", pedido.OrdenCompra);
        }
        else
        {
            await AnularAsync(pedido, EstadoPedido.Rechazado);
        }
    }

    /// <summary>
    /// Anula un pedido (o lo marca como rechazado) y devuelve el stock reservado.
    /// Solo actúa si el pedido sigue en el mismo estado en que se leyó (control de concurrencia optimista),
    /// así el stock nunca se devuelve dos veces.
    /// </summary>
    public async Task AnularAsync(Pedido pedido, EstadoPedido estadoFinal = EstadoPedido.Anulado)
    {
        if (pedido.Estado is EstadoPedido.Anulado or EstadoPedido.Rechazado or EstadoPedido.Despachado or EstadoPedido.Entregado) return;

        var estadoLeido = pedido.Estado;
        await using var transaccion = await _db.Database.BeginTransactionAsync();
        var filas = await _db.Pedidos
            .Where(p => p.Id == pedido.Id && p.Estado == estadoLeido)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estado, estadoFinal));
        if (filas == 0)
        {
            await transaccion.RollbackAsync();
            await _db.Entry(pedido).ReloadAsync();
            return;
        }

        var detalles = await _db.DetallesPedido.Where(d => d.PedidoId == pedido.Id).ToListAsync();
        foreach (var d in detalles)
        {
            var productoId = d.ProductoId;
            var cantidad = d.Cantidad;
            await _db.Productos
                .Where(p => p.Id == productoId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + cantidad));
        }
        await transaccion.CommitAsync();
        await _db.Entry(pedido).ReloadAsync();
    }

    /// <summary>Secreto de 256 bits en Base64 URL (43 caracteres), generado con un generador criptográfico.</summary>
    public static string GenerarTokenAcceso() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Compara el token del pedido en tiempo constante (evita ataques de temporización).</summary>
    public static bool TokenValido(Pedido pedido, string? token)
    {
        if (string.IsNullOrEmpty(pedido.TokenAcceso) || string.IsNullOrEmpty(token)) return false;
        var a = System.Text.Encoding.UTF8.GetBytes(pedido.TokenAcceso);
        var b = System.Text.Encoding.UTF8.GetBytes(token);
        return a.Length == b.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string GenerarOrdenCompra() =>
        // Ej: F240924153012-4821 (máx. 26 caracteres, exigido por Webpay)
        $"F{DateTime.Now:yyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
}
