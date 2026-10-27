import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { signal } from '@angular/core';
import { Creative } from '../../core/creative.model';
import { CreativeHubService } from '../../core/creative-hub';
import { DisplayName } from '../../core/display-name';
import { CreativeEditPage } from './creative-edit-page';

const BASE = 'http://localhost:5114/api/creatives';

const creative: Creative = {
  id: '3',
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Ada',
};

class FakeHub {
  readonly presence = signal<string[]>([]);
  readonly connected = signal(false);
  readonly unavailable = signal(false);
  readonly updates = new Subject<Creative>();
  readonly updated = this.updates.asObservable();
  readonly join = vi.fn(async (_id: number, _name: string) => {});
  readonly leave = vi.fn(async () => {});
  readonly dispose = vi.fn(async () => {});
}

describe('CreativeEditPage', () => {
  let http: HttpTestingController;
  let router: Router;
  let hub: FakeHub;

  function setup(id: string | null): ComponentFixture<CreativeEditPage> {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: CreativeHubService, useFactory: () => (hub = new FakeHub()) },
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

  it('opens a Creative by the GUID id the API issues and joins its group with it', async () => {
    const guid = '41f18372-7214-4fa9-8968-529416310b43';
    const fixture = setup(guid);
    await fixture.whenStable();

    http.expectOne(`${BASE}/${guid}`).flush({ ...creative, id: guid });
    await fixture.whenStable();

    expect(field(fixture, 'headline').value).toBe('Fresh');
    expect(hub.join).toHaveBeenCalledWith(guid, 'Ada');
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

  describe('Conflict', () => {
    const conflictStatus = { status: 409, statusText: 'Conflict' };
    const latest = { ...creative, version: 3, headline: 'Grace headline', body: 'Grace body', updatedBy: 'Grace' };

    async function conflicted(): Promise<ComponentFixture<CreativeEditPage>> {
      const fixture = setup('3');
      await fixture.whenStable();
      http.expectOne(`${BASE}/3`).flush(creative);
      await fixture.whenStable();

      type(fixture, 'headline', 'My headline');
      type(fixture, 'cta', 'My cta');
      submit(fixture);
      http.expectOne(`${BASE}/3`).flush({ current: latest }, conflictStatus);
      await fixture.whenStable();
      return fixture;
    }

    function button(fixture: ComponentFixture<unknown>, selector: string): HTMLButtonElement {
      return (fixture.nativeElement as HTMLElement).querySelector(selector) as HTMLButtonElement;
    }

    it('keeps the Draft on screen and names who saved', async () => {
      const fixture = await conflicted();

      expect(field(fixture, 'headline').value).toBe('My headline');
      expect(field(fixture, 'cta').value).toBe('My cta');
      expect(text(fixture)).toContain('Grace saved a newer version');
    });

    it('Load latest replaces the form with the server copy and hides the banner', async () => {
      const fixture = await conflicted();

      button(fixture, 'button.load-latest').click();
      await fixture.whenStable();

      expect(field(fixture, 'headline').value).toBe('Grace headline');
      expect(field(fixture, 'cta').value).toBe('Shop');
      expect(text(fixture)).not.toContain('saved a newer version');
      expect(text(fixture)).toContain('Version 3');
    });

    it('Keep my edits saves only my changed fields onto the latest Version', async () => {
      const fixture = await conflicted();

      button(fixture, 'button.keep-mine').click();

      const req = http.expectOne(`${BASE}/3`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toMatchObject({
        headline: 'My headline',
        cta: 'My cta',
        body: 'Grace body',
        version: 3,
      });
      req.flush({ ...latest, headline: 'My headline', cta: 'My cta', version: 4 });
      await fixture.whenStable();

      expect(field(fixture, 'body').value).toBe('Grace body');
      expect(text(fixture)).not.toContain('saved a newer version');
      expect(text(fixture)).toContain('Version 4');
    });

    it('says Another Collaborator when the newer Version has no recorded name', async () => {
      const fixture = setup('3');
      await fixture.whenStable();
      http.expectOne(`${BASE}/3`).flush(creative);
      await fixture.whenStable();
      type(fixture, 'cta', 'x');
      submit(fixture);
      http.expectOne(`${BASE}/3`).flush({ current: { ...latest, updatedBy: '' } }, conflictStatus);
      await fixture.whenStable();

      expect(text(fixture)).toContain('Another Collaborator saved a newer version');
    });
  });

  describe('live updates and Presence', () => {
    const remote = { ...creative, version: 3, headline: 'Grace headline', updatedBy: 'Grace' };

    async function opened(): Promise<ComponentFixture<CreativeEditPage>> {
      const fixture = setup('3');
      await fixture.whenStable();
      http.expectOne(`${BASE}/3`).flush(creative);
      await fixture.whenStable();
      return fixture;
    }

    it('joins the Creative group with the Display name', async () => {
      await opened();
      expect(hub.join).toHaveBeenCalledWith('3', 'Ada');
    });

    it('does not join anything on the create page', async () => {
      const fixture = setup(null);
      await fixture.whenStable();
      expect(hub.join).not.toHaveBeenCalled();
    });

    it('shows who has the Creative open as Presence changes', async () => {
      const fixture = await opened();

      hub.presence.set(['Ada', 'Grace']);
      await fixture.whenStable();
      expect(text(fixture)).toContain('Ada, Grace');

      hub.presence.set(['Ada']);
      await fixture.whenStable();
      expect(text(fixture)).not.toContain('Grace');
    });

    it('says live updates are unavailable when the connection failed', async () => {
      const fixture = await opened();
      hub.unavailable.set(true);
      await fixture.whenStable();
      expect(text(fixture)).toContain('Live updates are unavailable');
    });

    it('updates a clean form silently', async () => {
      const fixture = await opened();

      hub.updates.next(remote);
      await fixture.whenStable();

      expect(field(fixture, 'headline').value).toBe('Grace headline');
      expect(text(fixture)).toContain('Version 3');
      expect(text(fixture)).not.toContain('Grace updated');
    });

    it('never overwrites a dirty Draft and shows a notice instead', async () => {
      const fixture = await opened();
      type(fixture, 'headline', 'Typing');
      await fixture.whenStable();

      hub.updates.next(remote);
      await fixture.whenStable();

      expect(field(fixture, 'headline').value).toBe('Typing');
      expect(text(fixture)).toContain('Grace updated this Creative');
      expect(text(fixture)).toContain('Version 2');
    });

    it('treats a form edited back to its saved values as clean', async () => {
      const fixture = await opened();
      type(fixture, 'headline', 'Typing');
      type(fixture, 'headline', 'Fresh');
      await fixture.whenStable();

      hub.updates.next(remote);
      await fixture.whenStable();

      expect(field(fixture, 'headline').value).toBe('Grace headline');
    });

    it('Load latest from the notice replaces the form and hides the notice', async () => {
      const fixture = await opened();
      type(fixture, 'headline', 'Typing');
      await fixture.whenStable();
      hub.updates.next(remote);
      await fixture.whenStable();

      ((fixture.nativeElement as HTMLElement).querySelector('button.load-remote') as HTMLButtonElement).click();
      await fixture.whenStable();

      expect(field(fixture, 'headline').value).toBe('Grace headline');
      expect(text(fixture)).not.toContain('Grace updated this Creative');
    });

    it('keeps the notice on the newest Version when more saves arrive', async () => {
      const fixture = await opened();
      type(fixture, 'headline', 'Typing');
      await fixture.whenStable();

      hub.updates.next(remote);
      hub.updates.next({ ...remote, version: 4, updatedBy: 'Linus' });
      await fixture.whenStable();

      expect(text(fixture)).toContain('Linus updated this Creative');
      expect(text(fixture)).not.toContain('Grace updated this Creative');
    });

    it('ignores an echo whose Version already matches', async () => {
      const fixture = await opened();
      type(fixture, 'headline', 'Typing');
      await fixture.whenStable();

      hub.updates.next(creative);
      await fixture.whenStable();

      expect(field(fixture, 'headline').value).toBe('Typing');
      expect(text(fixture)).not.toContain('updated this Creative');
    });

    it('leaves the group and disposes the connection when destroyed', async () => {
      const fixture = await opened();

      fixture.destroy();

      expect(hub.dispose).toHaveBeenCalledTimes(1);
    });

    it('stops listening to updates once destroyed', async () => {
      const fixture = await opened();
      fixture.destroy();
      expect(hub.updates.observed).toBe(false);
    });
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
    req.flush({ ...creative, id: '8', version: 1 });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/creatives', '8'], { replaceUrl: true });
  });
});
