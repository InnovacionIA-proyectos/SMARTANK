import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, finalize } from 'rxjs';
import { ApiError } from '../../core/api/api-error';
import { cargarApi } from './cargar-api';

describe('cargarApi', () => {
  it('pasa de loading a data cuando llega la respuesta', () => {
    const fuente = new Subject<string>();
    const carga = TestBed.runInInjectionContext(() => cargarApi(() => fuente));
    TestBed.tick();

    expect(carga.loading()).toBe(true);
    expect(carga.data()).toBeNull();

    fuente.next('hola');
    fuente.complete();
    expect(carga.data()).toBe('hola');
    expect(carga.loading()).toBe(false);
    expect(carga.error()).toBeNull();
  });

  it('expone el mensaje del ApiError y conserva el último dato', () => {
    const filtro = signal(1);
    const fuentes = new Map<number, Subject<string>>([
      [1, new Subject<string>()],
      [2, new Subject<string>()],
    ]);
    const carga = TestBed.runInInjectionContext(() => cargarApi(() => fuentes.get(filtro())!));
    TestBed.tick();
    fuentes.get(1)!.next('uno');
    fuentes.get(1)!.complete();

    filtro.set(2);
    TestBed.tick();
    expect(carga.loading()).toBe(true);
    expect(carga.data()).toBe('uno'); // no se desmonta la vista mientras recarga

    fuentes.get(2)!.error(new ApiError('Sin permiso', 'FORBIDDEN', 403));
    expect(carga.error()).toBe('Sin permiso');
    expect(carga.loading()).toBe(false);
    expect(carga.data()).toBe('uno');
  });

  it('usa el mensaje de conexión ante errores que no son del API', () => {
    const fuente = new Subject<string>();
    const carga = TestBed.runInInjectionContext(() => cargarApi(() => fuente));
    TestBed.tick();
    fuente.error(new TypeError('boom'));
    expect(carga.error()).toBe('Error de conexión con el servidor');
  });

  it('cancela la consulta anterior cuando cambian las dependencias', () => {
    const id = signal('a');
    let cancelaciones = 0;
    const carga = TestBed.runInInjectionContext(() =>
      cargarApi(() => {
        id();
        return new Subject<string>().pipe(finalize(() => (cancelaciones += 1)));
      }),
    );
    TestBed.tick();
    expect(cancelaciones).toBe(0);

    id.set('b');
    TestBed.tick();
    expect(cancelaciones).toBe(1);
    expect(carga.loading()).toBe(true);
  });

  it('refetch vuelve a ejecutar la consulta y null la omite', () => {
    let llamadas = 0;
    const activo = signal(false);
    const carga = TestBed.runInInjectionContext(() =>
      cargarApi(() => {
        if (!activo()) return null;
        llamadas += 1;
        return new Subject<string>();
      }),
    );
    TestBed.tick();
    expect(llamadas).toBe(0);
    expect(carga.loading()).toBe(false);

    activo.set(true);
    TestBed.tick();
    expect(llamadas).toBe(1);

    carga.refetch();
    TestBed.tick();
    expect(llamadas).toBe(2);
  });
});
