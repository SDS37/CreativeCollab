import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { DisplayName } from './core/display-name';

describe('App', () => {
  let fixture: ComponentFixture<App>;

  const el = () => fixture.nativeElement as HTMLElement;

  function create(): void {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(App);
  }

  beforeEach(() => localStorage.clear());

  it('asks for a Display name before anything else', async () => {
    create();
    await fixture.whenStable();

    expect(el().querySelector('app-display-name-prompt')).not.toBeNull();
    expect(el().querySelector('router-outlet')).toBeNull();
  });

  it('remembers the name and lets the Collaborator in', async () => {
    create();
    await fixture.whenStable();

    const input = el().querySelector('input') as HTMLInputElement;
    input.value = 'Ada';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    (el().querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(TestBed.inject(DisplayName).value()).toBe('Ada');
    expect(el().querySelector('router-outlet')).not.toBeNull();
    expect(el().textContent).toContain('Ada');
  });

  it('lets the Collaborator change the name later', async () => {
    localStorage.setItem('creativecollab.displayName', 'Ada');
    create();
    await fixture.whenStable();
    expect(el().querySelector('app-display-name-prompt')).toBeNull();

    (el().querySelector('button.change-name') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(el().querySelector('app-display-name-prompt')).not.toBeNull();
  });
});
