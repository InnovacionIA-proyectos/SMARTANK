/**
 * Tipo de desplazamiento del viaje, deducido del manifiesto / numero_fo:
 *  · empieza con "32" y mide 10 caracteres → desplazamiento VACÍO
 *  · cualquier otro                         → desplazamiento CARGADO
 */
export type TipoDesplazamiento = 'vacio' | 'cargado';

export const DESPLAZAMIENTO_OPCIONES: { value: TipoDesplazamiento; label: string }[] = [
  { value: 'cargado', label: 'Cargados' },
  { value: 'vacio', label: 'Vacíos' },
];

export function tipoDesplazamiento(manifiestoOFo: string | null | undefined): TipoDesplazamiento {
  const t = (manifiestoOFo ?? '').trim();
  return t.length === 10 && t.startsWith('32') ? 'vacio' : 'cargado';
}
