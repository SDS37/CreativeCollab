import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Creative } from '../../core/creative.model';
import { CreativeListPage } from './creative-list-page';

const BASE = 'http://localhost:5114/api/creatives';

function creative(id: number, name: string): Creative {
  return {
    id: String(id),
    name,
    headline: `Headline ${id}`,
    body: 'b',
    cta: 'c',
    imageUrl: null,
    version: id,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: 'Ada',
  };
}

describe('CreativeListPage', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<CreativeListPage>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CreativeListPage);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('shows loading, then the Creatives in the order the API returned them', async () => {
    expect(text()).toContain('Loading');

    http.expectOne(BASE).flush([creative(2, 'Second'), creative(1, 'First')]);
    await fixture.whenStable();

    expect(text()).not.toContain('Loading');
    const names = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.creative-name')).map(
      (e) => e.textContent?.trim(),
    );
    expect(names).toEqual(['Second', 'First']);
  });

  it('says so when there are no Creatives', async () => {
    http.expectOne(BASE).flush([]);
    await fixture.whenStable();
    expect(text()).toContain('No Creatives yet');
  });

  it('shows an error with a retry that reloads the list', async () => {
    http.expectOne(BASE).error(new ProgressEvent('error'));
    await fixture.whenStable();
    expect(text()).toContain('Cannot reach the server');

    ((fixture.nativeElement as HTMLElement).querySelector('button.retry') as HTMLButtonElement).click();
    http.expectOne(BASE).flush([]);
  });

  it('opens a Creative when chosen', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    http.expectOne(BASE).flush([creative(4, 'Four')]);
    await fixture.whenStable();

    ((fixture.nativeElement as HTMLElement).querySelector('button.open') as HTMLButtonElement).click();

    expect(navigate).toHaveBeenCalledWith(['/creatives', '4']);
  });
});
