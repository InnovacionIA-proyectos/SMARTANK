-- =============================================================================
-- Esquema de base de datos de SMARTANK (SQL Server 2019)
-- =============================================================================
-- Esquema: dbo   Base sugerida: SmartankDb (ver bloque opcional más abajo).
--
-- Este archivo es la única definición del esquema. El proyecto no usa migraciones
-- de EF Core: se ejecuta completo, una sola vez, contra una base vacía (EF solo
-- mapea estas tablas). Un cambio de estructura se hace editando este archivo y
-- aplicando a mano el ALTER equivalente en cada base que ya exista.
--
-- Lo ejecuta una persona con permisos para crear tablas, no la aplicación.
-- La aplicación lee el esquema de la variable Database__Esquema, que debe valer dbo.
--
-- Orden del archivo:
--   1. Tablas, agrupadas por módulo, cada una con un comentario de su objetivo.
--      Las columnas van en orden lógico (llave, referencias, datos, estado y
--      auditoría). La aplicación las lee por nombre.
--   2. Índices. Van antes que las llaves foráneas por convención del equipo (así
--      un FK que apunte a un índice único lo encuentra ya creado).
--   3. Llaves foráneas. Van aparte para no depender del orden de creación de las
--      tablas.
--   4. Triggers. Cada CREATE TRIGGER va en su propio lote (GO antes y después).
--
-- Decisión sobre UpdatedAt: lo mantiene la BASE DE DATOS con un trigger AFTER UPDATE
-- por tabla (TRG_<tabla>_UpdatedAt) y NO un interceptor de EF. Motivo: las
-- sincronizaciones masivas y las transiciones atómicas de estado (UPDATE ... WHERE
-- Estado = <esperado>, ExecuteUpdate o SQL directo) no pasan por SaveChanges, y con
-- trigger cualquier escritor deja UpdatedAt correcto. Consecuencia en EF: las tablas
-- se mapean con UseSqlOutputClause(false) (EF no puede usar OUTPUT sobre tablas con
-- triggers). tblAuditLog y tblUploads son inmutables y no llevan trigger de UpdatedAt.
--
-- Tipos de columna:
--   * PK uniqueidentifier con DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial
--     del lado de la aplicación; el default sirve para inserts por SQL directo).
--   * Fechas datetimeoffset con DEFAULT sysdatetimeoffset().
--   * Enums del dominio como nvarchar + CHECK con el valor en snake_case (los mismos
--     textos que viajan en el JSON del API).
--   * JSON como nvarchar(max) con CHECK (ISJSON(col) = 1); bytes como varbinary(max).
--
-- Collation recomendada de la base: Modern_Spanish_CI_AS. Compatibilidad: 150 (SQL 2019).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- OPCIONAL: crear la base. Descomente, ajuste el nombre y ejecute este bloque
-- ANTES del resto (o cree la base desde DBeaver y selecciónela como activa).
-- -----------------------------------------------------------------------------
/*
CREATE DATABASE [SmartankDb] COLLATE Modern_Spanish_CI_AS;
GO
ALTER DATABASE [SmartankDb] SET COMPATIBILITY_LEVEL = 150;
GO
USE [SmartankDb];
GO
*/

-- Los índices filtrados, los índices sobre columnas calculadas y los triggers exigen
-- estas opciones. SSMS y DBeaver las traen activas; sqlcmd no (usar -I), por eso se
-- fijan aquí.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================================
-- 1. TABLAS
-- =============================================================================

-- --- Usuarios -----------------------------------------------------------------
-- Autenticación propia contra la base: no hay portal externo de identidad.

-- Cuenta de una persona que usa el sistema: email único, hash bcrypt de la
-- contraseña y uno de los 4 roles (despacho, control_flota, finanzas, admin).
CREATE TABLE [dbo].[tblUsuarios] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblUsuarios_Id] DEFAULT (NEWSEQUENTIALID()),
    [Email] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [Nombre] nvarchar(200) NULL,
    [Rol] nvarchar(20) NOT NULL CONSTRAINT [DF_tblUsuarios_Rol] DEFAULT (N'despacho'),
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblUsuarios_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblUsuarios_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblUsuarios] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblUsuarios_Rol] CHECK ([Rol] IN (N'despacho', N'control_flota', N'finanzas', N'admin'))
);

-- --- Flota y personal ---------------------------------------------------------
-- Maestros sincronizados desde los sistemas legados (cabezotes y conductores) más
-- la telemetría vigente de cada vehículo (posición y nivel de tanque, Geotab).

