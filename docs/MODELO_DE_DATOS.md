# Modelo de datos de SMARTANK

Diccionario de datos de la base propia de SMARTANK (SQL Server 2019, esquema `dbo`). La definicion ejecutable es `Database/ddl.sql`; las entidades estan en `backend/src/Smartank.Domain/<Modulo>/Entidades` y el mapeo de EF Core en `backend/src/Smartank.Infrastructure/Persistence/Configuraciones`. No hay migraciones de EF: un cambio de estructura se hace editando `ddl.sql` y aplicando el ALTER equivalente en cada base existente.

## Convenciones

- Tablas `tblXxx` en PascalCase plural; columnas en PascalCase.
- Llave primaria `Id` de tipo `uniqueidentifier` con `DEFAULT NEWSEQUENTIALID()` (excepcion: `tblViajePreciosRef`, cuya llave es `ViajeId`).
- Fechas `datetimeoffset` con `DEFAULT sysdatetimeoffset()`. Todas las tablas tienen `CreatedAt` y `UpdatedAt`.
- `UpdatedAt` lo mantiene la base: un trigger `AFTER UPDATE` por tabla mutable (`TRG_<tabla>_UpdatedAt`). EF nunca lo escribe, por eso las tablas se mapean con `UseSqlOutputClause(false)` (EF no puede usar `OUTPUT` sobre tablas con triggers). `tblAuditLog` y `tblUploads` son inmutables y no tienen trigger.
- Enums del dominio como `nvarchar` con `CHECK` en snake_case (los mismos textos del JSON del API). En C# se leen y escriben con `clsEnumSnakeCaseConverter<T>`.
- JSON como `nvarchar(max)` con `CHECK (ISJSON(col) = 1)`; imagenes como `varbinary(max)`.
- Dinero y galones son `decimal(p,s)` en C#; kilometros, rendimientos, odometros y coordenadas son `decimal(p,s)` en la base y `double` en C# (conversion en el mapeo EF).
- Valores por defecto de bit y enums: los replica el inicializador de la entidad (no `HasDefaultValue` de EF, que omitiria el valor CLR por defecto como `false` y dejaria que la base escriba el default).
- Llaves foraneas restrictivas por defecto; `ON DELETE SET NULL` en referencias opcionales ("quien hizo", vinculos) y `CASCADE` solo en `tblViajePreciosRef`.
- Collation recomendada `Modern_Spanish_CI_AS` (insensible a mayusculas): por eso `Placa`, `Email` y las busquedas de texto no necesitan `lower()`.

## Resumen de tablas

| Tabla | Entidad (namespace `Smartank.Domain.<Modulo>.Entidades`) | DbSet | Proposito |
|---|---|---|---|
| `tblUsuarios` | `Usuarios.Entidades.clsUsuario` | `Usuarios` | Cuentas de usuario con autenticacion propia y rol. |
| `tblVehiculos` | `Vehiculos.Entidades.clsVehiculo` | `Vehiculos` | Vehiculos (cabezotes) de la flota con parametros de consumo y ultima telemetria. |
| `tblConductores` | `Conductores.Entidades.clsConductor` | `Conductores` | Maestro de conductores sincronizado por cedula. |
| `tblEstaciones` | `Estaciones.Entidades.clsEstacion` | `Estaciones` | Catalogo de estaciones de servicio (EDS) con coordenadas y precios reales. |
| `tblViajes` | `Viajes.Entidades.clsViaje` | `Viajes` | Viajes (FO, fletes) reales y manuales del panel de despacho. |
| `tblRutas` | `Rutas.Entidades.clsRuta` | `Rutas` | Polilinea y detalle de kilometros de la ruta de cada FO. |
| `tblViajePreciosRef` | `Precios.Entidades.clsViajePrecioRef` | `ViajePreciosRef` | Precios ACPM minimo y maximo de las EDS cercanas a la ruta de cada viaje (panel de Recuperacion de Margen). |
| `tblTanqueos` | `Tanqueos.Entidades.clsTanqueo` | `Tanqueos` | Historial real de tanqueos de los programas Terpel y Distracom. |
| `tblAutorizacionesTanqueo` | `Autorizaciones.Entidades.clsAutorizacionTanqueo` | `AutorizacionesTanqueo` | Ordenes de tanqueo (propuesta, autorizacion y ejecucion) con precio techo y ventana de vigencia. |
| `tblEventosTanqueo` | `Eventos.Entidades.clsEventoTanqueo` | `EventosTanqueo` | Bitacora del conductor: lo que realmente se cargo contra una autorizacion. |
| `tblAlertas` | `Alertas.Entidades.clsAlerta` | `Alertas` | Alertas de control (desviaciones de consumo, estacion cara, ejecucion parcial). |
| `tblAuditLog` | `Auditoria.Entidades.clsAuditLog` | `AuditLog` | Auditoria append-only de toda accion que cambia estado. |
| `tblUploads` | `Uploads.Entidades.clsUpload` | `Uploads` | Fotos del surtidor subidas por el conductor, guardadas como bytes. |

