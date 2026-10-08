using System.Text.Json;
using Ferreteria.Data;
using Ferreteria.Models;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Services;

/// <summary>Una recomendación con el motivo que se le muestra al cliente (transparencia).</summary>
public record Recomendacion(Producto Producto, string Motivo);

/// <summary>
/// Recomendaciones personalizadas de compra. Combina tres señales, de la más fuerte a la más débil:
///  1. "Comprados juntos": productos que aparecen en los mismos pedidos pagados (filtrado colaborativo simple).
///  2. Interés del cliente: categorías de lo que vio recientemente (sesión) y de lo que compró antes.
///  3. Popularidad: lo más vendido, destacados y ofertas (para clientes nuevos sin historial).
/// No usa datos sensibles ni los comparte con terceros (ver Política de Privacidad).
/// </summary>
public class RecomendacionService
{
    private const string ClaveVistos = "VistosRecientes";
    private const int MaximoVistos = 12;

    private static readonly EstadoPedido[] EstadosVenta =
        { EstadoPedido.Pagado, EstadoPedido.EnPreparacion, EstadoPedido.Despachado, EstadoPedido.Entregado };

    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public RecomendacionService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private ISession? Sesion => _http.HttpContext?.Session;

    // ---------- Historial de productos vistos (en la sesión, no se guarda en la BD) ----------

    public List<int> ObtenerVistos()
    {
        var json = Sesion?.GetString(ClaveVistos);
        return string.IsNullOrEmpty(json) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
    }

    public void RegistrarVisto(int productoId)
    {
        if (Sesion is null) return;
        var vistos = ObtenerVistos();
        vistos.Remove(productoId);
        vistos.Insert(0, productoId);
        Sesion.SetString(ClaveVistos, JsonSerializer.Serialize(vistos.Take(MaximoVistos).ToList()));
    }

    // ---------- Recomendaciones ----------