-- Vehículo (cabezote) de la flota: datos maestros (capacidad de tanque, reserva,
-- chip de tanqueo), parámetros de consumo (rendimientos, tolerancia) y última
-- telemetría (nivel de tanque y posición GPS con su fecha de lectura).
-- FpsapId y GeotabDeviceId son únicos cuando están presentes (los vehículos
-- creados a mano los dejan en NULL).
CREATE TABLE [dbo].[tblVehiculos] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblVehiculos_Id] DEFAULT (NEWSEQUENTIALID()),
    [Placa] nvarchar(20) NOT NULL,
    [AliasPlaca] nvarchar(60) NULL,
    [TipoMotor] nvarchar(60) NOT NULL,
    [Programa] nvarchar(60) NULL,
    [ChipTanqueo] nvarchar(60) NULL,
    [NitPropietario] nvarchar(30) NULL,
    [CapacidadTanqueGal] decimal(10,3) NULL,
    [GalonesReserva] decimal(10,3) NULL,
    [GalonesUtilizables] decimal(10,3) NULL,
    [GalonesTolerancia] decimal(10,3) NULL,
    [RendimientoHistorico] decimal(8,4) NULL,
    [RendimientoEsperado] decimal(8,4) NULL,
    [ToleranciaMotor] decimal(5,4) NOT NULL CONSTRAINT [DF_tblVehiculos_ToleranciaMotor] DEFAULT (0.02),
    [Odometro] decimal(12,3) NULL,
    [NivelTanqueActual] decimal(10,3) NULL,
    [NivelAt] datetimeoffset NULL,
    [UltimaLat] decimal(10,7) NULL,
    [UltimaLng] decimal(10,7) NULL,
    [UbicacionAt] datetimeoffset NULL,
    [GeotabDeviceId] nvarchar(100) NULL,
    [FpsapId] bigint NULL,
    [FpsapSincronizadoAt] datetimeoffset NULL,
    [Activo] bit NOT NULL CONSTRAINT [DF_tblVehiculos_Activo] DEFAULT (1),
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblVehiculos_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblVehiculos_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblVehiculos] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblVehiculos_CapacidadTanqueGal] CHECK ([CapacidadTanqueGal] >= 0),
    CONSTRAINT [CK_tblVehiculos_GalonesReserva] CHECK ([GalonesReserva] >= 0),
    CONSTRAINT [CK_tblVehiculos_GalonesUtilizables] CHECK ([GalonesUtilizables] >= 0),
    CONSTRAINT [CK_tblVehiculos_GalonesTolerancia] CHECK ([GalonesTolerancia] >= 0),
    CONSTRAINT [CK_tblVehiculos_RendimientoHistorico] CHECK ([RendimientoHistorico] >= 0),
    CONSTRAINT [CK_tblVehiculos_RendimientoEsperado] CHECK ([RendimientoEsperado] >= 0),
    CONSTRAINT [CK_tblVehiculos_ToleranciaMotor] CHECK ([ToleranciaMotor] >= 0 AND [ToleranciaMotor] <= 1),
    CONSTRAINT [CK_tblVehiculos_Odometro] CHECK ([Odometro] >= 0),
    CONSTRAINT [CK_tblVehiculos_NivelTanqueActual] CHECK ([NivelTanqueActual] >= 0),
    CONSTRAINT [CK_tblVehiculos_UltimaLat] CHECK ([UltimaLat] BETWEEN -90 AND 90),
    CONSTRAINT [CK_tblVehiculos_UltimaLng] CHECK ([UltimaLng] BETWEEN -180 AND 180)
);

-- Conductor (maestro sincronizado por cédula): nombre, celular al que se envía la
-- orden de tanqueo por WhatsApp y si utiliza tarjeta prepago.
CREATE TABLE [dbo].[tblConductores] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblConductores_Id] DEFAULT (NEWSEQUENTIALID()),
    [Cedula] nvarchar(30) NOT NULL,
    [Nombres] nvarchar(200) NOT NULL,
    [NumeroCelular] nvarchar(30) NULL,
    [Activo] bit NOT NULL CONSTRAINT [DF_tblConductores_Activo] DEFAULT (1),
    [UtilizarTarjetaPrepago] bit NOT NULL CONSTRAINT [DF_tblConductores_UtilizarTarjetaPrepago] DEFAULT (0),
    [FpsapId] bigint NULL,
    [FpsapSincronizadoAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblConductores_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblConductores_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblConductores] PRIMARY KEY ([Id])
);

-- --- Estaciones ---------------------------------------------------------------

-- Estación de servicio (EDS): catálogo de las redes de abastecimiento (Terpel y
-- Distracom) y estaciones propias, con coordenadas, producto que vende y precios
-- reales de ACPM y gasolina corriente (llegan por la sincronización de precios;
-- NULL = sin dato). PrecioGalonActual replica PrecioAcpm (combustible de la flota).
-- NombreNormalizado es una columna calculada (minúsculas, sin espacios laterales)
-- para cruzar el nombre del tanqueo histórico con el catálogo usando un índice.
CREATE TABLE [dbo].[tblEstaciones] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblEstaciones_Id] DEFAULT (NEWSEQUENTIALID()),
    [CodigoExterno] nvarchar(50) NULL,
    [Nombre] nvarchar(200) NOT NULL,
    [NombreNormalizado] AS (lower(ltrim(rtrim([Nombre])))) PERSISTED,
    [Ruta] nvarchar(200) NULL,
    [Lat] decimal(10,7) NULL,
    [Lng] decimal(10,7) NULL,
    [Proveedor] nvarchar(60) NULL,
    [Producto] nvarchar(120) NULL,
    [PrecioGalonActual] decimal(12,2) NULL,
    [PrecioAcpm] decimal(12,2) NULL,
    [PrecioCorriente] decimal(12,2) NULL,
    [PrecioActualizadoAt] datetimeoffset NULL,
    [SoportaPreautorizacion] bit NOT NULL CONSTRAINT [DF_tblEstaciones_SoportaPreautorizacion] DEFAULT (0),
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblEstaciones_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblEstaciones_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblEstaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblEstaciones_Lat] CHECK ([Lat] BETWEEN -90 AND 90),
    CONSTRAINT [CK_tblEstaciones_Lng] CHECK ([Lng] BETWEEN -180 AND 180),
    CONSTRAINT [CK_tblEstaciones_PrecioGalonActual] CHECK ([PrecioGalonActual] >= 0),
    CONSTRAINT [CK_tblEstaciones_PrecioAcpm] CHECK ([PrecioAcpm] >= 0),
    CONSTRAINT [CK_tblEstaciones_PrecioCorriente] CHECK ([PrecioCorriente] >= 0)
);

