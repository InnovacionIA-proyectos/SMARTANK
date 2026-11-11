-- =============================================================================
-- drop_all.sql - ELIMINA TODAS LAS TABLAS DE SMARTANK (SOLO DESARROLLO)
-- =============================================================================
-- ADVERTENCIA: este script BORRA TODOS LOS DATOS de forma irreversible. Úselo solo
-- en bases de desarrollo o de pruebas, nunca en una base con datos reales. Elimina
-- las tablas en orden inverso de dependencias (las FK, los índices y los triggers se
-- van con cada tabla), incluida tblAuditLog, que es append-only: DROP TABLE no es un
-- UPDATE/DELETE y por eso no lo bloquea su trigger.
--
-- Como protección, el script no hace nada hasta que cambie @Confirmar a 1 en la
-- primera línea ejecutable. Después de ejecutarlo, vuelva a crear el esquema con
-- ddl.sql (y seed_estaciones.sql).
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Confirmar bit = 0;   -- cambie a 1 para confirmar el borrado de TODAS las tablas

IF @Confirmar <> 1
BEGIN
    PRINT N'drop_all: cancelado. Cambie @Confirmar a 1 para borrar todas las tablas de SMARTANK.';
    SET NOEXEC ON;
END

DROP TABLE IF EXISTS [dbo].[tblAuditLog];
DROP TABLE IF EXISTS [dbo].[tblAlertas];
DROP TABLE IF EXISTS [dbo].[tblEventosTanqueo];
DROP TABLE IF EXISTS [dbo].[tblAutorizacionesTanqueo];
DROP TABLE IF EXISTS [dbo].[tblTanqueos];
DROP TABLE IF EXISTS [dbo].[tblViajePreciosRef];
DROP TABLE IF EXISTS [dbo].[tblRutas];
DROP TABLE IF EXISTS [dbo].[tblViajes];
DROP TABLE IF EXISTS [dbo].[tblEstaciones];
DROP TABLE IF EXISTS [dbo].[tblConductores];
DROP TABLE IF EXISTS [dbo].[tblVehiculos];
DROP TABLE IF EXISTS [dbo].[tblUploads];
DROP TABLE IF EXISTS [dbo].[tblUsuarios];

PRINT N'drop_all: tablas de SMARTANK eliminadas.';
GO

SET NOEXEC OFF;
GO
