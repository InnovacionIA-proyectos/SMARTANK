# Base de datos de SMARTANK

SQL Server 2019, esquema `dbo`. El esquema NO se crea con migraciones de EF Core: la unica definicion es `ddl.sql`, que EF solo mapea. Descripcion tabla por tabla en `../docs/MODELO_DE_DATOS.md`.

## Archivos

| Archivo | Para que sirve |
|---|---|
| `ddl.sql` | Todo el DDL: tablas, indices, llaves foraneas y triggers. Se ejecuta completo, una sola vez, en una base vacia. |
| `seed_estaciones.sql` | Catalogo de estaciones de servicio (EDS) con coordenadas: 1357 estaciones de Terpel y Distracom mas la estacion propia SyT. Requiere `ddl.sql`. |
| `drop_all.sql` | SOLO DESARROLLO: elimina todas las tablas. No hace nada hasta que se cambie `@Confirmar` a 1. |
| `scripts/convertir_seed_estaciones.py` | Regenera `seed_estaciones.sql` desde el seed de EDS de la aplicacion anterior (ver mas abajo). |

## Como ejecutar en DBeaver

1. Cree la base (o use una existente y vacia). Opcion A: descomente el bloque del inicio de `ddl.sql` (`CREATE DATABASE ... COLLATE Modern_Spanish_CI_AS` y nivel de compatibilidad 150), ejecutelo y cambie la conexion a esa base. Opcion B: cree la base desde DBeaver (clic derecho en Databases, Create New Database) con collation `Modern_Spanish_CI_AS` y fije la compatibilidad con `ALTER DATABASE [SmartankDb] SET COMPATIBILITY_LEVEL = 150;`.
2. Seleccione la base de SMARTANK como base activa de la conexion (no `master`).
3. Abra `ddl.sql` y ejecute el script completo con **Execute SQL Script** (Alt+X), no con Execute Statement. Los lotes estan separados con `GO`, que DBeaver interpreta como separador.
4. Abra `seed_estaciones.sql` y ejecutelo igual (Alt+X). Debe terminar con `(1000 rows affected)`, `(357 rows affected)` y `(1 rows affected)`.
5. Verifique: `SELECT COUNT(*) FROM dbo.tblEstaciones;` devuelve 1358 y `SELECT COUNT(*) FROM sys.tables;` devuelve 13.

Orden: `ddl.sql` y luego `seed_estaciones.sql`. Ambos son idempotentes solo hasta cierto punto: `ddl.sql` falla si las tablas ya existen (use `drop_all.sql` en desarrollo) y el seed se omite solo si el catalogo ya esta cargado.

### Con sqlcmd

`sqlcmd` necesita `-I` (identificadores entre comillas, requeridos por los indices filtrados y las columnas calculadas) y `-f 65001` (UTF-8):

```
sqlcmd -b -I -f 65001 -S <servidor> -U <usuario> -P <clave> -d <base> -i ddl.sql
sqlcmd -b -I -f 65001 -S <servidor> -U <usuario> -P <clave> -d <base> -i seed_estaciones.sql
```

## Decisiones de diseno

- **UpdatedAt por triggers.** Cada tabla mutable tiene un trigger `AFTER UPDATE` que refresca `UpdatedAt`. Se eligio trigger y no un interceptor de EF porque las sincronizaciones masivas y las transiciones atomicas de estado (`UPDATE ... WHERE Estado = <esperado>`, `ExecuteUpdate`) no pasan por `SaveChanges`. Consecuencia: EF mapea las tablas con `UseSqlOutputClause(false)`.
- **tblAuditLog append-only.** Trigger `INSTEAD OF UPDATE, DELETE` que lanza el error 50001. `TRUNCATE TABLE` no dispara triggers: ademas, ejecute con un usuario administrador, una sola vez, `DENY UPDATE, DELETE ON [dbo].[tblAuditLog] TO [<usuario_de_la_aplicacion>];` (y no le conceda `ALTER`).
- **Concurrencia optimista.** `tblAutorizacionesTanqueo` y `tblAlertas` tienen columna `RowVersion`. La aplicacion traduce el conflicto a `409 CONFLICTO_CONCURRENCIA`; una violacion de indice unico llega como `409 REGISTRO_DUPLICADO`.
- **Reglas hechas cumplir por la base.** Una sola autorizacion en estado `propuesta` por viaje (indice unico filtrado) y un solo evento de tanqueo por autorizacion (indice unico).
- **Columnas derivadas por trigger.** `tblRutas` deriva `OrigenLat/OrigenLng/DestinoLat/DestinoLng` desde la polilinea con el trigger `TRG_tblRutas_Derivados`.
- **Permisos.** La aplicacion necesita `SELECT, INSERT, UPDATE, DELETE` sobre las tablas (en `tblAuditLog` y `tblUploads` solo `SELECT, INSERT`) y no necesita permisos de DDL: las tablas las crea una persona con `ddl.sql`.

## Seed de estaciones

`seed_estaciones.sql` se genera con `scripts/convertir_seed_estaciones.py` a partir del seed de EDS de la aplicacion anterior (PostgreSQL). El script se deja en el repositorio para poder regenerar el seed si cambia el catalogo fuente:

```
py Database/scripts/convertir_seed_estaciones.py <ruta/al/seed_eds.sql>
```

Cambios respecto al origen: sintaxis T-SQL (`BEGIN TRANSACTION`/`COMMIT`, `true` a `1`, cadenas Unicode), columnas PascalCase y lotes de maximo 1000 filas por `INSERT`. **Los precios NO se migran**: el origen traia precios aleatorios de prueba; `PrecioGalonActual`, `PrecioAcpm`, `PrecioCorriente` y `PrecioActualizadoAt` quedan en NULL y los precios reales llegan por la sincronizacion de precios. La unica excepcion es la estacion propia SyT, que conserva su precio vigente de ACPM. No se migra la data demo de la aplicacion anterior.

## Cadena de conexion

La aplicacion lee `ConnectionStrings__Smartank` y `Database__Esquema` (debe valer `dbo`). Ejemplo:

```
Server=<servidor>,1433;Database=SmartankDb;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True
```
