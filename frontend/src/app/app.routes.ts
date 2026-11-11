import { Routes } from '@angular/router';
import { authGuard, rolGuard } from './core/auth/guards';
import { ROLES_ALERTAS, ROLES_TABLERO } from './core/auth/roles';

/**
 * Rutas de la aplicación. Cada pantalla se carga bajo demanda (lazy) para que Leaflet
 * y Chart.js no engorden el paquete inicial.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'despacho' },

  // Públicas
  {
    path: 'login',
    title: 'Ingresar · SMARTANK',
    loadComponent: () => import('./features/login/login-page').then((m) => m.LoginPage),
  },
  {
    // Bitácora móvil del conductor: el token firmado del enlace de WhatsApp es la credencial.
    path: 'bitacora/:token',
    title: 'Cierre de Tanqueo · SMARTANK',
    loadComponent: () => import('./features/bitacora/bitacora-page').then((m) => m.BitacoraPage),
  },

  // Autenticadas (layout con sidebar)
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      {
        path: 'despacho',
        title: 'Panel de Despacho · SMARTANK',
        loadComponent: () =>
          import('./features/despacho/despacho-page').then((m) => m.DespachoPage),
      },
      {
        path: 'recomendacion',
        title: 'Recomendación de Tanqueo · SMARTANK',
        loadComponent: () =>
          import('./features/recomendacion/recomendacion-page').then((m) => m.RecomendacionPage),
      },
      {
        path: 'seguimiento',
        title: 'Seguimiento Control Combustible · SMARTANK',
        loadComponent: () =>
          import('./features/seguimiento/seguimiento-page').then((m) => m.SeguimientoPage),
      },
      {
        path: 'alertas',
        title: 'Bandeja de Alertas · SMARTANK',
        canActivate: [rolGuard(...ROLES_ALERTAS)],
        loadComponent: () => import('./features/alertas/alertas-page').then((m) => m.AlertasPage),
      },
      {
        path: 'tablero',
        title: 'Recuperación de Margen · SMARTANK',
        canActivate: [rolGuard(...ROLES_TABLERO)],
        loadComponent: () => import('./features/tablero/tablero-page').then((m) => m.TableroPage),
      },
      {
        path: 'historial',
        title: 'Historial de Tanqueos · SMARTANK',
        loadComponent: () =>
          import('./features/historial/historial-page').then((m) => m.HistorialPage),
      },
    ],
  },

  {
    path: '**',
    title: 'Página no encontrada · SMARTANK',
    loadComponent: () =>
      import('./features/no-encontrado/no-encontrado-page').then((m) => m.NoEncontradoPage),
  },
];
