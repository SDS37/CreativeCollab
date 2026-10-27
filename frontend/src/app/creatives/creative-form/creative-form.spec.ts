import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Creative, CreativeFields } from '../../core/creative.model';
import { CreativeForm } from './creative-form';

const creative: Creative = {
  id: '1',
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Ada',
};

describe('CreativeForm', () => {
  let fixture: ComponentFixture<CreativeForm>;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CreativeForm] }).compileComponents();
    fixture = TestBed.createComponent(CreativeForm);
    el = fixture.nativeElement;
  });

  function input(name: string): HTMLInputElement | HTMLTextAreaElement {
    return el.querySelector(`[name="${name}"]`) as HTMLInputElement;
  }

  function type(name: string, value: string): void {
    const field = input(name);
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  function saveButton(): HTMLButtonElement {
    return el.querySelector('button[type="submit"]') as HTMLButtonElement;
  }

  it('shows the given Creative in its fields', async () => {
    fixture.componentRef.setInput('creative', creative);
    await fixture.whenStable();

    expect(input('headline').value).toBe('Fresh');
    expect(input('imageUrl').value).toBe('');
  });

  it('emits the current Draft as the Collaborator types', async () => {
    const drafts: CreativeFields[] = [];
    fixture.componentInstance.draftChange.subscribe((d) => drafts.push(d));
    fixture.componentRef.setInput('creative', creative);
    await fixture.whenStable();

    type('headline', 'Fresher');

    expect(drafts.at(-1)).toEqual({
      name: 'Spring',
      headline: 'Fresher',
      body: 'Buy now',
      cta: 'Shop',
      imageUrl: '',
    });
  });

  it('emits the Draft on submit', async () => {
    const saved: CreativeFields[] = [];
    fixture.componentInstance.saved.subscribe((d) => saved.push(d));
    fixture.componentRef.setInput('creative', creative);
    await fixture.whenStable();

    type('cta', 'Buy');
    saveButton().click();

    expect(saved).toHaveLength(1);
    expect(saved[0].cta).toBe('Buy');
  });

  it('cannot be saved while saving or when a required field is empty', async () => {
    fixture.componentRef.setInput('creative', creative);
    await fixture.whenStable();
    expect(saveButton().disabled).toBe(false);

    type('headline', '');
    await fixture.whenStable();
    expect(saveButton().disabled).toBe(true);

    type('headline', 'Back');
    fixture.componentRef.setInput('saving', true);
    await fixture.whenStable();
    expect(saveButton().disabled).toBe(true);
  });

  it('shows the server message for a rejected field', async () => {
    fixture.componentRef.setInput('creative', creative);
    fixture.componentRef.setInput('fieldErrors', { headline: ['Headline is too long'] });
    await fixture.whenStable();

    expect(el.textContent).toContain('Headline is too long');
  });

  it('keeps the Draft when the same Creative is passed again, and resets for a different one', async () => {
    fixture.componentRef.setInput('creative', creative);
    await fixture.whenStable();
    type('headline', 'Typing');

    fixture.componentRef.setInput('fieldErrors', {});
    await fixture.whenStable();
    expect(input('headline').value).toBe('Typing');

    fixture.componentRef.setInput('creative', { ...creative, version: 3, headline: 'Server' });
    await fixture.whenStable();
    expect(input('headline').value).toBe('Server');
  });
});
