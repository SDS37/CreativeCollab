import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CreativeSignalStore } from '../../core/creative-signal-store';
import { CreativeHubService } from '../../core/creative-hub';
import { CreativeFields, sameFields, toFields } from '../../core/creative.model';
import { Session } from '../../core/session';
import { ConflictBanner } from '../conflict-banner/conflict-banner';
import { CreativeForm } from '../creative-form/creative-form';
import { PresenceBar } from '../presence-bar/presence-bar';
import { RemoteUpdateNotice } from '../remote-update-notice/remote-update-notice';

@Component({
  selector: 'app-creative-edit-page',
  imports: [ConflictBanner, CreativeForm, PresenceBar, RemoteUpdateNotice, RouterLink],
  templateUrl: './creative-edit-page.html',
})
export class CreativeEditPage {
  protected readonly store = inject(CreativeSignalStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly hub = inject(CreativeHubService);
  private readonly session = inject(Session);

  /** Viewers can open a Creative and follow it live, but the form is read-only. The API enforces this too. */
  protected readonly readOnly = computed(() => !this.session.canEdit());

  /** The id in the route, or null on the create page. */
  private readonly id = signal<string | null>(null);

  /** The Collaborator's unsaved edits, as last reported by the form. */
  private readonly draft = signal<CreativeFields | null>(null);

  /** True when the Draft differs from the Creative the form was loaded with. */
  private readonly isDirty = computed(() => {
    const draft = this.draft();
    const selected = this.store.selected();
    return draft !== null && selected !== null && !sameFields(draft, toFields(selected));
  });

  protected readonly heading = computed(() =>
    this.id() === null ? 'New Creative' : this.readOnly() ? 'View Creative' : 'Edit Creative',
  );

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

  /** Sessions that currently have this Creative open. The template does not reach into the hub. */
  protected readonly openSessions = computed(() => this.hub.presence());
  protected readonly liveUpdatesUnavailable = computed(() => this.hub.unavailable());

  private readonly followRoute = this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
    const id = params.get('id');
    this.id.set(id);
    this.load();
    if (id === null) {
      void this.hub.leave();
    } else {
      void this.hub.join(id);
    }
  });

  private readonly followUpdates = this.hub.updated
    .pipe(takeUntilDestroyed())
    .subscribe((creative) => this.store.receiveUpdate(creative, this.isDirty()));

  private readonly disposeHub = inject(DestroyRef).onDestroy(() => {
    void this.hub.dispose();
  });

  protected load(): void {
    const id = this.id();
    if (id === null) {
      this.store.startNew();
    } else {
      this.store.open(id);
    }
  }

  protected onDraftChange(draft: CreativeFields): void {
    this.draft.set(draft);
  }

  protected loadLatest(): void {
    this.store.loadLatest();
  }

  protected loadRemote(): void {
    this.store.loadRemote();
  }

  protected async keepMyEdits(): Promise<void> {
    const id = this.id();
    const draft = this.draft();
    if (id !== null && draft) {
      await this.store.keepMyEdits(id, draft);
    }
  }

  protected async save(fields: CreativeFields): Promise<void> {
    if (this.readOnly()) {
      return;
    }
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
