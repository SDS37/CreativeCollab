import { Component, computed, input, output } from '@angular/core';
import { Creative } from '../../core/creative.model';

/** A non-blocking notice that someone saved while the Collaborator has a Draft. */
@Component({
  selector: 'app-remote-update-notice',
  template: `
    <p class="notice" role="status">
      {{ message() }}
      <button type="button" class="load-remote" (click)="loadLatest.emit()">Load latest</button>
    </p>
  `,
  styles: `
    .notice { border: 1px solid #1a5fb4; background: #e8f1fc; padding: 0.5rem 0.75rem; margin-bottom: 1rem; }
  `,
})
export class RemoteUpdateNotice {
  readonly update = input.required<Creative>();
  readonly loadLatest = output();

  protected readonly message = computed(() => {
    const who = this.update().updatedBy || 'Another Collaborator';
    return `${who} updated this Creative (Version ${this.update().version}). Load latest? Your edits stay until you do.`;
  });
}