-- --- Viajes y rutas -----------------------------------------------------------

-- Viaje (FO: flete/orden de despacho). Los viajes reales se sincronizan desde el
-- sistema de fletes (FoId único); los creados a mano lo dejan en NULL. Estado:
-- programado, en_ruta, cerrado o descartado (FO borrado). FechaEfectiva (calculada)
-- es el inicio real si existe y, si no, la fecha de despacho: es la fecha que usa
-- el cruce viaje-tanqueo.
CREATE TABLE [dbo].[tblViajes] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblViajes_Id] DEFAULT (NEWSEQUENTIALID()),
    [VehiculoId] uniqueidentifier NOT NULL,
    [DespachadoPor] uniqueidentifier NULL,
    [Ruta] nvarchar(200) NOT NULL,
    [Estado] nvarchar(20) NOT NULL CONSTRAINT [DF_tblViajes_Estado] DEFAULT (N'programado'),
    [Manifiesto] nvarchar(60) NULL,
    [AutonomiaEstimadaKm] decimal(10,2) NULL,
    [InicioAt] datetimeoffset NULL,
    [FechaDespacho] datetimeoffset NULL,
    [FechaEfectiva] AS (isnull([InicioAt], [FechaDespacho])) PERSISTED,
    [FoId] bigint NULL,
    [NumeroFo] nvarchar(30) NULL,
    [PlacaCabezote] nvarchar(20) NULL,
    [PlacaTrailer] nvarchar(20) NULL,
    [ProgramaCabezote] nvarchar(60) NULL,
    [CedulaConductor] nvarchar(30) NULL,
    [Transportista] nvarchar(200) NULL,
    [CodigoRuta] nvarchar(50) NULL,
    [NombreRuta] nvarchar(250) NULL,
    [CiudadOrigen] nvarchar(100) NULL,
    [CiudadDestino] nvarchar(100) NULL,
    [PesoFo] decimal(14,3) NULL,
    [GalonesEds] decimal(10,3) NULL,
    [GalonesCredito] decimal(10,3) NULL,
    [GalonesContado] decimal(10,3) NULL,
    [GalonesRuta] decimal(10,3) NULL,
    [KmtsRuta] decimal(12,3) NULL,
    [GastosGenerados] bit NULL,
    [GastosEnviadoSap] bit NULL,
    [ConsumoEnviadoSap] bit NULL,
    [EnviadoSap] bit NULL,
    [Borrado] bit NOT NULL CONSTRAINT [DF_tblViajes_Borrado] DEFAULT (0),
    [FoSincronizadoAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblViajes_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblViajes_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblViajes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblViajes_Estado] CHECK ([Estado] IN (N'programado', N'en_ruta', N'cerrado', N'descartado')),
    CONSTRAINT [CK_tblViajes_AutonomiaEstimadaKm] CHECK ([AutonomiaEstimadaKm] >= 0)
);

-- Ruta (polilínea) de un FO calculada por el servicio de ruteo: kilómetros,
-- puntos, polilínea simplificada [[lat,lng],...] y detalle de tramos en JSON.
-- Fo es el número de FO SIN ceros a la izquierda (único). Los extremos
-- (OrigenLat/Lng, DestinoLat/Lng) los deriva el trigger de esta tabla a partir de
-- la polilínea (primer y último punto) para calcular el avance sin leer la
-- polilínea completa. Fuente: valhalla (detallada) o dane (recta origen-destino).
CREATE TABLE [dbo].[tblRutas] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblRutas_Id] DEFAULT (NEWSEQUENTIALID()),
    [Fo] nvarchar(30) NOT NULL,
    [ViajeId] uniqueidentifier NULL,
    [Fuente] nvarchar(20) NOT NULL CONSTRAINT [DF_tblRutas_Fuente] DEFAULT (N'valhalla'),
    [ValhallaId] bigint NULL,
    [Kilometros] decimal(12,3) NULL,
    [NumPuntos] int NULL,
    [NumPuntosOriginal] int NULL,
    [Polilinea] nvarchar(max) NULL,
    [DetalleKm] nvarchar(max) NULL,
    [OrigenLat] decimal(10,7) NULL,
    [OrigenLng] decimal(10,7) NULL,
    [DestinoLat] decimal(10,7) NULL,
    [DestinoLng] decimal(10,7) NULL,
    [FechaCalculo] datetimeoffset NULL,
    [SincronizadoAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblRutas_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblRutas_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblRutas] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblRutas_Fuente] CHECK ([Fuente] IN (N'valhalla', N'dane')),
    CONSTRAINT [CK_tblRutas_Polilinea] CHECK (ISJSON([Polilinea]) = 1),
    CONSTRAINT [CK_tblRutas_DetalleKm] CHECK (ISJSON([DetalleKm]) = 1)
);

-- Precios de referencia por viaje para el panel de Recuperación de Margen: ACPM
-- mínimo y máximo entre las EDS cercanas a la ruta del viaje y cuántas EDS
-- cumplieron el umbral. Una fila por viaje (la PK es el propio ViajeId); se borra
-- junto con el viaje.
CREATE TABLE [dbo].[tblViajePreciosRef] (
    [ViajeId] uniqueidentifier NOT NULL,
    [AcpmMin] decimal(12,2) NULL,
    [AcpmMax] decimal(12,2) NULL,
    [EdsCerca] int NOT NULL CONSTRAINT [DF_tblViajePreciosRef_EdsCerca] DEFAULT (0),
    [CalculadoAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblViajePreciosRef_CalculadoAt] DEFAULT (sysdatetimeoffset()),
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblViajePreciosRef_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblViajePreciosRef_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblViajePreciosRef] PRIMARY KEY ([ViajeId])
);

