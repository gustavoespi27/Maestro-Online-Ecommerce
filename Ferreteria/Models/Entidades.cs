using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Ferreteria.Models;

/// <summary>Usuario del sistema (cliente o administrador). Extiende IdentityUser.</summary>
public class Usuario : IdentityUser
{
    [Required, StringLength(100)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = "";

    [StringLength(12)]
    [Display(Name = "RUT")]
    public string? Rut { get; set; }

    [StringLength(200)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(60)]
    public string? Comuna { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    /// <summary>Fecha en que aceptó los Términos y la Política de Privacidad (Ley 19.628 / Ley 21.719).</summary>
    public DateTime? FechaAceptacionTerminos { get; set; }

    /// <summary>Consentimiento separado y opcional para recibir ofertas por correo.</summary>
    public bool AceptaComunicaciones { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}

public class Categoria
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(60)]
    public string Nombre { get; set; } = "";

    [StringLength(250)]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    /// <summary>Nombre de archivo del ícono en wwwroot/img/categorias (opcional).</summary>
    [StringLength(60)]
    public string? Icono { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

public class Producto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El SKU es obligatorio")]
    [StringLength(30)]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = "";

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(120)]
    public string Nombre { get; set; } = "";

    [StringLength(60)]
    public string? Marca { get; set; }

    [StringLength(1000)]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    /// <summary>Precio en pesos chilenos, IVA incluido (sin decimales).</summary>
    [Range(1, 100_000_000, ErrorMessage = "El precio debe ser mayor a 0")]
    [Display(Name = "Precio (CLP)")]
    public int Precio { get; set; }

    /// <summary>Precio "normal" antes de la oferta. Si es mayor que <see cref="Precio"/>, el producto está en oferta.</summary>
    [Range(1, 100_000_000, ErrorMessage = "El precio anterior debe ser mayor a 0")]
    [Display(Name = "Precio anterior (oferta)")]
    public int? PrecioAnterior { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "El stock no puede ser negativo")]
    public int Stock { get; set; }

    [Display(Name = "Stock mínimo")]
    [Range(0, 1_000_000)]
    public int StockMinimo { get; set; } = 5;

    [StringLength(300)]
    [Display(Name = "Imagen")]
    public string? ImagenUrl { get; set; }

    [Display(Name = "Destacado")]
    public bool Destacado { get; set; }

    [Display(Name = "Activo (visible en la tienda)")]
    public bool Activo { get; set; } = true;

    [Display(Name = "Categoría")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría")]
    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    [NotMapped]
    public bool StockBajo => Stock <= StockMinimo;

    [NotMapped]
    public bool EnOferta => PrecioAnterior > Precio;

    /// <summary>Porcentaje de descuento redondeado (ej: 25 para un 25% menos). 0 si no está en oferta.</summary>
    [NotMapped]
    public int PorcentajeDescuento => EnOferta ? (int)Math.Round(100.0 * (PrecioAnterior!.Value - Precio) / PrecioAnterior.Value) : 0;
}

public enum EstadoPedido
{
    [Display(Name = "Pendiente de pago")] PendientePago = 0,
    [Display(Name = "Pagado")] Pagado = 1,
    [Display(Name = "En preparación")] EnPreparacion = 2,
    [Display(Name = "Despachado")] Despachado = 3,
    [Display(Name = "Entregado")] Entregado = 4,
    [Display(Name = "Pago rechazado")] Rechazado = 5,
    [Display(Name = "Anulado")] Anulado = 6
}

public enum TipoEntrega
{
    [Display(Name = "Despacho a domicilio")] Despacho = 0,
    [Display(Name = "Retiro en tienda")] RetiroEnTienda = 1
}

public class Pedido
{
    public int Id { get; set; }

    /// <summary>Código único enviado a Webpay como buy_order (máx. 26 caracteres).</summary>
    [Required, StringLength(26)]
    public string OrdenCompra { get; set; } = "";

    /// <summary>Cliente con cuenta. Es null en las compras como invitado.</summary>
    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Correo de contacto de una compra como invitado (sin cuenta).</summary>
    [EmailAddress, StringLength(120)]
    public string? EmailInvitado { get; set; }

    /// <summary>
    /// Secreto aleatorio del pedido (256 bits). Permite al invitado ver su comprobante y se envía a Webpay
    /// como session_id, para validar que las notificaciones de anulación correspondan realmente a este pedido.
    /// </summary>
    [StringLength(64)]
    public string? TokenAcceso { get; set; }

    [NotMapped]
    public bool EsInvitado => UsuarioId is null;

    /// <summary>Correo al que se asocia el pedido (cuenta o invitado).</summary>
    [NotMapped]
    public string? EmailContacto => Usuario?.Email ?? EmailInvitado;

    public DateTime Fecha { get; set; } = DateTime.Now;

    public EstadoPedido Estado { get; set; } = EstadoPedido.PendientePago;

    public TipoEntrega TipoEntrega { get; set; }

    [Required, StringLength(100)]
    public string NombreReceptor { get; set; } = "";

