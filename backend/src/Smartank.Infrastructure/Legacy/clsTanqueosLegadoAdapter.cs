using System.Data;
using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Infrastructure.Legacy;

/// <summary>Anticorrupcion de los historiales tblHistorialTanqueosDistracom y tblHistorialTanqueosTerpel hacia un unico DTO.</summary>
public sealed class clsTanqueosLegadoAdapter(clsLegadoConexion conexion) : ITanqueosLegadoPort
{
    private const string SQL_DISTRACOM = """
        SELECT ID, PLACA, ESTACION, CIUDAD, FECHAFINAL, VOLUMEN, KILOMETRAJE, VALOR, VALORTOTAL,
               COMBUSTIBLE, CEDULA, VALE, CENTROCOSTO, IDAUTORIZACION
        FROM tblHistorialTanqueosDistracom
        WHERE FECHAFINAL >= @desde
        ORDER BY FECHAFINAL
        """;

    private const string SQL_TERPEL = """
        SELECT ID, NROFACTURA, IDELECTRONICO, PLACA, GALONESCONSUMO, VALORGALON, VALORVENTA,
               FECHATRANSACCION, CIUDAD, NOMBREPRODUCTO, FAMILIAPRODUCTO
        FROM tblHistorialTanqueosTerpel
        WHERE FECHATRANSACCION >= @desde
        ORDER BY FECHATRANSACCION
        """;

    public async Task<IReadOnlyList<clsTanqueoLegadoDto>> ListarDistracomAsync(DateTime desdeHoraColombia, CancellationToken ct = default)
    {
        var filas = await LeerAsync(SQL_DISTRACOM, desdeHoraColombia, ct);
        return filas.Select(f => new clsTanqueoLegadoDto(
            FuenteTanqueo.Distracom,
            f.Entero("ID"),
            f.Texto("PLACA") ?? string.Empty,
            clsHoraColombia.AUtc(f.Fecha("FECHAFINAL") ?? default),
            f.Decimal("VOLUMEN"),
            f.Decimal("VALOR"),
            f.Decimal("VALORTOTAL"),
            f.TextoSinNA("COMBUSTIBLE"),
            null,
            f.TextoSinNA("CIUDAD"),
            f.TextoSinNA("ESTACION"),
            f.Decimal("KILOMETRAJE"),
            f.TextoSinNA("CEDULA"),
            f.TextoSinNA("VALE"),
            f.TextoSinNA("IDAUTORIZACION"),
            f.TextoSinNA("CENTROCOSTO"))).ToList();
    }

    public async Task<IReadOnlyList<clsTanqueoLegadoDto>> ListarTerpelAsync(DateTime desdeHoraColombia, CancellationToken ct = default)
    {
        var filas = await LeerAsync(SQL_TERPEL, desdeHoraColombia, ct);
        return filas.Select(f => new clsTanqueoLegadoDto(
            FuenteTanqueo.Terpel,
            f.Entero("ID"),
            f.Texto("PLACA") ?? string.Empty,
            clsHoraColombia.AUtc(f.Fecha("FECHATRANSACCION") ?? default),
            f.Decimal("GALONESCONSUMO"),
            f.Decimal("VALORGALON"),
            f.Decimal("VALORVENTA"),
            f.TextoSinNA("NOMBREPRODUCTO"),
            f.TextoSinNA("FAMILIAPRODUCTO"),
            f.TextoSinNA("CIUDAD"),
            null,
            null,
            null,
            f.NumeroComoTexto("NROFACTURA"),
            f.TextoSinNA("IDELECTRONICO"),
            null)).ToList();
    }

    private async Task<IReadOnlyList<clsFilaLegada>> LeerAsync(string sql, DateTime desde, CancellationToken ct)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);
        var parametros = new DynamicParameters();
        parametros.Add("desde", desde, DbType.DateTime);
        return await clsLegadoConexion.ConsultarAsync(cn, sql, parametros, ct);
    }
}
