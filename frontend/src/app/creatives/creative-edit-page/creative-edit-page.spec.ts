import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { Creative } from '../../core/creative.model';
import { DisplayName } from '../../core/display-name';
import { CreativeEditPage } from './creative-edit-page';

const BASE = 'http://localhost:5114/api/creatives';

const creative: Creative = {
  id: 3,
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Ada',
};

describe('CreativeEditPage', () => {
  let http: HttpTestingController;
  let router: Router;

  function setup(id: string | null): ComponentFixture<CreativeEditPage> {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap(id ? { id } : {})) } },
      ],
    });
    TestBed.inject(DisplayName).set('Ada');
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    return TestBed.createComponent(CreativeEditPage);
  }

  function text(fixture: ComponentFixture<unknown>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function field(fixture: ComponentFixture<unknown>, name: string): HTMLInputElement {
    return (fixture.nativeElement as HTMLElement).querySelector(`[name="${name}"]`) as HTMLInputElement;
  }

  function type(fixture: ComponentFixture<unknown>, name: string, value: string): void {
    const el = field(fixture, name);
    el.value = value;
    el.dispatchEvent(new Event('input'));
  }

  function submit(fixture: ComponentFixture<unknown>): void {
    ((fixture.nativeElement as HTMLElement).querySelector('button[type="submit"]') as HTMLButtonElement).click();
  }

  afterEach(() => http.verify());

  it('shows a loading state, then the Creative', async () => {
    const fixture = setup('3');
    await fixture.whenStable();
    expect(text(fixture)).toContain('Loading');

    http.expectOne(`${BASE}/3`).flush(creative);
    await fixture.whenStable();

    expect(text(fixture)).not.toContain('Loading');
    expect(field(fixture, 'headline').value).toBe('Fresh');
  });

  it('shows a not-found message for an unknown Creative', async () => {
    const fixture = setup('99');
    await fixture.whenStable();
    http.expectOne(`${BASE}/99`).flush(null, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('does not exist');
    expect(field(fixture, 'headline')).toBeNull();
  });

  it('offers a retry when loading fails, and retries the same Creative', async () => {
    const fixture = setup('3');
    await fixture.whenStable();
    http.expectOne(`${BASE}/3`).error(new ProgressEvent('error'));
    await fixture.whenStable();
    expect(text(fixture)).toContain('Cannot reach the server');

    ((fixture.nativeElement as HTMLElement).querySelector('button.retry') as HTMLButtonElement).click();
    http.expectOne(`${BASE}/3`).flush(creative);
  });

  it('saves edits with the loaded Version and shows the saved Version', async () => {
    const fixture = setup('3');
    await fixture.whenStable();
    http.expectOne(`${BASE}/3`).flush(creative);
    await fixture.whenStable();

    type(fixture, 'headline', 'Fresher');
    submit(fixture);

    const req = http.expectOne(`${BASE}/3`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.version).toBe(2);
    expect(req.request.body.headline).toBe('Fresher');
    expect(req.request.body.displayName).toBe('Ada');
    req.flush({ ...creative, headline: 'Fresher', version: 3 });
    await fixture.whenStable();

    expect(text(fixture)).toContain('Version 3');
  });

  it('keeps the Draft and shows field messages when the server rejects it', async () => {
    const fixture = setup('3');
    await fixture.whenStable();
    http.expectOne(`${BASE}/3`).flush(creative);
    await fixture.whenStable();

    type(fixture, 'headline', 'Fresher');
    submit(fixture);
    http
      .expectOne(`${BASE}/3`)
      .flush({ errors: { Headline: ['Headline is too long'] } }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('Headline is too long');
    expect(field(fixture, 'headline').value).toBe('Fresher');
  });

  it('creates a new Creative and opens it', async () => {
    const fixture = setup(null);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await fixture.whenStable();

    type(fixture, 'name', 'New');
    type(fixture, 'headline', 'Hello');
    type(fixture, 'body', 'Body');
    type(fixture, 'cta', 'Go');
    await fixture.whenStable();
    submit(fixture);

    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('POST');
    req.flush({ ...creative, id: 8, version: 1 });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/creatives', 8], { replaceUrl: true });
  });
});