## tblUsuarios

Cuentas de usuario con autenticacion propia y rol. Entidad `clsUsuario` (modulo Usuarios), `DbSet` `Usuarios`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `Email` | `nvarchar(256)` | No | Correo de acceso (login). Unico, sin distinguir mayusculas. |
| `PasswordHash` | `nvarchar(255)` | No | Hash bcrypt de la contrasena. Dato sensible: nunca se expone en el API. |
| `Nombre` | `nvarchar(200)` | Si | Nombre para mostrar. |
| `Rol` | `nvarchar(20)` | No | Rol del usuario (enum `Rol`). DEFAULT `despacho`. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** Referenciada por tblViajes.DespachadoPor, tblAutorizacionesTanqueo.AutorizadoPor, tblEventosTanqueo.RegistradoPor, tblAlertas.GestionadaPor (todas ON DELETE SET NULL) y tblAuditLog.ActorId (sin cascada: un usuario con auditoria no se borra).

**Indices.**

- `UX_tblUsuarios_Email` (unico): (Email)

**Reglas.**

- Rol: `despacho`, `control_flota`, `finanzas`, `admin`.

## tblVehiculos

Vehiculos (cabezotes) de la flota con parametros de consumo y ultima telemetria. Entidad `clsVehiculo` (modulo Vehiculos), `DbSet` `Vehiculos`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `Placa` | `nvarchar(20)` | No | Placa del cabezote. Unica. |
| `AliasPlaca` | `nvarchar(60)` | Si | Placa alterna o apodo de la unidad. |
| `TipoMotor` | `nvarchar(60)` | No | Tipo de motor (diesel, gasolina...); base del margen de tolerancia. |
| `Programa` | `nvarchar(60)` | Si | Programa de combustible de la flota (terpel, distracom...). Texto libre. |
| `ChipTanqueo` | `nvarchar(60)` | Si | Identificador del chip de tanqueo. |
| `NitPropietario` | `nvarchar(30)` | Si | NIT del propietario. |
| `CapacidadTanqueGal` | `decimal(10,3)` | Si | Capacidad fisica del tanque, en galones. |
| `GalonesReserva` | `decimal(10,3)` | Si | Galones de reserva del tanque. |
| `GalonesUtilizables` | `decimal(10,3)` | Si | Galones utilizables. |
| `GalonesTolerancia` | `decimal(10,3)` | Si | Tolerancia fisica en galones (no confundir con ToleranciaMotor, que es una fraccion). |
| `RendimientoHistorico` | `decimal(8,4)` | Si | Rendimiento real medido, en km por galon. |
| `RendimientoEsperado` | `decimal(8,4)` | Si | Rendimiento teorico, en km por galon. |
| `ToleranciaMotor` | `decimal(5,4)` | No | Fraccion 0-1 de tolerancia sobre el consumo esperado. DEFAULT 0.02. |
| `Odometro` | `decimal(12,3)` | Si | Odometro en kilometros. |
| `NivelTanqueActual` | `decimal(10,3)` | Si | Ultimo nivel de tanque leido, en galones. |
| `NivelAt` | `datetimeoffset` | Si | Fecha real de la ultima lectura de NIVEL de tanque. |
| `UltimaLat` | `decimal(10,7)` | Si | Ultima latitud GPS. |
| `UltimaLng` | `decimal(10,7)` | Si | Ultima longitud GPS. |
| `UbicacionAt` | `datetimeoffset` | Si | Fecha de la ultima lectura de POSICION. |
| `GeotabDeviceId` | `nvarchar(100)` | Si | Id del dispositivo en el proveedor de telemetria. Unico cuando existe. |
| `FpsapId` | `bigint` | Si | Id del cabezote en el maestro legado. Unico cuando existe. |
| `FpsapSincronizadoAt` | `datetimeoffset` | Si | Ultima sincronizacion con el maestro legado. |
| `Activo` | `bit` | No | Si el vehiculo esta activo en el maestro. DEFAULT 1. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** Referenciada por tblViajes, tblAutorizacionesTanqueo, tblEventosTanqueo, tblAlertas (restrictivas) y tblTanqueos (SET NULL).

**Indices.**

- `UX_tblVehiculos_Placa` (unico): (Placa)
- `UX_tblVehiculos_FpsapId` (unico) filtrado `WHERE [FpsapId] IS NOT NULL`: (FpsapId)
- `UX_tblVehiculos_GeotabDeviceId` (unico) filtrado `WHERE [GeotabDeviceId] IS NOT NULL`: (GeotabDeviceId)

**Reglas.**

- CHECK: cantidades y rendimientos >= 0, ToleranciaMotor entre 0 y 1, latitud entre -90 y 90, longitud entre -180 y 180.

## tblConductores

