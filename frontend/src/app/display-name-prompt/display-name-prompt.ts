import { Component, computed, input, output, signal } from '@angular/core';

/** Asks for a Display name. Reports the name; storing it is the caller's job. */
@Component({
  selector: 'app-display-name-prompt',
  template: `
    <form (submit)="submit($event)">
      <label for="display-name">Your display name</label>
      <input id="display-name" name="displayName" type="text" autocomplete="nickname"
             [value]="draft()" (input)="onInput($event)" />
      <button type="submit" [disabled]="!canSubmit()">Continue</button>
    </form>
  `,
  styles: `
    form { display: grid; gap: 0.5rem; max-width: 20rem; margin: 2rem auto; }
    input { font: inherit; padding: 0.5rem; }
  `,
})
export class DisplayNamePrompt {
  readonly current = input('');
  readonly confirmed = output<string>();

  protected readonly edited = signal<string | null>(null);
  protected readonly draft = computed(() => this.edited() ?? this.current());
  protected readonly canSubmit = computed(() => this.draft().trim() !== '');

  protected onInput(event: Event): void {
    this.edited.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.canSubmit()) {
      this.confirmed.emit(this.draft().trim());
    }
  }
}
