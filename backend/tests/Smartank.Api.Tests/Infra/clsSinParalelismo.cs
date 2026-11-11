// Las pruebas contra SQL Server comparten una sola base y cada una mantiene una transaccion abierta (que se revierte al
// final): sus filas sin confirmar bloquean los escaneos de las demas y provocan interbloqueos. Se ejecutan en serie.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