Maestro de conductores sincronizado por cedula. Entidad `clsConductor` (modulo Conductores), `DbSet` `Conductores`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `Cedula` | `nvarchar(30)` | No | Cedula del conductor. Unica; enlaza con tblViajes.CedulaConductor. |
| `Nombres` | `nvarchar(200)` | No | Nombre completo. |
| `NumeroCelular` | `nvarchar(30)` | Si | Celular al que se envia la orden de tanqueo por WhatsApp. |
| `Activo` | `bit` | No | Si esta activo. DEFAULT 1. |
| `UtilizarTarjetaPrepago` | `bit` | No | Si utiliza tarjeta prepago. DEFAULT 0. |
| `FpsapId` | `bigint` | Si | Id en el maestro legado. Unico cuando existe. |
| `FpsapSincronizadoAt` | `datetimeoffset` | Si | Ultima sincronizacion con el maestro legado. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** Sin llaves foraneas; se enlaza por Cedula con tblViajes.CedulaConductor y tblTanqueos.CedulaConductor.

**Indices.**

- `UX_tblConductores_Cedula` (unico): (Cedula)
- `UX_tblConductores_FpsapId` (unico) filtrado `WHERE [FpsapId] IS NOT NULL`: (FpsapId)

## tblEstaciones

Catalogo de estaciones de servicio (EDS) con coordenadas y precios reales. Entidad `clsEstacion` (modulo Estaciones), `DbSet` `Estaciones`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `CodigoExterno` | `nvarchar(50)` | Si | Id de la EDS en su fuente (Terpel `EDS####`, Distracom entero, `SYT`). |
| `Nombre` | `nvarchar(200)` | No | Nombre de la estacion. |
| `NombreNormalizado` | calculada: `lower(ltrim(rtrim([Nombre])))` | - | Nombre en minusculas y sin espacios laterales, para cruzar con el nombre de los tanqueos historicos. |
| `Ruta` | `nvarchar(200)` | Si | Ruta asociada (texto libre; no se precalcula, la cercania a la ruta se calcula contra la polilinea del viaje). |
| `Lat` | `decimal(10,7)` | Si | Latitud (NULL = sin coordenadas). |
| `Lng` | `decimal(10,7)` | Si | Longitud. |
| `Proveedor` | `nvarchar(60)` | Si | Red de abastecimiento: terpel, distracom o syt (propia). |
| `Producto` | `nvarchar(120)` | Si | Combustibles que vende (ACPM, corriente, extra, GNV...). Texto libre. |
| `PrecioGalonActual` | `decimal(12,2)` | Si | Precio vigente por galon; replica PrecioAcpm. NULL = sin dato. |
| `PrecioAcpm` | `decimal(12,2)` | Si | Precio real del galon de ACPM. NULL = no vende o sin dato. |
| `PrecioCorriente` | `decimal(12,2)` | Si | Precio real del galon de gasolina corriente. |
| `PrecioActualizadoAt` | `datetimeoffset` | Si | Fecha del ultimo precio sincronizado. |
| `SoportaPreautorizacion` | `bit` | No | Si la EDS admite pre-autorizacion de la orden. DEFAULT 0. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** Referenciada por tblAutorizacionesTanqueo y tblEventosTanqueo (restrictivas). Se cruza por NombreNormalizado con tblTanqueos.Estacion.

**Indices.**

- `IX_tblEstaciones_Ruta`: (Ruta)
- `IX_tblEstaciones_Ruta_PrecioGalonActual`: (Ruta, PrecioGalonActual)
- `IX_tblEstaciones_Lat_Lng`: (Lat, Lng)
- `IX_tblEstaciones_Proveedor`: (Proveedor)
- `IX_tblEstaciones_Producto`: (Producto)
- `IX_tblEstaciones_CodigoExterno`: (CodigoExterno)
- `IX_tblEstaciones_NombreNormalizado`: (NombreNormalizado)

**Reglas.**

- CHECK: coordenadas en rango y precios >= 0.
- Los precios llegan por la sincronizacion; el seed los deja en NULL.

## tblViajes

