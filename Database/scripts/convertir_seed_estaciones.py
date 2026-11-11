#!/usr/bin/env python3
"""
Convierte el seed de estaciones (EDS) de la aplicación anterior (PostgreSQL) a T-SQL de SQL Server:
genera Database/seed_estaciones.sql.

Qué cambia respecto al origen:
  * Sintaxis T-SQL: BEGIN TRANSACTION / COMMIT, true -> 1, cadenas Unicode N'...'.
  * Tabla [dbo].[tblEstaciones] con columnas PascalCase.
  * Lotes de máximo 1000 filas por INSERT (límite de SQL Server para VALUES).
  * Los precios NO se migran: el origen los llenó con números aleatorios (placeholder 9100-12000).
    PrecioGalonActual, PrecioAcpm, PrecioCorriente y PrecioActualizadoAt quedan en NULL; los precios
    reales llegan por la sincronización de precios.
  * Se agrega la EDS propia "Estación SyT" (la insertaba una migración del origen, no el seed), con su
    precio vigente real de ACPM.
  * Es re-ejecutable: si ya existen estaciones de los proveedores terpel o distracom con código
    externo, el script no inserta nada (SET NOEXEC).

Uso (el origen es solo lectura):
  py Database/scripts/convertir_seed_estaciones.py <ruta/al/seed_eds.sql> [<ruta/de/salida.sql>]

Si no se indica salida se escribe Database/seed_estaciones.sql. El script se deja en el repositorio para
poder regenerar el seed si cambia el archivo fuente.
"""
import re
import sys
from pathlib import Path

# Un literal SQL del origen: cadena con '' escapado, NULL, número o booleano.
TOKEN = re.compile(r"'(?:[^']|'')*'|NULL|-?\d+(?:\.\d+)?|true|false")
LOTE_MAX = 1000
NUM_COLUMNAS_ORIGEN = 9  # codigo_externo, nombre, lat, lng, precio, precio_at, preaut, proveedor, producto


def convertir_valor(token: str) -> str:
    if token == "NULL":
        return "NULL"
    if token == "true":
        return "1"
    if token == "false":
        return "0"
    if token.startswith("'"):
        return "N" + token  # el contenido ya trae las comillas simples duplicadas
    return token


def leer_filas(ruta: Path) -> list[list[str]]:
    filas = []
    for linea in ruta.read_text(encoding="utf-8").splitlines():
        if not linea.startswith("('"):
            continue
        tokens = TOKEN.findall(linea)
        if len(tokens) != NUM_COLUMNAS_ORIGEN:
            raise ValueError(f"Fila con {len(tokens)} valores (se esperaban {NUM_COLUMNAS_ORIGEN}): {linea[:120]}")
        filas.append(tokens)
    return filas


def fila_tsql(tokens: list[str]) -> str:
    codigo, nombre, lat, lng, _precio, _precio_at, preaut, proveedor, producto = tokens
    # Precios en NULL a propósito (ver cabecera): se descartan precio y fecha de precio del origen.
    valores = [
        convertir_valor(codigo),
        convertir_valor(nombre),
        convertir_valor(lat),
        convertir_valor(lng),
        "NULL",
        "NULL",
        "NULL",
        "NULL",
        convertir_valor(preaut),
        convertir_valor(proveedor),
        convertir_valor(producto),
    ]
    return "(" + ", ".join(valores) + ")"


COLUMNAS = (
    "[CodigoExterno], [Nombre], [Lat], [Lng], [PrecioGalonActual], [PrecioAcpm], "
    "[PrecioCorriente], [PrecioActualizadoAt], [SoportaPreautorizacion], [Proveedor], [Producto]"
)


def main() -> None:
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    origen = Path(sys.argv[1])
    salida = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).resolve().parent.parent / "seed_estaciones.sql"

    filas = leer_filas(origen)
    por_proveedor: dict[str, int] = {}
    sin_coords = 0
    for t in filas:
        proveedor = t[7].strip("'")
        por_proveedor[proveedor] = por_proveedor.get(proveedor, 0) + 1
        if t[2] == "NULL":
            sin_coords += 1

    resumen = ", ".join(f"{k}: {v}" for k, v in por_proveedor.items())
    partes = [
        "-- =============================================================================",
        "-- Seed de estaciones (EDS) de SMARTANK - SQL Server 2019",
        "-- =============================================================================",
        "-- Generado por Database/scripts/convertir_seed_estaciones.py a partir del catálogo de EDS",
        "-- reales de las redes Terpel/Rumbo y Distracom. No editar a mano: regenerar con el script.",
        f"-- {len(filas)} estaciones del catálogo ({resumen}; {sin_coords} sin coordenadas) + la",
        "-- estación propia SyT.",
        "--",
        "-- Los PRECIOS quedan en NULL a propósito (el origen traía números aleatorios de",
        "-- prueba): los precios reales llegan por la sincronización de precios. La única excepción",
        "-- es la estación propia SyT, cuyo precio de ACPM (10049) es el vigente real.",
        "--",
        "-- Requisitos: haber ejecutado ddl.sql. Es re-ejecutable: si ya hay estaciones de los",
        "-- proveedores terpel o distracom con código externo, no inserta nada.",
        "-- Un lote de INSERT admite máximo 1000 filas; por eso hay varios INSERT.",
        "-- Si lo ejecuta con sqlcmd use: -I -f 65001 (UTF-8).",
        "-- =============================================================================",
        "SET ANSI_NULLS ON;",
        "SET QUOTED_IDENTIFIER ON;",
        "SET XACT_ABORT ON;",
        "GO",
        "",
        "IF EXISTS (SELECT 1 FROM [dbo].[tblEstaciones]",
        "           WHERE [CodigoExterno] IS NOT NULL AND [Proveedor] IN (N'terpel', N'distracom'))",
        "BEGIN",
        "    PRINT N'seed_estaciones: el catálogo ya está cargado; no se inserta nada.';",
        "    SET NOEXEC ON;",
        "END",
        "",
        "BEGIN TRANSACTION;",
        "",
    ]
    for inicio in range(0, len(filas), LOTE_MAX):
        lote = filas[inicio:inicio + LOTE_MAX]
        partes.append(f"INSERT INTO [dbo].[tblEstaciones] ({COLUMNAS}) VALUES")
        partes.append(",\n".join(fila_tsql(t) for t in lote) + ";")
        partes.append("")

    partes += [
        "-- Estación propia SyT (Itagüí, Antioquia): no viene de Terpel ni Distracom. Idempotente por nombre.",
        "IF NOT EXISTS (SELECT 1 FROM [dbo].[tblEstaciones] WHERE [Nombre] = N'Estación SyT')",
        "    INSERT INTO [dbo].[tblEstaciones]",
        "        ([CodigoExterno], [Nombre], [Lat], [Lng], [PrecioGalonActual], [PrecioAcpm],",
        "         [PrecioActualizadoAt], [Proveedor])",
        "    VALUES (N'SYT', N'Estación SyT', 6.1813655, -75.5915450, 10049, 10049, sysdatetimeoffset(), N'syt');",
        "",
        "COMMIT TRANSACTION;",
        "GO",
        "",
        "SET NOEXEC OFF;",
        "GO",
        "",
    ]
    salida.write_text("\n".join(partes), encoding="utf-8", newline="\n")
    print(f"{salida}: {len(filas)} filas ({por_proveedor}), sin coordenadas: {sin_coords}")


if __name__ == "__main__":
    main()