-- --- Tanqueos -----------------------------------------------------------------

-- Historial real de tanqueos de los dos programas de abastecimiento (distracom y
-- terpel), sincronizado por (Fuente, FuenteId). Es distinto de tblEventosTanqueo,
-- que es la bitácora del flujo de autorización. VehiculoId es NULL cuando la placa
-- no existe en la flota (no se descarta el registro para no subestimar el consumo).
CREATE TABLE [dbo].[tblTanqueos] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblTanqueos_Id] DEFAULT (NEWSEQUENTIALID()),
    [Fuente] nvarchar(20) NOT NULL,
    [FuenteId] bigint NOT NULL,
    [VehiculoId] uniqueidentifier NULL,
    [Placa] nvarchar(20) NOT NULL,
    [Fecha] datetimeoffset NOT NULL,
    [Galones] decimal(10,3) NULL,
    [ValorGalon] decimal(12,2) NULL,
    [ValorTotal] decimal(16,2) NULL,
    [Producto] nvarchar(120) NULL,
    [FamiliaProducto] nvarchar(120) NULL,
    [Ciudad] nvarchar(120) NULL,
    [Estacion] nvarchar(200) NULL,
    [Kilometraje] decimal(12,3) NULL,
    [CedulaConductor] nvarchar(30) NULL,
    [Factura] nvarchar(60) NULL,
    [Referencia] nvarchar(100) NULL,
    [CentroCosto] nvarchar(100) NULL,
    [SincronizadoAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblTanqueos_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblTanqueos_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblTanqueos] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblTanqueos_Fuente] CHECK ([Fuente] IN (N'distracom', N'terpel')),
    CONSTRAINT [CK_tblTanqueos_Galones] CHECK ([Galones] >= 0)
);

-- --- Autorizaciones y bitácora -------------------------------------------------

-- Autorización de tanqueo: orden para cargar hasta GalonesMax en una EDS, con
-- precio techo y ventana de vigencia, dentro de un viaje. Estados: propuesta,
-- autorizada, ejecutada, ejecutada_parcial, expirada, rechazada (las no ejecutadas
-- se conservan, nunca se borran). RowVersion da concurrencia optimista a las
-- transiciones de estado. EdsConfirmacion es la respuesta JSON de la EDS.
CREATE TABLE [dbo].[tblAutorizacionesTanqueo] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblAutorizacionesTanqueo_Id] DEFAULT (NEWSEQUENTIALID()),
    [ViajeId] uniqueidentifier NOT NULL,
    [VehiculoId] uniqueidentifier NOT NULL,
    [EstacionId] uniqueidentifier NOT NULL,
    [GalonesMax] decimal(10,3) NOT NULL,
    [PrecioTecho] decimal(12,2) NOT NULL,
    [DeltaAhorroEstimado] decimal(16,2) NULL,
    [FechaInicio] datetimeoffset NOT NULL,
    [FechaFin] datetimeoffset NOT NULL,
    [Estado] nvarchar(20) NOT NULL CONSTRAINT [DF_tblAutorizacionesTanqueo_Estado] DEFAULT (N'propuesta'),
    [AutorizadoPor] uniqueidentifier NULL,
    [EdsConfirmacion] nvarchar(max) NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblAutorizacionesTanqueo_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblAutorizacionesTanqueo_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblAutorizacionesTanqueo] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblAutorizacionesTanqueo_Estado] CHECK ([Estado] IN (N'propuesta', N'autorizada', N'ejecutada', N'ejecutada_parcial', N'expirada', N'rechazada')),
    CONSTRAINT [CK_tblAutorizacionesTanqueo_GalonesMax] CHECK ([GalonesMax] > 0),
    CONSTRAINT [CK_tblAutorizacionesTanqueo_PrecioTecho] CHECK ([PrecioTecho] >= 0),
    CONSTRAINT [CK_tblAutorizacionesTanqueo_Ventana] CHECK ([FechaFin] >= [FechaInicio]),
    CONSTRAINT [CK_tblAutorizacionesTanqueo_EdsConfirmacion] CHECK (ISJSON([EdsConfirmacion]) = 1)
);

-- Evento de tanqueo (bitácora): lo que el conductor registra al tanquear contra
-- una autorización (galones, precio pagado, foto del surtidor, odómetro, niveles
-- de tanque). Programa y Producto son instantáneas al momento del registro para
-- que el historial sea fiel aunque luego cambie la configuración del vehículo.
-- Máximo un evento por autorización (índice único).
CREATE TABLE [dbo].[tblEventosTanqueo] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblEventosTanqueo_Id] DEFAULT (NEWSEQUENTIALID()),
    [AutorizacionId] uniqueidentifier NOT NULL,
    [VehiculoId] uniqueidentifier NOT NULL,
    [EstacionId] uniqueidentifier NOT NULL,
    [GalonesCargados] decimal(10,3) NOT NULL,
    [PrecioPagado] decimal(12,2) NULL,
    [FotoSurtidorUrl] nvarchar(500) NULL,
    [OdometroEvento] decimal(12,3) NULL,
    [NivelAntes] decimal(10,3) NULL,
    [NivelDespues] decimal(10,3) NULL,
    [RegistradoPor] uniqueidentifier NULL,
    [Programa] nvarchar(60) NULL,
    [Producto] nvarchar(120) NULL,
    [OcurridoAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblEventosTanqueo_OcurridoAt] DEFAULT (sysdatetimeoffset()),
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblEventosTanqueo_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblEventosTanqueo_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblEventosTanqueo] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblEventosTanqueo_GalonesCargados] CHECK ([GalonesCargados] >= 0),
    CONSTRAINT [CK_tblEventosTanqueo_PrecioPagado] CHECK ([PrecioPagado] >= 0),
    CONSTRAINT [CK_tblEventosTanqueo_OdometroEvento] CHECK ([OdometroEvento] >= 0)
);

