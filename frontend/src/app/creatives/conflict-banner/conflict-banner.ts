import { Component, computed, input, output } from '@angular/core';
import { Creative } from '../../core/creative.model';

/** Tells the Collaborator a newer Version exists and reports which way they want to resolve it. */
@Component({
  selector: 'app-conflict-banner',
  template: `
    <section class="banner" role="alert">
      <p>{{ message() }}</p>
      <p class="hint">Your edits are still in the form.</p>
      <div class="actions">
        <button type="button" class="load-latest" [disabled]="saving()" (click)="loadLatest.emit()">
          Load latest
        </button>
        <button type="button" class="keep-mine" [disabled]="saving()" (click)="keepMine.emit()">
          Keep my edits
        </button>
      </div>
    </section>
  `,
  styles: `
    .banner { border: 1px solid #b26a00; background: #fff4e0; padding: 0.75rem 1rem; margin-bottom: 1rem; }
    .banner p { margin: 0 0 0.25rem; }
    .hint { color: #555; }
    .actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; }
  `,
})
export class ConflictBanner {
  readonly conflict = input.required<Creative>();
  readonly saving = input(false);

  readonly loadLatest = output();
  readonly keepMine = output();

  protected readonly message = computed(() => {
    const who = this.conflict().updatedBy || 'Another Collaborator';
    return `${who} saved a newer version`;
  });
}
