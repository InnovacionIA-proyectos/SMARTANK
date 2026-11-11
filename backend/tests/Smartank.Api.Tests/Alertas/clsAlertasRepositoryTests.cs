using Microsoft.EntityFrameworkCore;
using Moq;
using Smartank.Api.Tests.Infra;
using Smartank.Application.CasosDeUso.Alertas;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Alertas.Repositorios;
using Smartank.Infrastructure.Common;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Api.Tests.Alertas;

/// <summary>Alertas de punta a punta (handlers + repositorio EF + auditoria reales) contra SQL Server; se omite sin TEST_DB_CONNECTION.</summary>
public class clsAlertasRepositoryTests
{
    private static Mock<ICurrentUserPort> UsuarioActual(Guid id)
    {
        var m = new Mock<ICurrentUserPort>();
        m.SetupGet(a => a.UsuarioId).Returns(id);
        m.SetupGet(a => a.EstaAutenticado).Returns(true);
        return m;
    }

    private static async Task<clsUsuario> AgregarUsuarioAsync(clsEscenarioBd e)
    {
        var u = new clsUsuario { Email = $"{e.Marca.ToLowerInvariant()}-{Guid.NewGuid():N}@prueba.co", PasswordHash = "$2a$04$x", Rol = Rol.ControlFlota };
        e.Db.Usuarios.Add(u);
        await e.Db.SaveChangesAsync();
        return u;
    }

    [clsHechoConBaseDePruebas]
    public async Task Crear_luego_listar_con_placa_y_filtro_por_estado()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(1));
        var repo = new clsEfAlertasRepository(e.Db);
        var crear = new clsCrearAlertaCommandHandler(repo, new clsEfUnitOfWork(e.Db), new clsEfAuditoriaRepository(e.Db));

        var creada = await crear.Handle(new clsCrearAlertaCommand("estacion_cara", ve.Id, null, 0.25));

        Assert.Equal(EstadoAlerta.Nueva, creada.Estado);
        Assert.NotEqual(Guid.Empty, creada.Id);
        Assert.NotEqual(default, creada.CreatedAt);   // lo genera la base y EF lo lee de vuelta

        var nuevas = await repo.ListarAsync(EstadoAlerta.Nueva, default);
        var fila = Assert.Single(nuevas, a => a.Id == creada.Id);
        Assert.Equal(e.Placa(1), fila.Placa);
        Assert.Equal(TipoAlerta.EstacionCara, fila.Tipo);
        Assert.Equal(0.25, fila.MagnitudDesviacion!.Value, 4);
        Assert.DoesNotContain(await repo.ListarAsync(EstadoAlerta.Gestionada, default), a => a.Id == creada.Id);
    }

    [clsHechoConBaseDePruebas]
    public async Task Gestionar_cambia_el_estado_registra_al_usuario_y_deja_auditoria()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(2));
        var actor = await AgregarUsuarioAsync(e);
        var repo = new clsEfAlertasRepository(e.Db);
        var uow = new clsEfUnitOfWork(e.Db);
        var auditoria = new clsEfAuditoriaRepository(e.Db);
        var creada = await new clsCrearAlertaCommandHandler(repo, uow, auditoria)
            .Handle(new clsCrearAlertaCommand("desviacion_consumo", ve.Id, null, null));
        var gestionar = new clsGestionarAlertaCommandHandler(repo, uow, auditoria, UsuarioActual(actor.Id).Object);

        var dto = await gestionar.Handle(new clsGestionarAlertaCommand(creada.Id, "en_revision"));

        Assert.Equal(EstadoAlerta.EnRevision, dto.Estado);
        Assert.Equal(actor.Id, dto.GestionadaPor);
        var guardada = await repo.ListarAsync(EstadoAlerta.EnRevision, default);
        Assert.Contains(guardada, a => a.Id == creada.Id && a.GestionadaPor == actor.Id);
        var auditorias = e.Db.AuditLog.Where(a => a.EntidadId == creada.Id && a.Accion == "gestionar_alerta").ToList();
        var registro = Assert.Single(auditorias);
        Assert.Equal(actor.Id, registro.ActorId);
        Assert.Contains("\"estado\":[\"nueva\",\"en_revision\"]", registro.Diff);
    }

    [clsHechoConBaseDePruebas]
    public async Task Una_alerta_cerrada_no_se_puede_reabrir()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(3));
        var actor = await AgregarUsuarioAsync(e);
        var repo = new clsEfAlertasRepository(e.Db);
        var uow = new clsEfUnitOfWork(e.Db);
        var auditoria = new clsEfAuditoriaRepository(e.Db);
        var creada = await new clsCrearAlertaCommandHandler(repo, uow, auditoria)
            .Handle(new clsCrearAlertaCommand("desviacion_consumo", ve.Id, null, null));
        var gestionar = new clsGestionarAlertaCommandHandler(repo, uow, auditoria, UsuarioActual(actor.Id).Object);
        await gestionar.Handle(new clsGestionarAlertaCommand(creada.Id, "gestionada"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            gestionar.Handle(new clsGestionarAlertaCommand(creada.Id, "en_revision")));

        Assert.Equal("TRANSICION_ALERTA_INVALIDA", ex.Code);
    }

    [clsHechoConBaseDePruebas]
    public async Task Cambio_concurrente_de_otro_usuario_da_CONFLICTO_CONCURRENCIA_y_no_se_pisa()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(4));
        var actor = await AgregarUsuarioAsync(e);
        var repo = new clsEfAlertasRepository(e.Db);
        var uow = new clsEfUnitOfWork(e.Db);
        var auditoria = new clsEfAuditoriaRepository(e.Db);
        var creada = await new clsCrearAlertaCommandHandler(repo, uow, auditoria)
            .Handle(new clsCrearAlertaCommand("desviacion_consumo", ve.Id, null, null));

        // Este "usuario" ya leyo la alerta (con su rowversion)...
        var leida = await repo.ObtenerParaActualizarAsync(creada.Id, default);
        Assert.NotNull(leida);
        // ...y otro la modifica a la vez: el UPDATE directo cambia el rowversion en la base.
        await e.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[tblAlertas] SET [Estado] = N'descartada' WHERE [Id] = {creada.Id}");

        // Al guardar con el rowversion viejo, el cambio no se aplica y se informa el conflicto.
        leida!.Estado = EstadoAlerta.Gestionada;
        var ex = await Assert.ThrowsAsync<clsConflictException>(() => uow.SaveChangesAsync());
        Assert.Equal("CONFLICTO_CONCURRENCIA", ex.Code);
        Assert.NotEqual(Guid.Empty, actor.Id);
    }

    [clsHechoConBaseDePruebas]
    public async Task ExisteVehiculo_y_ExisteEvento_consultan_la_base()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(5));
        var repo = new clsEfAlertasRepository(e.Db);

        Assert.True(await repo.ExisteVehiculoAsync(ve.Id, default));
        Assert.False(await repo.ExisteVehiculoAsync(Guid.NewGuid(), default));
        Assert.False(await repo.ExisteEventoAsync(Guid.NewGuid(), default));
    }
}
