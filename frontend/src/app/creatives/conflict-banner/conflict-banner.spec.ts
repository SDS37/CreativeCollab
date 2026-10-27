import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Creative } from '../../core/creative.model';
import { ConflictBanner } from './conflict-banner';

const current: Creative = {
  id: '1',
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 3,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Bob',
};

describe('ConflictBanner', () => {
  let fixture: ComponentFixture<ConflictBanner>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ConflictBanner] }).compileComponents();
    fixture = TestBed.createComponent(ConflictBanner);
  });

  function button(name: string): HTMLButtonElement {
    return [...fixture.nativeElement.querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(name),
    ) as HTMLButtonElement;
  }

  it('names who saved and keeps the Draft on screen', async () => {
    fixture.componentRef.setInput('conflict', current);
    await fixture.whenStable();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Bob saved a newer version');
    expect(text).toContain('Your edits are still in the form.');
  });

  it('says another Collaborator when the latest save has no Display name', async () => {
    fixture.componentRef.setInput('conflict', { ...current, updatedBy: '' });
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Another Collaborator saved a newer version');
  });

  it('reports Load latest and Keep my edits', async () => {
    const choices: string[] = [];
    fixture.componentInstance.loadLatest.subscribe(() => choices.push('load'));
    fixture.componentInstance.keepMine.subscribe(() => choices.push('keep'));
    fixture.componentRef.setInput('conflict', current);
    await fixture.whenStable();

    button('Load latest').click();
    button('Keep my edits').click();

    expect(choices).toEqual(['load', 'keep']);
  });

  it('disables both actions while a save is in flight', async () => {
    fixture.componentRef.setInput('conflict', current);
    fixture.componentRef.setInput('saving', true);
    await fixture.whenStable();

    expect(button('Load latest').disabled).toBe(true);
    expect(button('Keep my edits').disabled).toBe(true);
  });
});