Viajes (FO, fletes) reales y manuales del panel de despacho. Entidad `clsViaje` (modulo Viajes), `DbSet` `Viajes`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `VehiculoId` | `uniqueidentifier` | No | Vehiculo del viaje (FK a tblVehiculos). |
| `DespachadoPor` | `uniqueidentifier` | Si | Usuario que despacho el viaje (FK a tblUsuarios, SET NULL). |
| `Ruta` | `nvarchar(200)` | No | Nombre de la ruta origen-destino (NombreRuta del FO o, si viene vacio, CodigoRuta). |
| `Estado` | `nvarchar(20)` | No | Estado del viaje (enum `EstadoViaje`). DEFAULT `programado`. |
| `Manifiesto` | `nvarchar(60)` | Si | Numero del manifiesto de carga. |
| `AutonomiaEstimadaKm` | `decimal(10,2)` | Si | Autonomia estimada con el nivel de tanque al despachar, en km. |
| `InicioAt` | `datetimeoffset` | Si | Inicio real del recorrido; NULL mientras esta programado. |
| `FechaDespacho` | `datetimeoffset` | Si | Fecha de despacho del FO. |
| `FechaEfectiva` | calculada: `isnull([InicioAt], [FechaDespacho])` | - | Inicio real si existe y, si no, fecha de despacho. |
| `FoId` | `bigint` | Si | Id del FO en el sistema de fletes. Unico cuando existe. |
| `NumeroFo` | `nvarchar(30)` | Si | Numero de FO con ceros a la izquierda tal como viene del sistema de fletes. |
| `PlacaCabezote` | `nvarchar(20)` | Si | Placa del cabezote del FO. |
| `PlacaTrailer` | `nvarchar(20)` | Si | Placa del trailer. |
| `ProgramaCabezote` | `nvarchar(60)` | Si | Programa de combustible del cabezote. |
| `CedulaConductor` | `nvarchar(30)` | Si | Cedula del conductor (enlaza con tblConductores.Cedula, sin FK). |
| `Transportista` | `nvarchar(200)` | Si | Nombre del transportista. |
| `CodigoRuta` | `nvarchar(50)` | Si | Codigo de la ruta. |
| `NombreRuta` | `nvarchar(250)` | Si | Nombre de la ruta. |
| `CiudadOrigen` | `nvarchar(100)` | Si | Codigo de la ciudad de origen. |
| `CiudadDestino` | `nvarchar(100)` | Si | Codigo de la ciudad de destino. |
| `PesoFo` | `decimal(14,3)` | Si | Peso del FO. |
| `GalonesEds` | `decimal(10,3)` | Si | Galones tanqueados en EDS. |
| `GalonesCredito` | `decimal(10,3)` | Si | Galones a credito. |
| `GalonesContado` | `decimal(10,3)` | Si | Galones de contado. |
| `GalonesRuta` | `decimal(10,3)` | Si | Galones asignados a la ruta. |
| `KmtsRuta` | `decimal(12,3)` | Si | Kilometros de la ruta. |
| `GastosGenerados` | `bit` | Si | Bandera del FO: gastos generados. |
| `GastosEnviadoSap` | `bit` | Si | Bandera del FO: gastos enviados a SAP. |
| `ConsumoEnviadoSap` | `bit` | Si | Bandera del FO: consumo enviado a SAP. |
| `EnviadoSap` | `bit` | Si | Bandera del FO: enviado a SAP. |
| `Borrado` | `bit` | No | FO borrado en el origen (estado descartado). DEFAULT 0. |
| `FoSincronizadoAt` | `datetimeoffset` | Si | Ultima sincronizacion del FO. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK a tblVehiculos (restrictiva) y tblUsuarios (SET NULL). Referenciada por tblAutorizacionesTanqueo (restrictiva), tblRutas (SET NULL) y tblViajePreciosRef (CASCADE).

**Indices.**

- `IX_tblViajes_DespachadoPor`: (DespachadoPor)
- `IX_tblViajes_VehiculoId`: (VehiculoId)
- `IX_tblViajes_Estado`: (Estado)
- `IX_tblViajes_Ruta`: (Ruta)
- `IX_tblViajes_Manifiesto`: (Manifiesto)
- `IX_tblViajes_FechaDespacho`: (FechaDespacho)
- `IX_tblViajes_PlacaCabezote`: (PlacaCabezote)
- `IX_tblViajes_NumeroFo`: (NumeroFo)
- `IX_tblViajes_VehiculoId_FechaEfectiva`: (VehiculoId, FechaEfectiva DESC)
- `UX_tblViajes_FoId` (unico) filtrado `WHERE [FoId] IS NOT NULL`: (FoId)

**Reglas.**

- Estado: `programado`, `en_ruta`, `cerrado`, `descartado`.

## tblRutas

Polilinea y detalle de kilometros de la ruta de cada FO. Entidad `clsRuta` (modulo Rutas), `DbSet` `Rutas`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `Fo` | `nvarchar(30)` | No | Numero de FO SIN ceros a la izquierda. Unico. |
| `ViajeId` | `uniqueidentifier` | Si | Viaje enlazado (FK a tblViajes, SET NULL). |
| `Fuente` | `nvarchar(20)` | No | Origen de la polilinea (enum `FuenteRuta`). DEFAULT `valhalla`. |
| `ValhallaId` | `bigint` | Si | Id de la peticion en el servicio de ruteo. |
| `Kilometros` | `decimal(12,3)` | Si | Longitud geodesica de la ruta, en km. |
| `NumPuntos` | `int` | Si | Puntos de la polilinea simplificada. |
| `NumPuntosOriginal` | `int` | Si | Puntos de la geometria cruda. |
| `Polilinea` | `nvarchar(max)` | Si | JSON `[[lat,lng],...]` simplificada. CHECK ISJSON. |
| `DetalleKm` | `nvarchar(max)` | Si | JSON con el detalle de tramos (costo, tiempo, origen, destino). CHECK ISJSON. |
| `OrigenLat` | `decimal(10,7)` | Si | Latitud del primer punto; la deriva el trigger TRG_tblRutas_Derivados. |
| `OrigenLng` | `decimal(10,7)` | Si | Longitud del primer punto (trigger). |
| `DestinoLat` | `decimal(10,7)` | Si | Latitud del ultimo punto (trigger). |
| `DestinoLng` | `decimal(10,7)` | Si | Longitud del ultimo punto (trigger). |
| `FechaCalculo` | `datetimeoffset` | Si | Fecha de calculo de la ruta en el servicio de ruteo. |
| `SincronizadoAt` | `datetimeoffset` | Si | Ultima sincronizacion. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK opcional a tblViajes (SET NULL).