-- --- Alertas ------------------------------------------------------------------

-- Alerta de control generada al conciliar un tanqueo o un consumo: tipos
-- desviacion_consumo_carga, desviacion_consumo, estacion_cara y ejecucion_parcial;
-- estados nueva, en_revision, gestionada y descartada. RowVersion evita que dos
-- usuarios gestionen la misma alerta a la vez.
CREATE TABLE [dbo].[tblAlertas] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblAlertas_Id] DEFAULT (NEWSEQUENTIALID()),
    [Tipo] nvarchar(30) NOT NULL,
    [VehiculoId] uniqueidentifier NOT NULL,
    [EventoId] uniqueidentifier NULL,
    [MagnitudDesviacion] decimal(14,4) NULL,
    [Estado] nvarchar(20) NOT NULL CONSTRAINT [DF_tblAlertas_Estado] DEFAULT (N'nueva'),
    [GestionadaPor] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblAlertas_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblAlertas_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblAlertas] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblAlertas_Tipo] CHECK ([Tipo] IN (N'desviacion_consumo_carga', N'desviacion_consumo', N'estacion_cara', N'ejecucion_parcial')),
    CONSTRAINT [CK_tblAlertas_Estado] CHECK ([Estado] IN (N'nueva', N'en_revision', N'gestionada', N'descartada'))
);

-- --- Auditoría y archivos ------------------------------------------------------

-- Registro de auditoría APPEND-ONLY: toda acción que cambia estado deja actor,
-- acción, entidad y diff (estado anterior/posterior en JSON). Un trigger INSTEAD OF
-- bloquea UPDATE y DELETE (sección 4). Recomendado además: DENY UPDATE, DELETE
-- sobre esta tabla al login de la aplicación. UpdatedAt = CreatedAt.
CREATE TABLE [dbo].[tblAuditLog] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblAuditLog_Id] DEFAULT (NEWSEQUENTIALID()),
    [ActorId] uniqueidentifier NULL,
    [Accion] nvarchar(80) NOT NULL,
    [Entidad] nvarchar(80) NOT NULL,
    [EntidadId] uniqueidentifier NULL,
    [Diff] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblAuditLog_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblAuditLog_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblAuditLog] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_tblAuditLog_Diff] CHECK (ISJSON([Diff]) = 1)
);

-- Imágenes subidas (foto del surtidor de la bitácora). Los bytes viven en la base y
-- se sirven por el API; el evento de tanqueo guarda solo la URL corta. Inmutable.
CREATE TABLE [dbo].[tblUploads] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_tblUploads_Id] DEFAULT (NEWSEQUENTIALID()),
    [ContentType] nvarchar(100) NOT NULL,
    [Data] varbinary(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblUploads_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_tblUploads_UpdatedAt] DEFAULT (sysdatetimeoffset()),
    CONSTRAINT [PK_tblUploads] PRIMARY KEY ([Id])
);
GO

-- =============================================================================
-- 2. ÍNDICES
-- =============================================================================

-- Usuarios
CREATE UNIQUE INDEX [UX_tblUsuarios_Email] ON [dbo].[tblUsuarios] ([Email]);

-- Referencias "quién hizo" hacia tblUsuarios: sin índice, borrar un usuario obligaría
-- a recorrer estas tablas completas para poner la columna en NULL (ON DELETE SET NULL).
CREATE INDEX [IX_tblViajes_DespachadoPor] ON [dbo].[tblViajes] ([DespachadoPor]);
CREATE INDEX [IX_tblAutorizacionesTanqueo_AutorizadoPor] ON [dbo].[tblAutorizacionesTanqueo] ([AutorizadoPor]);
CREATE INDEX [IX_tblEventosTanqueo_RegistradoPor] ON [dbo].[tblEventosTanqueo] ([RegistradoPor]);
CREATE INDEX [IX_tblAlertas_GestionadaPor] ON [dbo].[tblAlertas] ([GestionadaPor]);

-- Vehículos. Placa única (con la collation CI equivale al índice por lower(placa)
-- que usaba el cruce con tanqueos); FpsapId y GeotabDeviceId únicos si no son NULL.
CREATE UNIQUE INDEX [UX_tblVehiculos_Placa] ON [dbo].[tblVehiculos] ([Placa]);
CREATE UNIQUE INDEX [UX_tblVehiculos_FpsapId] ON [dbo].[tblVehiculos] ([FpsapId]) WHERE [FpsapId] IS NOT NULL;
CREATE UNIQUE INDEX [UX_tblVehiculos_GeotabDeviceId] ON [dbo].[tblVehiculos] ([GeotabDeviceId]) WHERE [GeotabDeviceId] IS NOT NULL;

-- Conductores. La cédula única sirve también para el enlace viajes.CedulaConductor.
CREATE UNIQUE INDEX [UX_tblConductores_Cedula] ON [dbo].[tblConductores] ([Cedula]);
CREATE UNIQUE INDEX [UX_tblConductores_FpsapId] ON [dbo].[tblConductores] ([FpsapId]) WHERE [FpsapId] IS NOT NULL;

