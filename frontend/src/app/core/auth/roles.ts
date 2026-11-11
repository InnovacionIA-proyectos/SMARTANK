import type { Rol } from '../api/api-types';

/**
 * Roles con acceso a cada módulo, alineados con las reglas del API
 * (`[Authorize(Roles = ...)]`): el backend sigue siendo la fuente de verdad y la
 * SPA solo evita mostrar pantallas que devolverían 403.
 */
export const ROLES_ALERTAS: readonly Rol[] = ['control_flota', 'admin'];
export const ROLES_TABLERO: readonly Rol[] = ['finanzas', 'admin'];