**Indices.**

- `UX_tblRutas_Fo` (unico): (Fo)
- `IX_tblRutas_ViajeId`: (ViajeId)

**Reglas.**

- Fuente: `valhalla`, `dane`.
- El trigger TRG_tblRutas_Derivados llena OrigenLat/OrigenLng/DestinoLat/DestinoLng al insertar o cambiar la polilinea.
- La polilinea es grande: evitar cargarla en listados (proyectar solo las columnas necesarias).

## tblViajePreciosRef

Precios ACPM minimo y maximo de las EDS cercanas a la ruta de cada viaje (panel de Recuperacion de Margen). Entidad `clsViajePrecioRef` (modulo Precios), `DbSet` `ViajePreciosRef`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `ViajeId` | `uniqueidentifier` | No | Llave primaria y FK a tblViajes (ON DELETE CASCADE). Una fila por viaje. |
| `AcpmMin` | `decimal(12,2)` | Si | ACPM mas barato entre las EDS cercanas a la ruta. |
| `AcpmMax` | `decimal(12,2)` | Si | ACPM mas caro entre las EDS cercanas. |
| `EdsCerca` | `int` | No | Cuantas EDS de ACPM cumplieron el umbral de cercania. DEFAULT 0. |
| `CalculadoAt` | `datetimeoffset` | No | Fecha del calculo. DEFAULT sysdatetimeoffset(). |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK a tblViajes (ON DELETE CASCADE), relacion 1 a 1.

## tblTanqueos

Historial real de tanqueos de los programas Terpel y Distracom. Entidad `clsTanqueo` (modulo Tanqueos), `DbSet` `Tanqueos`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `Fuente` | `nvarchar(20)` | No | Programa de abastecimiento (enum `FuenteTanqueo`). |
| `FuenteId` | `bigint` | No | Id del registro en la tabla de origen. (Fuente, FuenteId) es unico. |
| `VehiculoId` | `uniqueidentifier` | Si | Vehiculo (FK a tblVehiculos, SET NULL); NULL si la placa no esta en la flota. |
| `Placa` | `nvarchar(20)` | No | Placa tal como la reporta la fuente. |
| `Fecha` | `datetimeoffset` | No | Fecha del tanqueo. |
| `Galones` | `decimal(10,3)` | Si | Galones cargados. |
| `ValorGalon` | `decimal(12,2)` | Si | Precio por galon. |
| `ValorTotal` | `decimal(16,2)` | Si | Valor total. |
| `Producto` | `nvarchar(120)` | Si | Combustible. |
| `FamiliaProducto` | `nvarchar(120)` | Si | Familia del producto (Terpel). |
| `Ciudad` | `nvarchar(120)` | Si | Ciudad. |
| `Estacion` | `nvarchar(200)` | Si | Nombre de la estacion segun la fuente. |
| `Kilometraje` | `decimal(12,3)` | Si | Odometro reportado, en km. |
| `CedulaConductor` | `nvarchar(30)` | Si | Cedula del conductor. |
| `Factura` | `nvarchar(60)` | Si | Factura o vale. |
| `Referencia` | `nvarchar(100)` | Si | Referencia o id de autorizacion/electronico. |
| `CentroCosto` | `nvarchar(100)` | Si | Centro de costo. |
| `SincronizadoAt` | `datetimeoffset` | Si | Ultima sincronizacion. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK opcional a tblVehiculos (SET NULL).

**Indices.**

- `UX_tblTanqueos_Fuente_FuenteId` (unico): (Fuente, FuenteId)
- `IX_tblTanqueos_VehiculoId`: (VehiculoId)
- `IX_tblTanqueos_Placa`: (Placa)
- `IX_tblTanqueos_Fecha`: (Fecha)

**Reglas.**

- Fuente: `distracom`, `terpel`.

## tblAutorizacionesTanqueo

