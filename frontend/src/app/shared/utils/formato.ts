/** Formateadores numéricos y de texto compartidos por las pantallas (locale es-CO). */

export const MESES = [
  '',
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
];

/** Opciones de mes para selects: valor '1'..'12' y nombre en español. */
export const MESES_OPCIONES = MESES.slice(1).map((label, i) => ({ value: String(i + 1), label }));

/** Primera letra en mayúscula. */
export const cap = (s: string): string => s.charAt(0).toUpperCase() + s.slice(1);

/** Pesos sin decimales: `$1.234`. Para el historial se usa `copMoneda` (Intl con símbolo COP). */
export const cop = (n: number): string => `$${Math.round(n).toLocaleString('es-CO')}`;

/** Pesos conservando decimales tal cual (recomendación: `$7.500`). */
export const copExacto = (n: number): string => `$${n.toLocaleString('es-CO')}`;

/** Pesos grandes en formato corto ("$1.043 mill."); montos pequeños, completos. */
export const plata = (n: number): string =>
  Math.abs(n) >= 1_000_000
    ? `$${(n / 1_000_000).toLocaleString('es-CO', { maximumFractionDigits: 1 })} mill.`
    : cop(n);

/** Moneda COP vía Intl (historial de tanqueos). */
const formatoMoneda = new Intl.NumberFormat('es-CO', {
  style: 'currency',
  currency: 'COP',
  maximumFractionDigits: 0,
});
export const copMoneda = (n: number): string => formatoMoneda.format(n);

/** Entero con separador de miles es-CO, o '—' si el valor es nulo. */
export const entero = (n: number | null | undefined, maxDecimales = 0): string =>
  n != null ? n.toLocaleString('es-CO', { maximumFractionDigits: maxDecimales }) : '—';
