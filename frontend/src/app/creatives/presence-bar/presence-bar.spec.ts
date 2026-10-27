import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PresenceBar } from './presence-bar';

describe('PresenceBar', () => {
  let fixture: ComponentFixture<PresenceBar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PresenceBar] }).compileComponents();
    fixture = TestBed.createComponent(PresenceBar);
  });

  it('renders nothing when nobody has the Creative open', async () => {
    fixture.componentRef.setInput('names', []);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('lists one Display name per open session, including a repeated name', async () => {
    fixture.componentRef.setInput('names', ['Ada', 'Ada']);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Open now: Ada, Ada');
  });
});
