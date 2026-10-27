import { HttpErrorResponse } from '@angular/common/http';
import { inject, Service, signal } from '@angular/core';
import { firstValueFrom, Observable, Subscription } from 'rxjs';
import { Creative, CreativeFields } from './creative.model';
import { DisplayName } from './display-name';
import { CreativesApi } from './creatives-api';
import { rebase } from './rebase';

export type ApiErrorKind = 'network' | 'not-found' | 'validation' | 'server';

export interface ApiError {
  kind: ApiErrorKind;
  message: string;
  /** Field messages from a 400, keyed by camelCase field name. */
  fieldErrors: Record<string, string[]>;
}

/** Signals state for the Creatives screens. Components never see raw HTTP errors. */
@Service()
export class CreativeStore {
  private readonly api = inject(CreativesApi);
  private readonly displayName = inject(DisplayName);

  private readonly creativesState = signal<Creative[]>([]);
  private readonly selectedState = signal<Creative | null>(null);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<ApiError | null>(null);
  private readonly conflictState = signal<Creative | null>(null);
  private readonly remoteUpdateState = signal<Creative | null>(null);

  readonly creatives = this.creativesState.asReadonly();
  readonly selected = this.selectedState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly saving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  /** The latest Creative, set when a save was rejected as a Conflict. Not an error. */
  readonly conflict = this.conflictState.asReadonly();
  /** A newer saved copy heard about while the Collaborator has a Draft. Not yet shown in the form. */
  readonly remoteUpdate = this.remoteUpdateState.asReadonly();

  private inFlight: Subscription | undefined;

  loadList(): void {
    this.selectedState.set(null);
    this.conflictState.set(null);
    this.remoteUpdateState.set(null);
    this.load(this.api.list(), (creatives) => this.creativesState.set(creatives));
  }

  open(id: string): void {
    this.selectedState.set(null);
    this.conflictState.set(null);
    this.remoteUpdateState.set(null);
    this.load(this.api.get(id), (creative) => this.selectedState.set(creative));
  }

  /** Prepares an empty edit page: nothing selected, nothing loading, no stale error. */
  startNew(): void {
    this.inFlight?.unsubscribe();
    this.selectedState.set(null);
    this.conflictState.set(null);
    this.remoteUpdateState.set(null);
    this.loadingState.set(false);
    this.errorState.set(null);
  }

  create(fields: CreativeFields): Promise<Creative | null> {
    return this.save(this.api.create(fields));
  }

  update(id: string, fields: CreativeFields, version: number): Promise<Creative | null> {
    return this.save(this.api.update(id, fields, version));
  }

  /** Discards the Draft: the latest Creative from the Conflict becomes the selected one. */
  loadLatest(): void {
    const latest = this.conflictState();
    if (latest) {
      this.selectedState.set(latest);
      this.conflictState.set(null);
    }
  }

  /** Rebase: re-applies the Draft's changed fields onto the latest Creative and saves again. */
  keepMyEdits(id: string, draft: CreativeFields): Promise<Creative | null> {
    const loaded = this.selectedState();
    const latest = this.conflictState();
    if (!loaded || !latest) {
      return Promise.resolve(null);
    }
    return this.update(id, rebase(loaded, draft, latest), latest.version);
  }

  /**
   * Handles a Creative saved by someone (the hub also echoes my own saves back).
   * A Draft is never overwritten: the form only changes when it is clean.
   */
  receiveUpdate(incoming: Creative, formIsDirty: boolean): void {
    const selected = this.selectedState();
    if (!selected || incoming.id !== selected.id) {
      return;
    }
    const conflict = this.conflictState();
    const known = Math.max(selected.version, this.remoteUpdateState()?.version ?? 0, conflict?.version ?? 0);
    if (incoming.version <= known) {
      return;
    }
    // The echo of my own save can arrive before the HTTP response that carries the same Creative.
    if (this.savingState() && incoming.updatedBy === this.displayName.value()) {
      return;
    }
    if (conflict) {
      this.conflictState.set(incoming);
    } else if (formIsDirty) {
      this.remoteUpdateState.set(incoming);
    } else {
      this.selectedState.set(incoming);
      this.remoteUpdateState.set(null);
    }
  }

  /** Adopts the newest remote update, discarding the Draft. */
  loadRemote(): void {
    const latest = this.remoteUpdateState();
    if (latest) {
      this.selectedState.set(latest);
      this.remoteUpdateState.set(null);
    }
  }

  private load<T>(request: Observable<T>, onResult: (result: T) => void): void {
    this.inFlight?.unsubscribe();
    this.loadingState.set(true);
    this.errorState.set(null);
    this.inFlight = request.subscribe({
      next: (result) => {
        onResult(result);
        this.loadingState.set(false);
      },
      error: (error: unknown) => {
        this.errorState.set(toApiError(error));
        this.loadingState.set(false);
      },
    });
  }

  private async save(request: Observable<Creative>): Promise<Creative | null> {
    this.savingState.set(true);
    this.errorState.set(null);
    try {
      const saved = await firstValueFrom(request);
      const current = this.selectedState();
      // A live update may have been adopted while this save was in flight; never go backwards.
      if (!current || saved.version >= current.version) {
        this.selectedState.set(saved);
      }
      this.conflictState.set(null);
      const remote = this.remoteUpdateState();
      if (remote && remote.version <= saved.version) {
        this.remoteUpdateState.set(null);
      }
      return saved;
    } catch (error) {
      const latest = conflictOf(error);
      if (latest) {
        this.conflictState.set(latest);
        this.remoteUpdateState.set(null);
      } else {
        this.errorState.set(toApiError(error));
      }
      return null;
    } finally {
      this.savingState.set(false);
    }
  }
}

/** The latest Creative carried by a 409, or null when the error is not a usable Conflict. */
function conflictOf(error: unknown): Creative | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
    return null;
  }
  const current = (error.error as { current?: Creative } | null)?.current;
  return current ?? null;
}

function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { kind: 'server', message: 'Something went wrong.', fieldErrors: {} };
  }
  switch (error.status) {
    case 0:
      return {
        kind: 'network',
        message: 'Cannot reach the server. Check that the API is running.',
        fieldErrors: {},
      };
    case 404:
      return { kind: 'not-found', message: 'That Creative does not exist.', fieldErrors: {} };
    case 400:
      return {
        kind: 'validation',
        message: 'Some fields need attention.',
        fieldErrors: toFieldErrors(error.error),
      };
    default:
      return { kind: 'server', message: 'The server could not complete the request.', fieldErrors: {} };
  }
}

function toFieldErrors(body: unknown): Record<string, string[]> {
  const errors = (body as { errors?: Record<string, string[]> } | null)?.errors ?? {};
  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => [key.charAt(0).toLowerCase() + key.slice(1), messages]),
  );
}