    [StringLength(20)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    [StringLength(60)]
    public string? Comuna { get; set; }

    [StringLength(60)]
    [Display(Name = "Región")]
    public string? Region { get; set; }

    [StringLength(300)]
    public string? Notas { get; set; }

    // --- Logística ---
    /// <summary>Fecha estimada de entrega calculada al comprar (según región y tipo de entrega).</summary>
    public DateTime? FechaEstimadaEntrega { get; set; }

    [StringLength(40)]
    public string? Transportista { get; set; }

    [StringLength(40)]
    [Display(Name = "Código de seguimiento")]
    public string? CodigoSeguimiento { get; set; }

    public DateTime? FechaDespacho { get; set; }
    public DateTime? FechaEntrega { get; set; }

    public int Subtotal { get; set; }
    public int CostoDespacho { get; set; }
    public int Total { get; set; }

    // --- Datos del pago (Webpay) ---
    [StringLength(100)]
    public string? TokenPago { get; set; }

    [StringLength(20)]
    public string? MetodoPago { get; set; }

    [StringLength(20)]
    public string? CodigoAutorizacion { get; set; }

    [StringLength(4)]
    public string? UltimosDigitosTarjeta { get; set; }

    [StringLength(5)]
    public string? TipoPagoWebpay { get; set; }

    public int? Cuotas { get; set; }

    public DateTime? FechaPago { get; set; }

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
}

public class DetallePedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Se guarda el nombre y precio al momento de la compra (por si el producto cambia después).</summary>
    [Required, StringLength(120)]
    public string NombreProducto { get; set; } = "";

    public int PrecioUnitario { get; set; }
    public int Cantidad { get; set; }

    [NotMapped]
    public int Subtotal => PrecioUnitario * Cantidad;
}

// ---------- Atención al cliente ----------

public enum AsuntoContacto
{
    [Display(Name = "Consulta sobre un producto")] Producto = 0,
    [Display(Name = "Estado de mi pedido o despacho")] Pedido = 1,
    [Display(Name = "Cambios, devoluciones o garantía")] Devolucion = 2,
    [Display(Name = "Cotización para empresas u obras")] Cotizacion = 3,
    [Display(Name = "Reclamo")] Reclamo = 4,
    [Display(Name = "Otro")] Otro = 5
}

public class MensajeContacto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ingresa tu nombre"), StringLength(100)]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = "Ingresa tu correo"), EmailAddress(ErrorMessage = "Correo inválido"), StringLength(120)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = "";

    [StringLength(20)]
    [Display(Name = "Teléfono (opcional)")]
    public string? Telefono { get; set; }

    public AsuntoContacto Asunto { get; set; }

    [StringLength(26)]
    [Display(Name = "Pedido (opcional)")]
    public string? OrdenCompra { get; set; }

    [Required(ErrorMessage = "Escribe tu mensaje"), StringLength(2000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre 10 y 2000 caracteres")]
    public string Mensaje { get; set; } = "";

    public DateTime Fecha { get; set; } = DateTime.Now;
    public bool Atendido { get; set; }
}

/// <summary>Reseña de un producto. Solo la puede escribir alguien que lo compró (compra verificada).</summary>
public class Resena
{
    public int Id { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Autor con cuenta (compra verificada). Es null en las reseñas de ejemplo.</summary>
    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Nombre visible en las reseñas de ejemplo (datos de demostración del proyecto).</summary>
    [StringLength(60)]
    public string? NombreAutor { get; set; }

    /// <summary>true = reseña de demostración; se muestra rotulada como "Reseña de ejemplo".</summary>
    public bool EsEjemplo { get; set; }

    [Range(1, 5, ErrorMessage = "Elige de 1 a 5 estrellas")]
    public int Calificacion { get; set; }

    [StringLength(600)]
    public string? Comentario { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;
}

// ---------- Instrumento de aceptación tecnológica (UTAUT) ----------

/// <summary>
/// Respuesta anónima a la encuesta UTAUT. Cada ítem va de 1 (muy en desacuerdo) a 5 (muy de acuerdo).
/// Constructos: Expectativa de Desempeño (ED), Expectativa de Esfuerzo (EE), Influencia Social (IS),
/// Condiciones Facilitadoras (CF) e Intención de Uso (IU).
/// </summary>
public class RespuestaEncuesta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;

    [StringLength(20)] public string? RangoEdad { get; set; }
    [StringLength(20)] public string? Genero { get; set; }
    [StringLength(30)] public string? FrecuenciaCompraOnline { get; set; }
    [StringLength(30)] public string? Perfil { get; set; }

    [Range(1, 5)] public int ED1 { get; set; }
    [Range(1, 5)] public int ED2 { get; set; }
    [Range(1, 5)] public int ED3 { get; set; }
    [Range(1, 5)] public int EE1 { get; set; }
    [Range(1, 5)] public int EE2 { get; set; }
    [Range(1, 5)] public int EE3 { get; set; }
    [Range(1, 5)] public int IS1 { get; set; }
    [Range(1, 5)] public int IS2 { get; set; }
    [Range(1, 5)] public int IS3 { get; set; }
    [Range(1, 5)] public int CF1 { get; set; }
    [Range(1, 5)] public int CF2 { get; set; }
    [Range(1, 5)] public int CF3 { get; set; }
    [Range(1, 5)] public int IU1 { get; set; }
    [Range(1, 5)] public int IU2 { get; set; }
    [Range(1, 5)] public int IU3 { get; set; }

    [StringLength(500)] public string? Comentario { get; set; }
}
