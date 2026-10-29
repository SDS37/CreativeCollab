import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Creative, CreativeFields } from './creative.model';
import { CreativesApi } from './creatives-api';

const BASE = 'http://localhost:5114/api/creatives';

const fields: CreativeFields = {
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: '',
};

const creative: Creative = {
  id: '1',
  ...fields,
  imageUrl: null,
  version: 1,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Ada',
};

describe('CreativesApi', () => {
  let api: CreativesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CreativesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists Creatives', () => {
    let result: Creative[] | undefined;
    api.list().subscribe((r) => (result = r));

    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('GET');
    req.flush([creative]);
    expect(result).toEqual([creative]);
  });

  it('gets one Creative by id', () => {
    let result: Creative | undefined;
    api.get('1').subscribe((r) => (result = r));

    const req = http.expectOne(`${BASE}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(creative);
    expect(result).toEqual(creative);
  });

  it('creates without any name and omits an empty image URL', () => {
    api.create(fields).subscribe();

    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Spring',
      headline: 'Fresh',
      body: 'Buy now',
      cta: 'Shop',
    });
    req.flush(creative);
  });

  it('updates with the loaded Version and no name, sending a present image URL', () => {
    api.update('1', { ...fields, imageUrl: 'https://x.test/a.png' }, 3).subscribe();

    const req = http.expectOne(`${BASE}/1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      name: 'Spring',
      headline: 'Fresh',
      body: 'Buy now',
      cta: 'Shop',
      imageUrl: 'https://x.test/a.png',
      version: 3,
    });
    req.flush(creative);
  });

  it('rejects a body that is not a Creative', () => {
    let failed = false;
    api.list().subscribe({ error: () => (failed = true) });

    http.expectOne(BASE).flush([{ id: '1' }]);

    expect(failed).toBe(true);
  });
});
