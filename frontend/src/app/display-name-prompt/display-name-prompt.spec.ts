import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DisplayNamePrompt } from './display-name-prompt';

describe('DisplayNamePrompt', () => {
  let fixture: ComponentFixture<DisplayNamePrompt>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DisplayNamePrompt] }).compileComponents();
    fixture = TestBed.createComponent(DisplayNamePrompt);
    await fixture.whenStable();
  });

  function input(): HTMLInputElement {
    return fixture.nativeElement.querySelector('input') as HTMLInputElement;
  }

  function submit(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  }

  function type(value: string): void {
    const field = input();
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  it('refuses a blank Display name', async () => {
    expect(submit().disabled).toBe(true);

    type('   ');
    await fixture.whenStable();

    expect(submit().disabled).toBe(true);
  });

  it('shows the current name and confirms a trimmed one', async () => {
    const names: string[] = [];
    fixture.componentInstance.confirmed.subscribe((name) => names.push(name));
    fixture.componentRef.setInput('current', 'Ada');
    await fixture.whenStable();

    expect(input().value).toBe('Ada');

    type('  Bob  ');
    submit().click();
    await fixture.whenStable();

    expect(names).toEqual(['Bob']);
  });
});