Ordenes de tanqueo (propuesta, autorizacion y ejecucion) con precio techo y ventana de vigencia. Entidad `clsAutorizacionTanqueo` (modulo Autorizaciones), `DbSet` `AutorizacionesTanqueo`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `ViajeId` | `uniqueidentifier` | No | Viaje (FK a tblViajes). |
| `VehiculoId` | `uniqueidentifier` | No | Vehiculo (FK a tblVehiculos). |
| `EstacionId` | `uniqueidentifier` | No | EDS autorizada (FK a tblEstaciones). |
| `GalonesMax` | `decimal(10,3)` | No | Galones maximos a cargar. CHECK > 0. |
| `PrecioTecho` | `decimal(12,2)` | No | Precio maximo por galon. CHECK >= 0. |
| `DeltaAhorroEstimado` | `decimal(16,2)` | Si | Ahorro estimado frente a la estacion de referencia. |
| `FechaInicio` | `datetimeoffset` | No | Inicio de la ventana de vigencia. |
| `FechaFin` | `datetimeoffset` | No | Fin de la ventana. CHECK >= FechaInicio. |
| `Estado` | `nvarchar(20)` | No | Estado (enum `EstadoAutorizacion`). DEFAULT `propuesta`. |
| `AutorizadoPor` | `uniqueidentifier` | Si | Usuario que autorizo (FK a tblUsuarios, SET NULL). |
| `EdsConfirmacion` | `nvarchar(max)` | Si | Respuesta JSON de la EDS a la pre-autorizacion. CHECK ISJSON. |
| `RowVersion` | `rowversion` | No | Token de concurrencia optimista (rowversion). Lo genera la base. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK a tblViajes, tblVehiculos, tblEstaciones (restrictivas) y tblUsuarios (SET NULL). Referenciada por tblEventosTanqueo.

**Indices.**

- `IX_tblAutorizacionesTanqueo_AutorizadoPor`: (AutorizadoPor)
- `IX_tblAutorizacionesTanqueo_VehiculoId_Ventana`: (VehiculoId, FechaInicio, FechaFin)
- `IX_tblAutorizacionesTanqueo_ViajeId`: (ViajeId)
- `IX_tblAutorizacionesTanqueo_EstacionId`: (EstacionId)
- `IX_tblAutorizacionesTanqueo_Estado`: (Estado)
- `UX_tblAutorizacionesTanqueo_ViajePropuesta` (unico) filtrado `WHERE [Estado] = N'propuesta'`: (ViajeId)

**Reglas.**

- Estado: `propuesta`, `autorizada`, `ejecutada`, `ejecutada_parcial`, `expirada`, `rechazada`.
- RowVersion: las transiciones de estado usan concurrencia optimista; un conflicto llega a Application como 409 CONFLICTO_CONCURRENCIA.
- Indice unico filtrado: a lo sumo una autorizacion en estado `propuesta` por viaje.
- Las no ejecutadas (`expirada`, `rechazada`) se conservan, nunca se borran.

## tblEventosTanqueo

Bitacora del conductor: lo que realmente se cargo contra una autorizacion. Entidad `clsEventoTanqueo` (modulo Eventos), `DbSet` `EventosTanqueo`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `AutorizacionId` | `uniqueidentifier` | No | Autorizacion (FK a tblAutorizacionesTanqueo). Unica: un evento por autorizacion. |
| `VehiculoId` | `uniqueidentifier` | No | Vehiculo (FK). |
| `EstacionId` | `uniqueidentifier` | No | Estacion (FK). |
| `GalonesCargados` | `decimal(10,3)` | No | Galones cargados. CHECK >= 0. |
| `PrecioPagado` | `decimal(12,2)` | Si | Precio pagado por galon. |
| `FotoSurtidorUrl` | `nvarchar(500)` | Si | URL corta de la foto del surtidor. |
| `OdometroEvento` | `decimal(12,3)` | Si | Odometro al tanquear, en km. |
| `NivelAntes` | `decimal(10,3)` | Si | Nivel de tanque antes, en galones. |
| `NivelDespues` | `decimal(10,3)` | Si | Nivel de tanque despues. |
| `RegistradoPor` | `uniqueidentifier` | Si | Usuario que registro (FK a tblUsuarios, SET NULL); NULL si lo registro el conductor por el enlace. |
| `Programa` | `nvarchar(60)` | Si | Programa del vehiculo al momento del registro (instantanea). |
| `Producto` | `nvarchar(120)` | Si | Combustible cargado. |
| `OcurridoAt` | `datetimeoffset` | No | Momento del tanqueo. DEFAULT sysdatetimeoffset(). |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK a tblAutorizacionesTanqueo, tblVehiculos, tblEstaciones (restrictivas) y tblUsuarios (SET NULL). Referenciada por tblAlertas.EventoId (SET NULL).

**Indices.**

- `IX_tblEventosTanqueo_RegistradoPor`: (RegistradoPor)
- `UX_tblEventosTanqueo_AutorizacionId` (unico): (AutorizacionId)
- `IX_tblEventosTanqueo_VehiculoId_OcurridoAt`: (VehiculoId, OcurridoAt)
- `IX_tblEventosTanqueo_EstacionId`: (EstacionId)
- `IX_tblEventosTanqueo_Programa`: (Programa)
- `IX_tblEventosTanqueo_Producto`: (Producto)

