using Microsoft.EntityFrameworkCore;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Auditoria.Entidades;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Precios.Entidades;
using Smartank.Domain.Rutas.Entidades;
using Smartank.Domain.Tanqueos.Entidades;
using Smartank.Domain.Uploads.Entidades;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Infrastructure.Persistence;

/// <summary>
/// DbContext de la base propia de SMARTANK (SQL Server 2019). No hay migraciones de EF: el esquema real lo define
/// Database/ddl.sql y este contexto solo lo mapea (las clases de Configuraciones/ deben mantenerse iguales al DDL).
/// </summary>
public class clsSmartankDbContext(DbContextOptions<clsSmartankDbContext> options) : DbContext(options)
{
    public DbSet<clsUsuario> Usuarios => Set<clsUsuario>();

    public DbSet<clsVehiculo> Vehiculos => Set<clsVehiculo>();

    public DbSet<clsConductor> Conductores => Set<clsConductor>();

    public DbSet<clsEstacion> Estaciones => Set<clsEstacion>();

    public DbSet<clsViaje> Viajes => Set<clsViaje>();

    public DbSet<clsRuta> Rutas => Set<clsRuta>();

    public DbSet<clsViajePrecioRef> ViajePreciosRef => Set<clsViajePrecioRef>();

    public DbSet<clsTanqueo> Tanqueos => Set<clsTanqueo>();

    public DbSet<clsAutorizacionTanqueo> AutorizacionesTanqueo => Set<clsAutorizacionTanqueo>();

    public DbSet<clsEventoTanqueo> EventosTanqueo => Set<clsEventoTanqueo>();

    public DbSet<clsAlerta> Alertas => Set<clsAlerta>();

    /// <summary>Append-only: solo se inserta (la base bloquea UPDATE y DELETE).</summary>
    public DbSet<clsAuditLog> AuditLog => Set<clsAuditLog>();

    public DbSet<clsUpload> Uploads => Set<clsUpload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(clsEsquemaSmartank.Nombre);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(clsSmartankDbContext).Assembly);
    }
}
