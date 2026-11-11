/**
 * Formatea una fecha (Date, ISO string o epoch ms) como `aaaa/MM/dd HH:mm` en
 * hora local. Devuelve '—' si el valor es nulo/vacío o no es una fecha válida.
 * Formato único usado por toda la UI.
 */
export function formatFecha(valor: string | number | Date | null | undefined): string {
  if (valor == null || valor === '') return '—';
  const d = valor instanceof Date ? valor : new Date(valor);
  if (Number.isNaN(d.getTime())) return '—';
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}/${p(d.getMonth() + 1)}/${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}