**Reglas.**

- Un solo evento por autorizacion (indice unico).

## tblAlertas

Alertas de control (desviaciones de consumo, estacion cara, ejecucion parcial). Entidad `clsAlerta` (modulo Alertas), `DbSet` `Alertas`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `Tipo` | `nvarchar(30)` | No | Tipo de alerta (enum `TipoAlerta`). |
| `VehiculoId` | `uniqueidentifier` | No | Vehiculo (FK). |
| `EventoId` | `uniqueidentifier` | Si | Evento que la origino (FK a tblEventosTanqueo, SET NULL). |
| `MagnitudDesviacion` | `decimal(14,4)` | Si | Magnitud de la desviacion. |
| `Estado` | `nvarchar(20)` | No | Estado (enum `EstadoAlerta`). DEFAULT `nueva`. |
| `GestionadaPor` | `uniqueidentifier` | Si | Usuario que la gestiono (FK a tblUsuarios, SET NULL). |
| `RowVersion` | `rowversion` | No | Token de concurrencia optimista (rowversion). Lo genera la base. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Fecha de la ultima modificacion; la mantiene un trigger AFTER UPDATE. |

**Relaciones.** FK a tblVehiculos (restrictiva), tblEventosTanqueo (SET NULL) y tblUsuarios (SET NULL).

**Indices.**

- `IX_tblAlertas_GestionadaPor`: (GestionadaPor)
- `IX_tblAlertas_Estado_CreatedAt`: (Estado, CreatedAt)
- `IX_tblAlertas_VehiculoId`: (VehiculoId)
- `IX_tblAlertas_EventoId`: (EventoId)

**Reglas.**

- Tipo: `desviacion_consumo_carga`, `desviacion_consumo`, `estacion_cara`, `ejecucion_parcial`.
- Estado: `nueva`, `en_revision`, `gestionada`, `descartada`.
- RowVersion: evita que dos usuarios gestionen la misma alerta a la vez.

## tblAuditLog

Auditoria append-only de toda accion que cambia estado. Entidad `clsAuditLog` (modulo Auditoria), `DbSet` `AuditLog`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `ActorId` | `uniqueidentifier` | Si | Usuario que ejecuto la accion (FK a tblUsuarios, sin SET NULL); NULL para acciones del sistema. |
| `Accion` | `nvarchar(80)` | No | Accion, p. ej. `autorizar_tanqueo`. |
| `Entidad` | `nvarchar(80)` | No | Entidad afectada. |
| `EntidadId` | `uniqueidentifier` | Si | Id de la entidad afectada. |
| `Diff` | `nvarchar(max)` | Si | Estado anterior/posterior en JSON. CHECK ISJSON. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Igual a CreatedAt (tabla inmutable); no hay trigger. |

**Relaciones.** FK a tblUsuarios (ActorId).

**Indices.**

- `IX_tblAuditLog_Entidad_EntidadId`: (Entidad, EntidadId)
- `IX_tblAuditLog_ActorId`: (ActorId)
- `IX_tblAuditLog_CreatedAt`: (CreatedAt)

**Reglas.**

- APPEND-ONLY: el trigger TRG_tblAuditLog_AppendOnly (INSTEAD OF UPDATE, DELETE) lanza el error 50001. TRUNCATE no dispara triggers; se recomienda ademas `DENY UPDATE, DELETE` sobre la tabla al login de la aplicacion.
- La aplicacion solo inserta (`clsEfAuditoriaRepository`).

## tblUploads

Fotos del surtidor subidas por el conductor, guardadas como bytes. Entidad `clsUpload` (modulo Uploads), `DbSet` `Uploads`.

| Columna | Tipo SQL | Nulo | Descripcion |
|---|---|---|---|
| `Id` | `uniqueidentifier` | No | Llave primaria. DEFAULT NEWSEQUENTIALID() (EF genera el Guid secuencial del lado de la aplicacion). |
| `ContentType` | `nvarchar(100)` | No | Tipo de contenido (image/jpeg, image/png, image/webp). |
| `Data` | `varbinary(max)` | No | Bytes de la imagen. |
| `CreatedAt` | `datetimeoffset` | No | Fecha de creacion. DEFAULT sysdatetimeoffset(). |
| `UpdatedAt` | `datetimeoffset` | No | Igual a CreatedAt (tabla inmutable); no hay trigger. |

**Relaciones.** Sin llaves foraneas; los eventos la referencian por URL (FotoSurtidorUrl).

**Reglas.**

- Inmutable: la aplicacion solo inserta y lee.

## Enums y valores permitidos

