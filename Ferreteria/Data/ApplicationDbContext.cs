using Ferreteria.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Data;

public class ApplicationDbContext : IdentityDbContext<Usuario>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<MensajeContacto> MensajesContacto => Set<MensajeContacto>();
    public DbSet<Resena> Resenas => Set<Resena>();
    public DbSet<RespuestaEncuesta> RespuestasEncuesta => Set<RespuestaEncuesta>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // necesario para las tablas de Identity

        builder.Entity<Categoria>(e =>
        {
            e.HasIndex(c => c.Nombre).IsUnique();
        });

        builder.Entity<Producto>(e =>
        {
            e.HasIndex(p => p.Sku).IsUnique();
            e.HasOne(p => p.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(p => p.CategoriaId)
             .OnDelete(DeleteBehavior.Restrict); // no se puede borrar una categoría con productos
        });

        builder.Entity<Pedido>(e =>
        {
            e.HasIndex(p => p.OrdenCompra).IsUnique();
            e.HasOne(p => p.Usuario)
             .WithMany(u => u.Pedidos)
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DetallePedido>(e =>
        {
            e.HasOne(d => d.Pedido)
             .WithMany(p => p.Detalles)
             .HasForeignKey(d => d.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto)
             .WithMany()
             .HasForeignKey(d => d.ProductoId)
             .OnDelete(DeleteBehavior.Restrict); // no se puede borrar un producto que ya se vendió
        });

        builder.Entity<Resena>(e =>
        {
            e.HasIndex(r => new { r.ProductoId, r.UsuarioId }).IsUnique(); // una reseña por persona y producto
            e.HasOne(r => r.Producto).WithMany().HasForeignKey(r => r.ProductoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Usuario).WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
