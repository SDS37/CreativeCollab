import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Creative } from '../../core/creative.model';
import { CreativeList } from './creative-list';

function creative(overrides: Partial<Creative> = {}): Creative {
  return {
    id: '41f18372-7214-4fa9-8968-529416310b43',
    name: 'Spring sale banner',
    headline: 'Spring into savings',
    body: 'Up to 40% off.',
    cta: 'Shop now',
    imageUrl: null,
    version: 1,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: 'Ada',
    ...overrides,
  };
}

describe('CreativeList', () => {
  let fixture: ComponentFixture<CreativeList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CreativeList] }).compileComponents();
    fixture = TestBed.createComponent(CreativeList);
  });

  it('shows each Creative and reports the one the Collaborator opens', async () => {
    const opened: string[] = [];
    fixture.componentInstance.opened.subscribe((id) => opened.push(id));
    fixture.componentRef.setInput('creatives', [creative(), creative({ id: 'other', name: 'Newsletter signup' })]);
    await fixture.whenStable();

    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    expect(buttons[0].textContent).toContain('Spring sale banner');
    expect(buttons[0].textContent).toContain('Spring into savings');
    expect(buttons[0].textContent).toContain('Ada');

    buttons[1].click();

    expect(opened).toEqual(['other']);
  });

  it('does not invent a Display name for a seeded Creative', async () => {
    fixture.componentRef.setInput('creatives', [creative({ updatedBy: '' })]);
    await fixture.whenStable();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Version 1');
    expect(text).not.toContain('·');
  });
});