| Enum de C# (`Smartank.Domain.Enums`) | Columna | Valores en la base (snake_case) |
|---|---|---|
| `Rol` | `tblUsuarios.Rol` | `despacho`, `control_flota`, `finanzas`, `admin` |
| `EstadoViaje` | `tblViajes.Estado` | `programado`, `en_ruta`, `cerrado`, `descartado` |
| `EstadoAutorizacion` | `tblAutorizacionesTanqueo.Estado` | `propuesta`, `autorizada`, `ejecutada`, `ejecutada_parcial`, `expirada`, `rechazada` |
| `TipoAlerta` | `tblAlertas.Tipo` | `desviacion_consumo_carga`, `desviacion_consumo`, `estacion_cara`, `ejecucion_parcial` |
| `EstadoAlerta` | `tblAlertas.Estado` | `nueva`, `en_revision`, `gestionada`, `descartada` |
| `FuenteRuta` | `tblRutas.Fuente` | `valhalla`, `dane` |
| `FuenteTanqueo` | `tblTanqueos.Fuente` | `distracom`, `terpel` |

## Triggers

| Trigger | Tabla | Tipo | Funcion |
|---|---|---|---|
| `TRG_<tabla>_UpdatedAt` | cada tabla mutable (11) | `AFTER UPDATE` | Refresca `UpdatedAt` con `sysdatetimeoffset()`. |
| `TRG_tblRutas_Derivados` | `tblRutas` | `AFTER INSERT, UPDATE` | Refresca `UpdatedAt` en UPDATE y deriva origen y destino desde la polilinea. |
| `TRG_tblAuditLog_AppendOnly` | `tblAuditLog` | `INSTEAD OF UPDATE, DELETE` | Lanza el error 50001 (la tabla es append-only). |

## Equivalencia con las tablas de origen

| Tabla de origen | Tabla nueva | Notas |
|---|---|---|
| tabla de origen `usuarios` | `tblUsuarios` | Misma estructura; `rol` pasa a `nvarchar` con CHECK. |
| tabla de origen `vehiculos` | `tblVehiculos` | Incluye las columnas agregadas por las migraciones (ubicacion, programa, maestro del cabezote, Geotab, nivel). |
| tabla de origen `conductores` | `tblConductores` | |
| tabla de origen `estaciones` | `tblEstaciones` | Se elimina `dist_ruta_km` (obsoleta). Se agrega `NombreNormalizado` (reemplaza el indice por expresion sobre el nombre). |
| tabla de origen `viajes` | `tblViajes` | `corredor` pasa a `Ruta`. Se agrega `FechaEfectiva` (reemplaza el indice por expresion sobre inicio y despacho). |
| tabla de origen `rutas` | `tblRutas` | Las columnas generadas de origen/destino pasan a ser mantenidas por trigger. |
| tabla de origen `viaje_precio_ref` | `tblViajePreciosRef` | |
| tabla de origen `tanqueos` | `tblTanqueos` | |
| tabla de origen `autorizaciones_tanqueo` | `tblAutorizacionesTanqueo` | Se agrega `RowVersion`; `eds_confirmacion` (jsonb) pasa a `nvarchar(max)` con CHECK ISJSON. |
| tabla de origen `eventos_tanqueo` | `tblEventosTanqueo` | El indice por autorizacion pasa a ser unico (un evento por autorizacion). |
| tabla de origen `alertas` | `tblAlertas` | Se agrega `RowVersion`. |
| tabla de origen `audit_log` | `tblAuditLog` | El bloqueo por triggers `BEFORE` pasa a un trigger `INSTEAD OF`; `diff` (jsonb) pasa a `nvarchar(max)` con CHECK ISJSON. |
| tabla de origen `uploads` | `tblUploads` | `bytea` pasa a `varbinary(max)`; se agrega `UpdatedAt`. |

Los tipos `ENUM` de PostgreSQL de origen (rol, estado de viaje, estado de autorizacion, tipo y estado de alerta) y los campos de texto `fuente` de rutas y tanqueos se representan como `nvarchar` con CHECK (ver la tabla de enums).

## Cambios deliberados respecto al origen

- Indice unico filtrado `UX_tblAutorizacionesTanqueo_ViajePropuesta` (una propuesta por viaje) e indice unico `UX_tblEventosTanqueo_AutorizacionId` (un evento por autorizacion): hacen cumplir en la base reglas que antes solo se validaban en la aplicacion y que son vulnerables a carreras entre usuarios.
- Indices sobre las columnas "quien hizo" hacia `tblUsuarios` para que `ON DELETE SET NULL` no recorra las tablas completas.
- `IX_tblViajes_NumeroFo`: la sincronizacion de rutas busca viajes por numero de FO.
- Los precios de las EDS no se migran con valores de prueba: `seed_estaciones.sql` los deja en NULL (excepcion: la estacion propia SyT).
- Precision de las columnas numericas: el origen declaraba `numeric` sin precision; aqui se fijo una `decimal(p,s)` explicita por tipo de dato (galones `(10,3)`, precios `(12,2)`, valores totales `(16,2)`, coordenadas `(10,7)`, kilometros y odometros `(12,3)`, rendimientos `(8,4)`).