-- Estaciones. Selección de la EDS más barata de la ruta, búsqueda por zona,
-- filtros por programa/producto, cruce por código externo y por nombre normalizado.
CREATE INDEX [IX_tblEstaciones_Ruta] ON [dbo].[tblEstaciones] ([Ruta]);
CREATE INDEX [IX_tblEstaciones_Ruta_PrecioGalonActual] ON [dbo].[tblEstaciones] ([Ruta], [PrecioGalonActual]);
CREATE INDEX [IX_tblEstaciones_Lat_Lng] ON [dbo].[tblEstaciones] ([Lat], [Lng]);
CREATE INDEX [IX_tblEstaciones_Proveedor] ON [dbo].[tblEstaciones] ([Proveedor]);
CREATE INDEX [IX_tblEstaciones_Producto] ON [dbo].[tblEstaciones] ([Producto]);
CREATE INDEX [IX_tblEstaciones_CodigoExterno] ON [dbo].[tblEstaciones] ([CodigoExterno]);
CREATE INDEX [IX_tblEstaciones_NombreNormalizado] ON [dbo].[tblEstaciones] ([NombreNormalizado]);

-- Viajes. Panel de despacho (por vehículo, estado, ruta, manifiesto, fecha, placa),
-- cruce con tanqueos por vehículo y fecha efectiva (descendente, para tomar el
-- viaje vigente más reciente) y upsert por FoId / NumeroFo.
CREATE INDEX [IX_tblViajes_VehiculoId] ON [dbo].[tblViajes] ([VehiculoId]);
CREATE INDEX [IX_tblViajes_Estado] ON [dbo].[tblViajes] ([Estado]);
CREATE INDEX [IX_tblViajes_Ruta] ON [dbo].[tblViajes] ([Ruta]);
CREATE INDEX [IX_tblViajes_Manifiesto] ON [dbo].[tblViajes] ([Manifiesto]);
CREATE INDEX [IX_tblViajes_FechaDespacho] ON [dbo].[tblViajes] ([FechaDespacho]);
CREATE INDEX [IX_tblViajes_PlacaCabezote] ON [dbo].[tblViajes] ([PlacaCabezote]);
CREATE INDEX [IX_tblViajes_NumeroFo] ON [dbo].[tblViajes] ([NumeroFo]);
CREATE INDEX [IX_tblViajes_VehiculoId_FechaEfectiva] ON [dbo].[tblViajes] ([VehiculoId], [FechaEfectiva] DESC);
CREATE UNIQUE INDEX [UX_tblViajes_FoId] ON [dbo].[tblViajes] ([FoId]) WHERE [FoId] IS NOT NULL;

-- Rutas. Un FO tiene una sola ruta; búsqueda por viaje.
CREATE UNIQUE INDEX [UX_tblRutas_Fo] ON [dbo].[tblRutas] ([Fo]);
CREATE INDEX [IX_tblRutas_ViajeId] ON [dbo].[tblRutas] ([ViajeId]);

-- Tanqueos (historial). Clave de sincronización y filtros frecuentes. El cruce por
-- placa usa IX_tblTanqueos_Placa (la collation CI hace innecesario lower()).
CREATE UNIQUE INDEX [UX_tblTanqueos_Fuente_FuenteId] ON [dbo].[tblTanqueos] ([Fuente], [FuenteId]);
CREATE INDEX [IX_tblTanqueos_VehiculoId] ON [dbo].[tblTanqueos] ([VehiculoId]);
CREATE INDEX [IX_tblTanqueos_Placa] ON [dbo].[tblTanqueos] ([Placa]);
CREATE INDEX [IX_tblTanqueos_Fecha] ON [dbo].[tblTanqueos] ([Fecha]);

-- Autorizaciones. Validación de la pre-autorización en la EDS (placa + ventana) y
-- FKs frecuentes. El índice único filtrado impide dos propuestas simultáneas para
-- el mismo viaje (carrera entre dos usuarios de despacho).
CREATE INDEX [IX_tblAutorizacionesTanqueo_VehiculoId_Ventana] ON [dbo].[tblAutorizacionesTanqueo] ([VehiculoId], [FechaInicio], [FechaFin]);
CREATE INDEX [IX_tblAutorizacionesTanqueo_ViajeId] ON [dbo].[tblAutorizacionesTanqueo] ([ViajeId]);
CREATE INDEX [IX_tblAutorizacionesTanqueo_EstacionId] ON [dbo].[tblAutorizacionesTanqueo] ([EstacionId]);
CREATE INDEX [IX_tblAutorizacionesTanqueo_Estado] ON [dbo].[tblAutorizacionesTanqueo] ([Estado]);
CREATE UNIQUE INDEX [UX_tblAutorizacionesTanqueo_ViajePropuesta] ON [dbo].[tblAutorizacionesTanqueo] ([ViajeId]) WHERE [Estado] = N'propuesta';

-- Eventos de tanqueo. Un solo evento por autorización (UX); conciliación por
-- vehículo y fecha; filtros del tablero por programa y producto.
CREATE UNIQUE INDEX [UX_tblEventosTanqueo_AutorizacionId] ON [dbo].[tblEventosTanqueo] ([AutorizacionId]);
CREATE INDEX [IX_tblEventosTanqueo_VehiculoId_OcurridoAt] ON [dbo].[tblEventosTanqueo] ([VehiculoId], [OcurridoAt]);
CREATE INDEX [IX_tblEventosTanqueo_EstacionId] ON [dbo].[tblEventosTanqueo] ([EstacionId]);
CREATE INDEX [IX_tblEventosTanqueo_Programa] ON [dbo].[tblEventosTanqueo] ([Programa]);
CREATE INDEX [IX_tblEventosTanqueo_Producto] ON [dbo].[tblEventosTanqueo] ([Producto]);

