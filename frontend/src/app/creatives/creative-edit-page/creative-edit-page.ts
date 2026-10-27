import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CreativeStore } from '../../core/creative-store';
import { CreativeFields } from '../../core/creative.model';
import { CreativeForm } from '../creative-form/creative-form';

@Component({
  selector: 'app-creative-edit-page',
  imports: [CreativeForm, RouterLink],
  templateUrl: './creative-edit-page.html',
})
export class CreativeEditPage {
  protected readonly store = inject(CreativeStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  /** The id in the route, or null on the create page. */
  private readonly id = signal<number | null>(null);

  protected readonly heading = computed(() => (this.id() === null ? 'New Creative' : 'Edit Creative'));

  protected readonly summary = computed(() => {
    const selected = this.store.selected();
    if (!selected) {
      return '';
    }
    return selected.updatedBy
      ? `Version ${selected.version}, last saved by ${selected.updatedBy}`
      : `Version ${selected.version}`;
  });

  /** The form is hidden while the Creative loads or when it could not be loaded. */
  protected readonly loadFailed = computed(() => this.id() !== null && this.store.error() !== null && this.store.selected() === null);
  protected readonly canRetry = computed(() => this.loadFailed() && this.store.error()?.kind !== 'not-found');
  protected readonly showForm = computed(() => !this.store.loading() && !this.loadFailed());
  protected readonly fieldErrors = computed(() => this.store.error()?.fieldErrors ?? {});

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const raw = params.get('id');
      const id = raw === null ? null : Number(raw);
      this.id.set(id);
      this.load();
    });
  }

  protected load(): void {
    const id = this.id();
    if (id === null) {
      this.store.startNew();
    } else {
      this.store.open(id);
    }
  }

  protected async save(fields: CreativeFields): Promise<void> {
    const id = this.id();
    if (id === null) {
      const created = await this.store.create(fields);
      if (created) {
        await this.router.navigate(['/creatives', created.id], { replaceUrl: true });
      }
      return;
    }
    const selected = this.store.selected();
    if (selected) {
      await this.store.update(id, fields, selected.version);
    }
  }
}
