import { Component, computed, input } from '@angular/core';

/** Lists the Collaborators who have the Creative open, one entry per open session. */
@Component({
  selector: 'app-presence-bar',
  template: `
    @if (label()) {
      <p class="presence" aria-live="polite">{{ label() }}</p>
    }
  `,
  styles: `
    .presence { color: #555; font-size: 0.9rem; margin: 0 0 0.75rem; }
  `,
})
export class PresenceBar {
  readonly names = input.required<string[]>();

  protected readonly label = computed(() => {
    const names = this.names();
    return names.length === 0 ? '' : `Open now: ${names.join(', ')}`;
  });
}
