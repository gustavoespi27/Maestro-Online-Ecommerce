using System.Text.Json;

namespace Ferreteria.Services;

public class ItemCarrito
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

/// <summary>
/// Carrito de compras guardado en la sesión del usuario (no requiere login para agregar productos).
/// Solo guarda ids y cantidades; los precios siempre se leen desde la base de datos.
/// </summary>
public class CarritoService
{
    private const string ClaveSesion = "Carrito";
    public const int CantidadMaximaPorProducto = 99;

    private readonly IHttpContextAccessor _http;

    public CarritoService(IHttpContextAccessor http) => _http = http;

    private ISession Sesion => _http.HttpContext?.Session
        ?? throw new InvalidOperationException("No hay una sesión HTTP activa.");

    public List<ItemCarrito> ObtenerItems()
    {
        var json = Sesion.GetString(ClaveSesion);
        return string.IsNullOrEmpty(json)
            ? new List<ItemCarrito>()
            : JsonSerializer.Deserialize<List<ItemCarrito>>(json) ?? new List<ItemCarrito>();
    }

    private void Guardar(List<ItemCarrito> items) =>
        Sesion.SetString(ClaveSesion, JsonSerializer.Serialize(items));

    public void Agregar(int productoId, int cantidad)
    {
        if (cantidad < 1) cantidad = 1;
        var items = ObtenerItems();
        var item = items.FirstOrDefault(i => i.ProductoId == productoId);
        if (item is null)
            items.Add(new ItemCarrito { ProductoId = productoId, Cantidad = Math.Min(cantidad, CantidadMaximaPorProducto) });
        else
            item.Cantidad = Math.Min(item.Cantidad + cantidad, CantidadMaximaPorProducto);
        Guardar(items);
    }

    public void Actualizar(int productoId, int cantidad)
    {
        var items = ObtenerItems();
        var item = items.FirstOrDefault(i => i.ProductoId == productoId);
        if (item is null) return;
        if (cantidad <= 0) items.Remove(item);
        else item.Cantidad = Math.Min(cantidad, CantidadMaximaPorProducto);
        Guardar(items);
    }

    public void Quitar(int productoId) => Actualizar(productoId, 0);

    public void Vaciar() => Sesion.Remove(ClaveSesion);

    public int CantidadTotal() => ObtenerItems().Sum(i => i.Cantidad);
}