-- Alertas. Bandeja filtrada por estado; búsquedas por vehículo y evento.
CREATE INDEX [IX_tblAlertas_Estado_CreatedAt] ON [dbo].[tblAlertas] ([Estado], [CreatedAt]);
CREATE INDEX [IX_tblAlertas_VehiculoId] ON [dbo].[tblAlertas] ([VehiculoId]);
CREATE INDEX [IX_tblAlertas_EventoId] ON [dbo].[tblAlertas] ([EventoId]);

-- Auditoría. Trazabilidad por entidad, actor y fecha.
CREATE INDEX [IX_tblAuditLog_Entidad_EntidadId] ON [dbo].[tblAuditLog] ([Entidad], [EntidadId]);
CREATE INDEX [IX_tblAuditLog_ActorId] ON [dbo].[tblAuditLog] ([ActorId]);
CREATE INDEX [IX_tblAuditLog_CreatedAt] ON [dbo].[tblAuditLog] ([CreatedAt]);
GO

-- =============================================================================
-- 3. LLAVES FORÁNEAS
-- =============================================================================
-- NO ACTION (restrict) por defecto: nada que tenga historial se borra en cascada.
-- Excepciones: referencias "quién hizo" y vínculos opcionales pasan a NULL al borrar
-- el referenciado; tblViajePreciosRef se borra con su viaje.

ALTER TABLE [dbo].[tblViajes] ADD CONSTRAINT [FK_tblViajes_tblVehiculos_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [dbo].[tblVehiculos] ([Id]);
ALTER TABLE [dbo].[tblViajes] ADD CONSTRAINT [FK_tblViajes_tblUsuarios_DespachadoPor] FOREIGN KEY ([DespachadoPor]) REFERENCES [dbo].[tblUsuarios] ([Id]) ON DELETE SET NULL;
ALTER TABLE [dbo].[tblRutas] ADD CONSTRAINT [FK_tblRutas_tblViajes_ViajeId] FOREIGN KEY ([ViajeId]) REFERENCES [dbo].[tblViajes] ([Id]) ON DELETE SET NULL;
ALTER TABLE [dbo].[tblViajePreciosRef] ADD CONSTRAINT [FK_tblViajePreciosRef_tblViajes_ViajeId] FOREIGN KEY ([ViajeId]) REFERENCES [dbo].[tblViajes] ([Id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[tblTanqueos] ADD CONSTRAINT [FK_tblTanqueos_tblVehiculos_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [dbo].[tblVehiculos] ([Id]) ON DELETE SET NULL;
ALTER TABLE [dbo].[tblAutorizacionesTanqueo] ADD CONSTRAINT [FK_tblAutorizacionesTanqueo_tblViajes_ViajeId] FOREIGN KEY ([ViajeId]) REFERENCES [dbo].[tblViajes] ([Id]);
ALTER TABLE [dbo].[tblAutorizacionesTanqueo] ADD CONSTRAINT [FK_tblAutorizacionesTanqueo_tblVehiculos_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [dbo].[tblVehiculos] ([Id]);
ALTER TABLE [dbo].[tblAutorizacionesTanqueo] ADD CONSTRAINT [FK_tblAutorizacionesTanqueo_tblEstaciones_EstacionId] FOREIGN KEY ([EstacionId]) REFERENCES [dbo].[tblEstaciones] ([Id]);
ALTER TABLE [dbo].[tblAutorizacionesTanqueo] ADD CONSTRAINT [FK_tblAutorizacionesTanqueo_tblUsuarios_AutorizadoPor] FOREIGN KEY ([AutorizadoPor]) REFERENCES [dbo].[tblUsuarios] ([Id]) ON DELETE SET NULL;
ALTER TABLE [dbo].[tblEventosTanqueo] ADD CONSTRAINT [FK_tblEventosTanqueo_tblAutorizacionesTanqueo_AutorizacionId] FOREIGN KEY ([AutorizacionId]) REFERENCES [dbo].[tblAutorizacionesTanqueo] ([Id]);
ALTER TABLE [dbo].[tblEventosTanqueo] ADD CONSTRAINT [FK_tblEventosTanqueo_tblVehiculos_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [dbo].[tblVehiculos] ([Id]);
ALTER TABLE [dbo].[tblEventosTanqueo] ADD CONSTRAINT [FK_tblEventosTanqueo_tblEstaciones_EstacionId] FOREIGN KEY ([EstacionId]) REFERENCES [dbo].[tblEstaciones] ([Id]);
ALTER TABLE [dbo].[tblEventosTanqueo] ADD CONSTRAINT [FK_tblEventosTanqueo_tblUsuarios_RegistradoPor] FOREIGN KEY ([RegistradoPor]) REFERENCES [dbo].[tblUsuarios] ([Id]) ON DELETE SET NULL;
ALTER TABLE [dbo].[tblAlertas] ADD CONSTRAINT [FK_tblAlertas_tblVehiculos_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [dbo].[tblVehiculos] ([Id]);
ALTER TABLE [dbo].[tblAlertas] ADD CONSTRAINT [FK_tblAlertas_tblEventosTanqueo_EventoId] FOREIGN KEY ([EventoId]) REFERENCES [dbo].[tblEventosTanqueo] ([Id]) ON DELETE SET NULL;
ALTER TABLE [dbo].[tblAlertas] ADD CONSTRAINT [FK_tblAlertas_tblUsuarios_GestionadaPor] FOREIGN KEY ([GestionadaPor]) REFERENCES [dbo].[tblUsuarios] ([Id]) ON DELETE SET NULL;

-- ActorId sin ON DELETE SET NULL a propósito: SQL Server no permite una acción
-- referencial en cascada sobre una tabla con trigger INSTEAD OF UPDATE/DELETE, y
-- además un usuario con historial de auditoría no debe poder borrarse.
ALTER TABLE [dbo].[tblAuditLog] ADD CONSTRAINT [FK_tblAuditLog_tblUsuarios_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[tblUsuarios] ([Id]);
GO

-- =============================================================================
-- 4. TRIGGERS
-- =============================================================================
-- UpdatedAt: un trigger AFTER UPDATE por tabla mutable. Opera sobre la pseudo-tabla
-- "inserted" completa (soporta UPDATE de varias filas) y se protege de la
-- recursión con TRIGGER_NESTLEVEL.
-- tblAuditLog: INSTEAD OF UPDATE, DELETE que lanza error (append-only).
-- tblRutas: además deriva los extremos de la polilínea.

-- tblUsuarios
GO
CREATE TRIGGER [dbo].[TRG_tblUsuarios_UpdatedAt]
ON [dbo].[tblUsuarios]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblUsuarios] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblVehiculos
CREATE TRIGGER [dbo].[TRG_tblVehiculos_UpdatedAt]
ON [dbo].[tblVehiculos]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblVehiculos] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblConductores
CREATE TRIGGER [dbo].[TRG_tblConductores_UpdatedAt]
ON [dbo].[tblConductores]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblConductores] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblEstaciones
CREATE TRIGGER [dbo].[TRG_tblEstaciones_UpdatedAt]
ON [dbo].[tblEstaciones]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblEstaciones] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblViajes
CREATE TRIGGER [dbo].[TRG_tblViajes_UpdatedAt]
ON [dbo].[tblViajes]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblViajes] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblViajePreciosRef (la llave es ViajeId)
CREATE TRIGGER [dbo].[TRG_tblViajePreciosRef_UpdatedAt]
ON [dbo].[tblViajePreciosRef]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblViajePreciosRef] t
    INNER JOIN inserted i ON i.[ViajeId] = t.[ViajeId];
END;
GO

-- tblTanqueos
CREATE TRIGGER [dbo].[TRG_tblTanqueos_UpdatedAt]
ON [dbo].[tblTanqueos]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblTanqueos] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblAutorizacionesTanqueo
CREATE TRIGGER [dbo].[TRG_tblAutorizacionesTanqueo_UpdatedAt]
ON [dbo].[tblAutorizacionesTanqueo]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblAutorizacionesTanqueo] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblEventosTanqueo
CREATE TRIGGER [dbo].[TRG_tblEventosTanqueo_UpdatedAt]
ON [dbo].[tblEventosTanqueo]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblEventosTanqueo] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblAlertas
CREATE TRIGGER [dbo].[TRG_tblAlertas_UpdatedAt]
ON [dbo].[tblAlertas]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
    FROM [dbo].[tblAlertas] t
    INNER JOIN inserted i ON i.[Id] = t.[Id];
