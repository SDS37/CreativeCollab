import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom, Observable, Subscription } from 'rxjs';
import { Creative, creativeSchema, CreativeFields } from './creative.model';
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

interface CreativeState {
  creatives: Creative[];
  selected: Creative | null;
  loading: boolean;
  saving: boolean;
  error: ApiError | null;
  /** The latest Creative, set when a save was rejected as a Conflict. Not an error. */
  conflict: Creative | null;
  /** A newer saved copy heard about while the Collaborator has a Draft. Not yet shown in the form. */
  remoteUpdate: Creative | null;
}

const initialState: CreativeState = {
  creatives: [],
  selected: null,
  loading: false,
  saving: false,
  error: null,
  conflict: null,
  remoteUpdate: null,
};

/**
 * Signal store for the Creatives screens. It is not the API's store of Creatives.
 * Components never see raw HTTP errors.
 */
export const CreativeSignalStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, api = inject(CreativesApi), displayName = inject(DisplayName)) => {
    let inFlight: Subscription | undefined;

    const load = <T>(request: Observable<T>, onResult: (result: T) => void): void => {
      inFlight?.unsubscribe();
      patchState(store, { loading: true, error: null });
      inFlight = request.subscribe({
        next: (result) => {
          onResult(result);
          patchState(store, { loading: false });
        },
        error: (error: unknown) => {
          patchState(store, { error: toApiError(error), loading: false });
        },
      });
    };

    const save = async (request: Observable<Creative>): Promise<Creative | null> => {
      patchState(store, { saving: true, error: null });
      try {
        const saved = await firstValueFrom(request);
        const current = store.selected();
        // A live update may have been adopted while this save was in flight; never go backwards.
        if (!current || saved.version >= current.version) {
          patchState(store, { selected: saved });
        }
        patchState(store, { conflict: null });
        const remote = store.remoteUpdate();
        if (remote && remote.version <= saved.version) {
          patchState(store, { remoteUpdate: null });
        }
        return saved;
      } catch (error) {
        const latest = conflictOf(error);
        if (latest) {
          patchState(store, { conflict: latest, remoteUpdate: null });
        } else {
          patchState(store, { error: toApiError(error) });
        }
        return null;
      } finally {
        patchState(store, { saving: false });
      }
    };

    return {
      loadList(): void {
        patchState(store, { selected: null, conflict: null, remoteUpdate: null });
        load(api.list(), (creatives) => patchState(store, { creatives }));
      },

      open(id: string): void {
        patchState(store, { selected: null, conflict: null, remoteUpdate: null });
        load(api.get(id), (creative) => patchState(store, { selected: creative }));
      },

      /** Prepares an empty edit page: nothing selected, nothing loading, no stale error. */
      startNew(): void {
        inFlight?.unsubscribe();
        patchState(store, {
          selected: null,
          conflict: null,
          remoteUpdate: null,
          loading: false,
          error: null,
        });
      },

      create(fields: CreativeFields): Promise<Creative | null> {
        return save(api.create(fields));
      },

      update(id: string, fields: CreativeFields, version: number): Promise<Creative | null> {
        return save(api.update(id, fields, version));
      },

      /** Discards the Draft: the latest Creative from the Conflict becomes the selected one. */
      loadLatest(): void {
        const latest = store.conflict();
        if (latest) {
          patchState(store, { selected: latest, conflict: null });
        }
      },

      /** Rebase: re-applies the Draft's changed fields onto the latest Creative and saves again. */
      keepMyEdits(id: string, draft: CreativeFields): Promise<Creative | null> {
        const loaded = store.selected();
        const latest = store.conflict();
        if (!loaded || !latest) {
          return Promise.resolve(null);
        }
        return save(api.update(id, rebase(loaded, draft, latest), latest.version));
      },

      /**
       * Handles a Creative saved by someone (the hub also echoes my own saves back).
       * A Draft is never overwritten: the form only changes when it is clean.
       */
      receiveUpdate(incoming: Creative, formIsDirty: boolean): void {
        const selected = store.selected();
        if (!selected || incoming.id !== selected.id) {
          return;
        }
        const conflict = store.conflict();
        const known = Math.max(selected.version, store.remoteUpdate()?.version ?? 0, conflict?.version ?? 0);
        if (incoming.version <= known) {
          return;
        }
        // The echo of my own save can arrive before the HTTP response that carries the same Creative.
        if (store.saving() && incoming.updatedBy === displayName.value()) {
          return;
        }
        if (conflict) {
          patchState(store, { conflict: incoming });
        } else if (formIsDirty) {
          patchState(store, { remoteUpdate: incoming });
        } else {
          patchState(store, { selected: incoming, remoteUpdate: null });
        }
      },

      /** Adopts the newest remote update, discarding the Draft. */
      loadRemote(): void {
        const latest = store.remoteUpdate();
        if (latest) {
          patchState(store, { selected: latest, remoteUpdate: null });
        }
      },
    };
  }),
);

export type CreativeSignalStore = InstanceType<typeof CreativeSignalStore>;

/** The latest Creative carried by a 409, or null when the error is not a usable Conflict. */
function conflictOf(error: unknown): Creative | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
    return null;
  }
  const parsed = creativeSchema.safeParse((error.error as { current?: unknown } | null)?.current);
  return parsed.success ? parsed.data : null;
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
