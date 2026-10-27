import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Creative, CreativeFields } from './creative.model';
import { CreativeStore } from './creative-store';
import { DisplayName } from './display-name';

const BASE = 'http://localhost:5114/api/creatives';

const fields: CreativeFields = { name: 'n', headline: 'h', body: 'b', cta: 'c', imageUrl: '' };

function creative(id: number, version = 1): Creative {
  return { id, ...fields, imageUrl: null, version, updatedAt: '2026-01-01T00:00:00Z', updatedBy: 'Ada' };
}

describe('CreativeStore', () => {
  let store: CreativeStore;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(DisplayName).set('Ada');
    store = TestBed.inject(CreativeStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('loadList', () => {
    it('is loading until the list arrives, then holds the Creatives', () => {
      store.loadList();
      expect(store.loading()).toBe(true);

      http.expectOne(BASE).flush([creative(1), creative(2)]);

      expect(store.loading()).toBe(false);
      expect(store.creatives().map((c) => c.id)).toEqual([1, 2]);
      expect(store.error()).toBeNull();
    });

    it('reports a network failure as an error and stops loading', () => {
      store.loadList();
      http.expectOne(BASE).error(new ProgressEvent('error'));

      expect(store.loading()).toBe(false);
      expect(store.error()?.kind).toBe('network');
    });

    it('clears a previous error when retried', () => {
      store.loadList();
      http.expectOne(BASE).flush('boom', { status: 500, statusText: 'Server Error' });
      expect(store.error()?.kind).toBe('server');

      store.loadList();
      expect(store.error()).toBeNull();
      http.expectOne(BASE).flush([]);
    });
  });

  describe('open', () => {
    it('selects the Creative once loaded', () => {
      store.open(5);
      expect(store.loading()).toBe(true);
      expect(store.selected()).toBeNull();

      http.expectOne(`${BASE}/5`).flush(creative(5, 4));

      expect(store.loading()).toBe(false);
      expect(store.selected()?.version).toBe(4);
    });

    it('reports an unknown id as not found', () => {
      store.open(99);
      http.expectOne(`${BASE}/99`).flush(null, { status: 404, statusText: 'Not Found' });

      expect(store.error()?.kind).toBe('not-found');
      expect(store.selected()).toBeNull();
    });

    it('drops the previously selected Creative while opening another', () => {
      store.open(1);
      http.expectOne(`${BASE}/1`).flush(creative(1));
      store.open(2);
      expect(store.selected()).toBeNull();
      http.expectOne(`${BASE}/2`).flush(creative(2));
    });
  });

  describe('create', () => {
    it('returns the saved Creative and selects it', async () => {
      const result = store.create(fields);
      expect(store.saving()).toBe(true);

      http.expectOne(BASE).flush(creative(7));

      expect((await result)?.id).toBe(7);
      expect(store.saving()).toBe(false);
      expect(store.selected()?.id).toBe(7);
    });

    it('turns a 400 into field messages keyed by camelCase field name', async () => {
      const result = store.create(fields);
      http.expectOne(BASE).flush(
        { title: 'Validation', errors: { Headline: ['Too long'], Cta: ['Required'] } },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(await result).toBeNull();
      expect(store.saving()).toBe(false);
      expect(store.error()?.kind).toBe('validation');
      expect(store.error()?.fieldErrors).toEqual({ headline: ['Too long'], cta: ['Required'] });
    });
  });

  describe('Conflict', () => {
    const conflictBody = (current: Creative) => ({ title: 'Conflict', status: 409, current });
    const conflictStatus = { status: 409, statusText: 'Conflict' };

    function openLoaded(): void {
      store.open(1);
      http.expectOne(`${BASE}/1`).flush({ ...creative(1, 1), headline: 'Mine-base' });
    }

    it('keeps the selected Creative and records the latest as a Conflict, not an error', async () => {
      openLoaded();
      const result = store.update(1, fields, 1);
      http
        .expectOne(`${BASE}/1`)
        .flush(conflictBody({ ...creative(1, 2), updatedBy: 'Grace' }), conflictStatus);

      expect(await result).toBeNull();
      expect(store.conflict()?.version).toBe(2);
      expect(store.conflict()?.updatedBy).toBe('Grace');
      expect(store.selected()?.version).toBe(1);
      expect(store.error()).toBeNull();
      expect(store.saving()).toBe(false);
    });

    it('treats a 409 without a latest Creative as a server error', async () => {
      openLoaded();
      const result = store.update(1, fields, 1);
      http.expectOne(`${BASE}/1`).flush({ title: 'Conflict' }, conflictStatus);

      expect(await result).toBeNull();
      expect(store.conflict()).toBeNull();
      expect(store.error()?.kind).toBe('server');
    });

    it('loadLatest adopts the latest Creative and clears the Conflict', async () => {
      openLoaded();
      const result = store.update(1, fields, 1);
      http.expectOne(`${BASE}/1`).flush(conflictBody(creative(1, 2)), conflictStatus);
      await result;

      store.loadLatest();

      expect(store.selected()?.version).toBe(2);
      expect(store.conflict()).toBeNull();
    });

    it('keepMyEdits saves only the changed fields over the latest values, using the latest Version', async () => {
      openLoaded();
      const first = store.update(1, fields, 1);
      const latest = { ...creative(1, 5), headline: 'Theirs', body: 'Their body' };
      http.expectOne(`${BASE}/1`).flush(conflictBody(latest), conflictStatus);
      await first;

      const draft = { ...fields, headline: 'Mine-base', cta: 'My cta' };
      const second = store.keepMyEdits(1, draft);

      const req = http.expectOne(`${BASE}/1`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toMatchObject({
        headline: 'Theirs',
        body: 'Their body',
        cta: 'My cta',
        name: 'n',
        version: 5,
      });
      req.flush({ ...latest, cta: 'My cta', version: 6 });

      expect((await second)?.version).toBe(6);
      expect(store.conflict()).toBeNull();
      expect(store.selected()?.version).toBe(6);
    });

    it('a second Conflict during keepMyEdits replaces the Conflict and still rebases from the original base', async () => {
      openLoaded();
      const first = store.update(1, fields, 1);
      http.expectOne(`${BASE}/1`).flush(conflictBody({ ...creative(1, 2), body: 'v2' }), conflictStatus);
      await first;

      const draft = { ...fields, headline: 'Mine-base', cta: 'My cta' };
      const second = store.keepMyEdits(1, draft);
      http
        .expectOne(`${BASE}/1`)
        .flush(conflictBody({ ...creative(1, 3), body: 'v3', updatedBy: 'Linus' }), conflictStatus);
      expect(await second).toBeNull();
      expect(store.conflict()?.version).toBe(3);
      expect(store.selected()?.version).toBe(1);

      const third = store.keepMyEdits(1, draft);
      const req = http.expectOne(`${BASE}/1`);
      expect(req.request.body).toMatchObject({ body: 'v3', cta: 'My cta', version: 3 });
      req.flush(creative(1, 4));
      await third;
    });

    it('opening another Creative clears a Conflict', async () => {
      openLoaded();
      const result = store.update(1, fields, 1);
      http.expectOne(`${BASE}/1`).flush(conflictBody(creative(1, 2)), conflictStatus);
      await result;

      store.open(2);
      expect(store.conflict()).toBeNull();
      http.expectOne(`${BASE}/2`).flush(creative(2));
    });
  });

  describe('update', () => {
    it('returns the saved Creative with its new Version', async () => {
      const result = store.update(1, fields, 2);
      http.expectOne(`${BASE}/1`).flush(creative(1, 3));

      expect((await result)?.version).toBe(3);
      expect(store.selected()?.version).toBe(3);
    });

    it('returns null and keeps the error when the save fails', async () => {
      const result = store.update(1, fields, 2);
      http.expectOne(`${BASE}/1`).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(await result).toBeNull();
      expect(store.error()?.kind).toBe('server');
    });
  });
});
