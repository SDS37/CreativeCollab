import { computed, Service, signal } from '@angular/core';

const STORAGE_KEY = 'creativecollab.displayName';

/**
 * The Collaborator's self-declared Display name. Trusted and unauthenticated,
 * kept in localStorage so it survives a reload.
 */
@Service()
export class DisplayName {
  private readonly name = signal(localStorage.getItem(STORAGE_KEY) ?? '');

  readonly value = this.name.asReadonly();
  readonly isSet = computed(() => this.name() !== '');

  set(name: string): void {
    const trimmed = name.trim();
    if (trimmed === '') {
      return;
    }
    localStorage.setItem(STORAGE_KEY, trimmed);
    this.name.set(trimmed);
  }
}
