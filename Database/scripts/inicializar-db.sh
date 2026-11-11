#!/bin/bash
# =============================================================================
# SMARTANK - inicializacion de la base de datos para Docker Compose (local / QA).
# =============================================================================
# Lo ejecuta el servicio "db-init" del docker-compose.yml con la propia imagen de SQL Server 2019 (trae sqlcmd).
# Es IDEMPOTENTE: crea la base si no existe y aplica ddl.sql (y el seed de estaciones) SOLO la primera vez.
# NO se usa en Dokploy: alli el esquema se crea a mano ejecutando Database/ddl.sql en la base corporativa.
#
# Variables: DB_HOST (por defecto db), DB_SA_PASSWORD (obligatoria), DB_NOMBRE (por defecto Smartank),
#            DB_CARGAR_ESTACIONES (true|false, por defecto true).
set -euo pipefail

DB_HOST="${DB_HOST:-db}"
DB_NOMBRE="${DB_NOMBRE:-Smartank}"
DB_CARGAR_ESTACIONES="${DB_CARGAR_ESTACIONES:-true}"
: "${DB_SA_PASSWORD:?Defina DB_SA_PASSWORD}"

# La imagen mssql/server:2019 trae mssql-tools18 (exige -C para confiar en el certificado); las antiguas, mssql-tools.
if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then
  SQLCMD=/opt/mssql-tools18/bin/sqlcmd
else
  SQLCMD=/opt/mssql-tools/bin/sqlcmd
fi

# -b: aborta con error si falla; -I: QUOTED_IDENTIFIER ON (lo exigen los indices filtrados); -f 65001: archivos en UTF-8.
sql() { "$SQLCMD" -C -b -I -f 65001 -S "$DB_HOST" -U sa -P "$DB_SA_PASSWORD" "$@"; }

echo "[db-init] Creando la base '$DB_NOMBRE' si no existe..."
sql -Q "IF DB_ID(N'$DB_NOMBRE') IS NULL
BEGIN
    CREATE DATABASE [$DB_NOMBRE] COLLATE Modern_Spanish_CI_AS;
    ALTER DATABASE [$DB_NOMBRE] SET COMPATIBILITY_LEVEL = 150;
    ALTER DATABASE [$DB_NOMBRE] SET READ_COMMITTED_SNAPSHOT ON;
END"

TABLAS=$(sql -d "$DB_NOMBRE" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name = N'tblUsuarios'" | tr -d '[:space:]')

if [ "$TABLAS" = "0" ]; then
  echo "[db-init] Aplicando ddl.sql..."
  sql -d "$DB_NOMBRE" -i /database/ddl.sql
  if [ "$DB_CARGAR_ESTACIONES" = "true" ]; then
    echo "[db-init] Cargando seed_estaciones.sql..."
    sql -d "$DB_NOMBRE" -i /database/seed_estaciones.sql
  fi
  echo "[db-init] Base '$DB_NOMBRE' lista."
else
  echo "[db-init] El esquema ya existe en '$DB_NOMBRE': no se aplica nada."
fi