    /// <summary>Productos que otros clientes compraron junto con los indicados.</summary>
    private async Task<List<int>> CompradosJuntosAsync(IReadOnlyCollection<int> productoIds, int cantidad)
    {
        if (productoIds.Count == 0) return new List<int>();
        var ids = productoIds.ToList();
        return await _db.DetallesPedido
            .Where(d => EstadosVenta.Contains(d.Pedido!.Estado)
                        && d.Pedido.Detalles.Any(o => ids.Contains(o.ProductoId))
                        && !ids.Contains(d.ProductoId))
            .GroupBy(d => d.ProductoId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(cantidad)
            .ToListAsync();
    }

    private IQueryable<Producto> Disponibles(IEnumerable<int> excluir)
    {
        var lista = excluir.ToList();
        return _db.Productos.Where(p => p.Activo && p.Stock > 0 && !lista.Contains(p.Id));
    }

    private async Task<List<Producto>> CargarEnOrdenAsync(List<int> ids)
    {
        var productos = await _db.Productos.Where(p => ids.Contains(p.Id) && p.Activo && p.Stock > 0).ToListAsync();
        return ids.Select(id => productos.FirstOrDefault(p => p.Id == id)).OfType<Producto>().ToList();
    }

    /// <summary>"Recomendado para ti" en la portada.</summary>
    public async Task<List<Recomendacion>> ParaClienteAsync(string? usuarioId, IEnumerable<int> enCarrito, int cantidad = 8)
    {
        var vistos = ObtenerVistos();
        var comprados = usuarioId is null
            ? new List<int>()
            : await _db.DetallesPedido
                .Where(d => d.Pedido!.UsuarioId == usuarioId && EstadosVenta.Contains(d.Pedido.Estado))
                .Select(d => d.ProductoId).Distinct().ToListAsync();

        var excluir = new HashSet<int>(comprados.Concat(enCarrito));
        var resultado = new List<Recomendacion>();
        void Agregar(IEnumerable<Producto> productos, string motivo)
        {
            foreach (var p in productos)
            {
                if (resultado.Count >= cantidad) return;
                if (excluir.Add(p.Id)) resultado.Add(new Recomendacion(p, motivo));
            }
        }

        // 1) Comprados junto con lo que vio o compró
        var base1 = vistos.Concat(comprados).Distinct().ToList();
        Agregar(await CargarEnOrdenAsync(await CompradosJuntosAsync(base1, cantidad)), "Otros clientes lo compraron junto a lo que viste");

        // 2) De las categorías que le interesan (primero lo visto recientemente)
        var categoriasVistas = await _db.Productos.Where(p => vistos.Contains(p.Id)).Select(p => p.CategoriaId).Distinct().ToListAsync();
        var categoriasCompradas = await _db.Productos.Where(p => comprados.Contains(p.Id)).Select(p => p.CategoriaId).Distinct().ToListAsync();
        var categorias = categoriasVistas.Concat(categoriasCompradas).Distinct().ToList();
        if (categorias.Count > 0)
        {
            var deInteres = await Disponibles(excluir.Concat(vistos))
                .Include(p => p.Categoria)
                .Where(p => categorias.Contains(p.CategoriaId))
                .OrderByDescending(p => p.PrecioAnterior > p.Precio).ThenByDescending(p => p.Destacado)
                .Take(cantidad).ToListAsync();
            Agregar(deInteres, "Porque te interesa " + "{categoria}");
        }

        // 3) Lo más vendido / destacado (clientes nuevos)
        var masVendidos = await _db.DetallesPedido
            .Where(d => EstadosVenta.Contains(d.Pedido!.Estado))
            .GroupBy(d => d.ProductoId).OrderByDescending(g => g.Sum(x => x.Cantidad)).Select(g => g.Key)
            .Take(cantidad).ToListAsync();
        Agregar(await CargarEnOrdenAsync(masVendidos), "Lo más vendido de la tienda");
        Agregar(await Disponibles(excluir).OrderByDescending(p => p.Destacado).ThenByDescending(p => p.PrecioAnterior > p.Precio)
            .Take(cantidad).ToListAsync(), "Favorito de los maestros");

        // Reemplaza el marcador de categoría por su nombre real
        return resultado.Select(r => r.Motivo.Contains("{categoria}")
            ? r with { Motivo = r.Motivo.Replace("{categoria}", (r.Producto.Categoria?.Nombre ?? "esta categoría").ToLower()) }
            : r).ToList();
    }

    /// <summary>"Clientes que compraron esto también compraron" en el detalle del producto.</summary>
    public async Task<List<Recomendacion>> ParaProductoAsync(Producto producto, int cantidad = 4)
    {
        var excluir = new HashSet<int> { producto.Id };
        var resultado = (await CargarEnOrdenAsync(await CompradosJuntosAsync(new[] { producto.Id }, cantidad)))
            .Select(p => new Recomendacion(p, "Comprado junto con este producto")).ToList();
        excluir.UnionWith(resultado.Select(r => r.Producto.Id));

        if (resultado.Count < cantidad)
        {
            var mismaCategoria = await Disponibles(excluir)
                .Where(p => p.CategoriaId == producto.CategoriaId)
                .OrderByDescending(p => p.Destacado).ThenByDescending(p => p.PrecioAnterior > p.Precio)
                .Take(cantidad - resultado.Count).ToListAsync();
            resultado.AddRange(mismaCategoria.Select(p => new Recomendacion(p, "De la misma categoría")));
        }
        return resultado;
    }

    /// <summary>"Completa tu compra" en el carrito: complementos de lo que lleva.</summary>
    public async Task<List<Recomendacion>> ParaCarritoAsync(IReadOnlyCollection<int> enCarrito, int cantidad = 4)
    {
        if (enCarrito.Count == 0) return new List<Recomendacion>();
        var excluir = new HashSet<int>(enCarrito);
        var resultado = (await CargarEnOrdenAsync(await CompradosJuntosAsync(enCarrito, cantidad)))
            .Select(p => new Recomendacion(p, "Suelen comprarse juntos")).ToList();
        excluir.UnionWith(resultado.Select(r => r.Producto.Id));

        if (resultado.Count < cantidad)
        {
            // Complementos baratos de las mismas categorías (ej: rodillo + brocha con la pintura)
            var ids = enCarrito.ToList();
            var categorias = await _db.Productos.Where(p => ids.Contains(p.Id)).Select(p => p.CategoriaId).Distinct().ToListAsync();
            var complementos = await Disponibles(excluir)
                .Where(p => categorias.Contains(p.CategoriaId))
                .OrderBy(p => p.Precio)
                .Take(cantidad - resultado.Count).ToListAsync();
            resultado.AddRange(complementos.Select(p => new Recomendacion(p, "Complemento ideal")));
        }
        return resultado;
    }
}
