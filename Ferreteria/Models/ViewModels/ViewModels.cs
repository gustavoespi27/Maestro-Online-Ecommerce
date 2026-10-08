using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ferreteria.Models.ViewModels;

// ---------- Tienda ----------

public class CatalogoVM
{
    public List<Producto> Productos { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();
    public int? CategoriaId { get; set; }
    public string? Busqueda { get; set; }
    public bool SoloOfertas { get; set; }
    public string Orden { get; set; } = "relevancia";
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; }
    public int TotalResultados { get; set; }
    public string? NombreCategoria => Categorias.FirstOrDefault(c => c.Id == CategoriaId)?.Nombre;
}

public class InicioVM
{
    public List<Producto> Destacados { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();
    public List<Producto> Nuevos { get; set; } = new();
    public List<Producto> Ofertas { get; set; } = new();
    public Dictionary<int, int> ProductosPorCategoria { get; set; } = new();
    public int TotalProductos { get; set; }
    public int TotalCategorias { get; set; }
    public int TotalMarcas { get; set; }
    public List<Ferreteria.Services.Recomendacion> Recomendados { get; set; } = new();
    /// <summary>true si las recomendaciones se basan en el historial del cliente (y no solo en popularidad).</summary>
    public bool HayHistorial { get; set; }
}

public class LineaCarritoVM
{
    public Producto Producto { get; set; } = null!;
    public int Cantidad { get; set; }
    public int Subtotal => Producto.Precio * Cantidad;
    public bool StockSuficiente => Producto.Activo && Producto.Stock >= Cantidad;
}

public class CarritoVM
{
    public List<LineaCarritoVM> Lineas { get; set; } = new();
    public int Subtotal => Lineas.Sum(l => l.Subtotal);
    public int CostoDespacho { get; set; }
    public int DespachoGratisDesde { get; set; }
    public int Total => Subtotal + CostoDespacho;
    public bool TieneProblemasDeStock => Lineas.Any(l => !l.StockSuficiente);
    public List<Ferreteria.Services.Recomendacion> Recomendaciones { get; set; } = new();
    public bool EstaVacio => Lineas.Count == 0;
}

public class CheckoutVM
{
    public CarritoVM Carrito { get; set; } = new();

    [Display(Name = "Tipo de entrega")]
    public TipoEntrega TipoEntrega { get; set; } = TipoEntrega.Despacho;

    /// <summary>Solo para compras como invitado: correo para enviar el comprobante y hacer seguimiento.</summary>
    [EmailAddress(ErrorMessage = "Correo inválido")]
    [StringLength(120)]
    [Display(Name = "Correo electrónico")]
    public string? EmailInvitado { get; set; }

    public bool EsInvitado { get; set; }

    [Required(ErrorMessage = "Ingresa el nombre de quien recibe")]
    [StringLength(100)]
    [Display(Name = "Nombre de quien recibe")]
    public string NombreReceptor { get; set; } = "";

    [Required(ErrorMessage = "Ingresa un teléfono de contacto")]
    [StringLength(20)]
    [RegularExpression(@"^\+?[0-9 ]{8,15}$", ErrorMessage = "Teléfono inválido (ej: +56 9 1234 5678)")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = "";

    [StringLength(200)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(60)]
    public string? Comuna { get; set; }

    [StringLength(60)]
    [Display(Name = "Región")]
    public string? Region { get; set; } = Ferreteria.Services.Envios.RegionPorDefecto;

    [StringLength(300)]
    [Display(Name = "Notas para el pedido (opcional)")]
    public string? Notas { get; set; }

    /// <summary>Ley 19.496: el consumidor debe conocer y aceptar las condiciones antes de pagar.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar los Términos y Condiciones y la Política de Privacidad")]
    [Display(Name = "Acepto los Términos y Condiciones y la Política de Privacidad")]
    public bool AceptaTerminos { get; set; }

    public int CostoDespachoConfigurado { get; set; }
}

// ---------- Cuenta ----------

public class LoginVM
{
    [Required(ErrorMessage = "Ingresa tu correo")]
    [EmailAddress(ErrorMessage = "Correo inválido")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ingresa tu contraseña")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    [Display(Name = "Recordarme")]
    public bool Recordarme { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegistroVM
{
    [Required(ErrorMessage = "Ingresa tu nombre")]
    [StringLength(100)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [Required(ErrorMessage = "Ingresa tu correo")]
    [EmailAddress(ErrorMessage = "Correo inválido")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = "";

    [StringLength(12)]
    [RegularExpression(@"^\d{7,8}-[\dkK]$", ErrorMessage = "Formato de RUT: 12345678-9")]
    [Display(Name = "RUT (opcional)")]
    public string? Rut { get; set; }

    [Phone]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "Ingresa una contraseña")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
    [Display(Name = "Repetir contraseña")]
    public string ConfirmarPassword { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar los Términos y Condiciones y la Política de Privacidad")]
    public bool AceptaTerminos { get; set; }

    [Display(Name = "Quiero recibir ofertas y novedades por correo (opcional)")]
    public bool AceptaComunicaciones { get; set; }

    public string? ReturnUrl { get; set; }
}

public class PerfilVM
{
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ingresa tu nombre")]
    [StringLength(100)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [StringLength(12)]
    [RegularExpression(@"^\d{7,8}-[\dkK]$", ErrorMessage = "Formato de RUT: 12345678-9")]
    [Display(Name = "RUT")]
    public string? Rut { get; set; }

    [Phone]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [StringLength(200)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(60)]
    public string? Comuna { get; set; }

    [Display(Name = "Quiero recibir ofertas y novedades por correo")]
    public bool AceptaComunicaciones { get; set; }
}

// ---------- Admin ----------

public class DashboardVM
{
    public int VentasMes { get; set; }
    public int VentasTotales { get; set; }
    public int PedidosPagadosMes { get; set; }
    public int PedidosPorPreparar { get; set; }
    public int TotalProductos { get; set; }
    public int TotalClientes { get; set; }
    public List<Producto> StockBajo { get; set; } = new();
    public List<Pedido> UltimosPedidos { get; set; } = new();
    public List<(string Nombre, int Unidades, int Monto)> MasVendidos { get; set; } = new();
    public List<(DateTime Dia, int Monto)> VentasUltimos7Dias { get; set; } = new();
}

public class ProductoFormVM
{
    public Producto Producto { get; set; } = new();
    public IFormFile? Imagen { get; set; }
    public IEnumerable<SelectListItem> Categorias { get; set; } = Enumerable.Empty<SelectListItem>();
}