END;
GO

-- tblRutas: al insertar o al cambiar la polilínea, deriva el primer y el último
-- punto ([lat,lng]) hacia OrigenLat/OrigenLng y DestinoLat/DestinoLng (JSON de SQL
-- Server no admite índices negativos, por eso el último punto sale de OPENJSON).
-- En un UPDATE también refresca UpdatedAt.
CREATE TRIGGER [dbo].[TRG_tblRutas_Derivados]
ON [dbo].[tblRutas]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(@@PROCID, 'AFTER', 'DML') > 1 RETURN;

    IF EXISTS (SELECT 1 FROM deleted)
        UPDATE t SET [UpdatedAt] = sysdatetimeoffset()
        FROM [dbo].[tblRutas] t
        INNER JOIN inserted i ON i.[Id] = t.[Id];

    IF UPDATE([Polilinea])
        UPDATE t SET
            [OrigenLat]  = TRY_CAST(TRY_CAST(JSON_VALUE(i.[Polilinea], '$[0][0]') AS float) AS decimal(10,7)),
            [OrigenLng]  = TRY_CAST(TRY_CAST(JSON_VALUE(i.[Polilinea], '$[0][1]') AS float) AS decimal(10,7)),
            [DestinoLat] = TRY_CAST(TRY_CAST(JSON_VALUE(u.[Punto], '$[0]') AS float) AS decimal(10,7)),
            [DestinoLng] = TRY_CAST(TRY_CAST(JSON_VALUE(u.[Punto], '$[1]') AS float) AS decimal(10,7))
        FROM [dbo].[tblRutas] t
        INNER JOIN inserted i ON i.[Id] = t.[Id]
        OUTER APPLY (
            SELECT TOP (1) p.[value] AS [Punto]
            FROM OPENJSON(ISNULL(i.[Polilinea], N'[]')) p
            ORDER BY TRY_CAST(p.[key] AS int) DESC
        ) u;
END;
GO

-- tblAuditLog: append-only. Bloquea cualquier UPDATE o DELETE (incluidos los de
-- sysadmin; para vaciarla en desarrollo ver drop_all.sql). TRUNCATE TABLE no
-- dispara triggers: por eso se recomienda ADEMÁS revocar permisos al login de la
-- aplicación:
--     DENY UPDATE, DELETE ON [dbo].[tblAuditLog] TO [<login_o_usuario_de_la_app>];
CREATE TRIGGER [dbo].[TRG_tblAuditLog_AppendOnly]
ON [dbo].[tblAuditLog]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, N'tblAuditLog es append-only: no se permite UPDATE ni DELETE.', 1;
END;
GO
