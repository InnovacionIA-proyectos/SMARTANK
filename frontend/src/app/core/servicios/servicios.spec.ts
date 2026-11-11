import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiInterceptor } from '../http/api.interceptor';
import { AlertasService } from './alertas.service';
import { AutorizacionesService } from './autorizaciones.service';
import { BitacoraService } from './bitacora.service';
import { EstacionesService } from './estaciones.service';
import { EventosService } from './eventos.service';
import { MetricasService } from './metricas.service';
import { VehiculosService } from './vehiculos.service';
import { ViajesService } from './viajes.service';

describe('servicios del API', () => {
  let controlador: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    controlador = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controlador.verify());

  describe('ViajesService', () => {
    it('listar envía solo los filtros con valor, como query string', () => {
      TestBed.inject(ViajesService)
        .listar({
          estado: 'en_ruta',
          ruta: '',
          placa: 'SNU123',
          anio: '2026',
          con_recomendacion: '1',
        })
        .subscribe();
      const req = controlador.expectOne((r) => r.url === '/api/viajes');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.keys().sort()).toEqual([
        'anio',
        'con_recomendacion',
        'estado',
        'placa',
      ]);
      expect(req.request.params.get('estado')).toBe('en_ruta');
      req.flush({ total: 0, viajes: [], opciones: { rutas: [], anios: [] } });
    });

    it('ruta, autonomía y recomendación usan los paths del contrato', () => {
      const svc = TestBed.inject(ViajesService);
      svc.ruta('abc').subscribe();
      controlador.expectOne('/api/viajes/abc/ruta').flush({});

      svc.calcularAutonomia('abc').subscribe();
      const aut = controlador.expectOne('/api/viajes/abc/autonomia');
      expect(aut.request.method).toBe('POST');
      expect(aut.request.body).toBeNull();
      aut.flush({});

      svc.recomendar('abc').subscribe();
      const rec = controlador.expectOne('/api/viajes/abc/recomendacion');
      expect(rec.request.method).toBe('POST');
      expect(rec.request.body).toBeNull();
      rec.flush({});

      svc.recomendar('abc', { galones: 120 }).subscribe();
      const rec2 = controlador.expectOne('/api/viajes/abc/recomendacion');
      expect(rec2.request.body).toEqual({ galones: 120 });
      rec2.flush({});
    });
  });

  describe('AutorizacionesService', () => {
    it('detalle y acciones apuntan a /api/autorizaciones/{id}/...', () => {
      const svc = TestBed.inject(AutorizacionesService);

      svc.detalle('a1').subscribe();
      expect(controlador.expectOne('/api/autorizaciones/a1').request.method).toBe('GET');

      svc.autorizar('a1').subscribe();
      const autorizar = controlador.expectOne('/api/autorizaciones/a1/autorizar');
      expect(autorizar.request.method).toBe('POST');
      autorizar.flush({});

      svc.preautorizarEds('a1').subscribe();
      controlador.expectOne('/api/autorizaciones/a1/preautorizar-eds').flush({});

      svc.notificar('a1').subscribe();
      controlador.expectOne('/api/autorizaciones/a1/notificar').flush({});

      controlador.expectNone('/api/autorizaciones/a1');
    });

    it('crear envía el cuerpo tal cual en snake_case', () => {
      const cuerpo = {
        viaje_id: 'v',
        vehiculo_id: 'h',
        estacion_id: 'e',
        galones_max: 100,
        precio_techo: 9000,
        fecha_inicio: '2026-06-17T00:00:00Z',
        fecha_fin: '2026-06-18T00:00:00Z',
      };
      TestBed.inject(AutorizacionesService).crear(cuerpo).subscribe();
      const req = controlador.expectOne('/api/autorizaciones');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(cuerpo);
      req.flush({});
    });
  });

  describe('AlertasService', () => {
    it('listar filtra por estado y cambiarEstado hace PATCH con { estado }', () => {
      const svc = TestBed.inject(AlertasService);
      svc.listar('nueva').subscribe();
      const lista = controlador.expectOne((r) => r.url === '/api/alertas');
      expect(lista.request.params.get('estado')).toBe('nueva');
      lista.flush({ total: 0, alertas: [] });

      svc.listar('').subscribe();
      const todas = controlador.expectOne('/api/alertas');
      expect(todas.request.params.keys()).toEqual([]);
      todas.flush({ total: 0, alertas: [] });

      svc.cambiarEstado('al1', 'gestionada').subscribe();
      const patch = controlador.expectOne('/api/alertas/al1');
      expect(patch.request.method).toBe('PATCH');
      expect(patch.request.body).toEqual({ estado: 'gestionada' });
      patch.flush({});
    });
  });

  describe('EstacionesService', () => {
    it('listar envía la ruta y sincronizarPrecios hace POST sin cuerpo en modo automático', () => {
      const svc = TestBed.inject(EstacionesService);
      svc.listar('bogota-medellin').subscribe();
      const lista = controlador.expectOne((r) => r.url === '/api/estaciones');
      expect(lista.request.params.get('ruta')).toBe('bogota-medellin');
      lista.flush({ total: 0, estaciones: [] });

      svc.listar(null).subscribe();
      controlador.expectOne('/api/estaciones').flush({ total: 0, estaciones: [] });

      svc.sincronizarPrecios().subscribe();
      const sync = controlador.expectOne('/api/estaciones/precios/sync');
      expect(sync.request.method).toBe('POST');
      expect(sync.request.body).toBeNull();
      sync.flush({ modo: 'automatico' });
    });
  });

  describe('EventosService', () => {
    it('listar aplica los filtros del historial', () => {
      TestBed.inject(EventosService)
        .listar({ placa: 'SNU123', proveedor: 'terpel', desde: '2026-06-01', hasta: '' })
        .subscribe();
      const req = controlador.expectOne((r) => r.url === '/api/eventos');
      expect(req.request.params.keys().sort()).toEqual(['desde', 'placa', 'proveedor']);
      req.flush({ total: 0, eventos: [] });
    });

    it('registrar publica el cierre de la bitácora con el token', () => {
      const cuerpo = {
        token: 'tok',
        galones_cargados: 80.5,
        odometro_evento: null,
        foto_surtidor_url: null,
      };
      TestBed.inject(EventosService).registrar(cuerpo).subscribe();
      const req = controlador.expectOne('/api/eventos');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(cuerpo);
      req.flush({}, { status: 201, statusText: 'Created' });
    });
  });

  describe('MetricasService', () => {
    it('recuperacion consulta /api/metricas/recuperacion con los filtros', () => {
      TestBed.inject(MetricasService)
        .recuperacion({ anio: '2026', mes: '6', programa: '' })
        .subscribe();
      const req = controlador.expectOne((r) => r.url === '/api/metricas/recuperacion');
      expect(req.request.params.keys().sort()).toEqual(['anio', 'mes']);
      req.flush({});
    });
  });

  describe('VehiculosService', () => {
    it('telemetria consulta el último estado del vehículo', () => {
      TestBed.inject(VehiculosService).telemetria('veh1').subscribe();
      controlador.expectOne('/api/vehiculos/veh1/telemetria').flush({});
    });
  });

  describe('BitacoraService', () => {
    it('obtener usa el token en la ruta y lo codifica', () => {
      TestBed.inject(BitacoraService).obtener('a.b/c').subscribe();
      controlador.expectOne('/api/bitacora/a.b%2Fc').flush({});
    });

    it('subirFoto envía los bytes crudos con Content-Type de imagen y el token en la query', () => {
      const foto = new Blob([new Uint8Array([0xff, 0xd8, 0xff])], { type: 'image/jpeg' });
      TestBed.inject(BitacoraService).subirFoto('tok-123', foto).subscribe();
      const req = controlador.expectOne((r) => r.url === '/api/uploads');
      expect(req.request.method).toBe('POST');
      expect(req.request.params.get('token')).toBe('tok-123');
      expect(req.request.headers.get('Content-Type')).toBe('image/jpeg');
      expect(req.request.body).toBe(foto);
      req.flush({ id: 'u1', url: '/api/uploads/u1' }, { status: 201, statusText: 'Created' });
    });

    it('subirFoto usa image/jpeg si el blob no declara tipo', () => {
      TestBed.inject(BitacoraService)
        .subirFoto('t', new Blob([new Uint8Array([1])]))
        .subscribe();
      const req = controlador.expectOne((r) => r.url === '/api/uploads');
      expect(req.request.headers.get('Content-Type')).toBe('image/jpeg');
      req.flush({ id: 'u', url: '/u' });
    });
  });
});
