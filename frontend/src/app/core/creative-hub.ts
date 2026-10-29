import { inject, InjectionToken, Service, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { HUB_URL } from './api-config';
import { Creative, creativeSchema } from './creative.model';
import { Session } from './session';

/** Builds the SignalR connection. Replaced in tests with a fake. */
export const HUB_CONNECTION_FACTORY = new InjectionToken<() => HubConnection>('HUB_CONNECTION_FACTORY', {
  providedIn: 'root',
  factory: () => {
    const session = inject(Session);
    // Read again on every connect and reconnect, so a renewed token is used.
    return () =>
      new HubConnectionBuilder()
        .withUrl(HUB_URL, { accessTokenFactory: () => session.accessToken() ?? '' })
        .withAutomaticReconnect()
        .build();
  },
});

interface Group {
  id: string;
}

/**
 * The SignalR connection for the Creative a Collaborator has open. It exposes Presence
 * (one account name per open session) and the stream of saved Creatives. One group at a time.
 */
@Service()
export class CreativeHubService {
  private readonly createConnection = inject(HUB_CONNECTION_FACTORY);

  private readonly presenceState = signal<string[]>([]);
  private readonly connectedState = signal(false);
  private readonly updates = new Subject<Creative>();

  readonly presence = this.presenceState.asReadonly();
  /** False when the server could not be reached or the connection closed. */
  readonly connected = this.connectedState.asReadonly();
  /** True once a join failed or the connection was lost, so the page can say live updates are off. */
  readonly unavailable = signal(false);
  readonly updated: Observable<Creative> = this.updates.asObservable();

  private connection: HubConnection | undefined;
  private group: Group | null = null;
  /** Bumped by every join and dispose, so a join that was overtaken stops quietly. */
  private epoch = 0;

  async join(id: string): Promise<void> {
    const epoch = ++this.epoch;
    try {
      await this.leaveGroup();
      if (epoch !== this.epoch) {
        return;
      }
      const connection = this.ensureConnection();
      if (connection.state === HubConnectionState.Disconnected) {
        await connection.start();
      }
      if (epoch !== this.epoch) {
        return;
      }
      this.group = { id };
      this.presenceState.set(await this.invokeJoin(connection, this.group));
      this.connectedState.set(true);
      this.unavailable.set(false);
    } catch {
      this.group = null;
      this.connectedState.set(false);
      this.unavailable.set(true);
      this.presenceState.set([]);
    }
  }

  /** Leaves the group but keeps the connection for the next Creative. */
  async leave(): Promise<void> {
    this.epoch++;
    await this.leaveGroup();
  }

  /** Leaves the group and closes the connection. Safe to call more than once. */
  async dispose(): Promise<void> {
    this.epoch++;
    await this.leaveGroup();
    const connection = this.connection;
    this.connection = undefined;
    this.connectedState.set(false);
    this.unavailable.set(false);
    if (connection) {
      try {
        await connection.stop();
      } catch {
        // Already closed; nothing left to release.
      }
    }
  }

  private ensureConnection(): HubConnection {
    if (this.connection) {
      return this.connection;
    }
    const connection = this.createConnection();
    this.connection = connection;

    connection.on('CreativeUpdated', (creative: unknown) => {
      const parsed = creativeSchema.safeParse(creative);
      if (parsed.success) {
        this.updates.next(parsed.data);
      }
    });
    connection.on('UserJoined', (name: string) => this.presenceState.update((names) => [...names, name]));
    connection.on('UserLeft', (name: string) =>
      this.presenceState.update((names) => {
        const index = names.indexOf(name);
        return index === -1 ? names : names.filter((_, i) => i !== index);
      }),
    );
    connection.onreconnected(() => void this.rejoin(connection));
    connection.onclose(() => {
      if (this.connection === connection) {
        this.connectedState.set(false);
        this.unavailable.set(true);
      }
    });
    return connection;
  }

  /** Group membership belongs to the old connection id, so it must be re-established. */
  private async rejoin(connection: HubConnection): Promise<void> {
    const group = this.group;
    if (!group || this.connection !== connection) {
      return;
    }
    try {
      this.presenceState.set(await this.invokeJoin(connection, group));
      this.connectedState.set(true);
      this.unavailable.set(false);
    } catch {
      this.connectedState.set(false);
      this.unavailable.set(true);
    }
  }

  private invokeJoin(connection: HubConnection, group: Group): Promise<string[]> {
    return connection.invoke<string[]>('JoinCreative', group.id);
  }

  private async leaveGroup(): Promise<void> {
    const group = this.group;
    this.group = null;
    this.presenceState.set([]);
    if (group && this.connection?.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('LeaveCreative', group.id);
      } catch {
        // The server drops the connection from the group anyway when it disconnects.
      }
    }
  }
}
