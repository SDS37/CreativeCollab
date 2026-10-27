import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { TestBed } from '@angular/core/testing';
import { Creative } from './creative.model';
import { CreativeHubService, HUB_CONNECTION_FACTORY } from './creative-hub';

class FakeConnection {
  state: HubConnectionState = HubConnectionState.Disconnected;
  readonly handlers = new Map<string, (...args: unknown[]) => void>();
  readonly invocations: { method: string; args: unknown[] }[] = [];
  readonly joinResults: string[][] = [['Ada']];
  failStart = false;
  private reconnected: (() => void)[] = [];
  private closed: (() => void)[] = [];
  starts = 0;
  stops = 0;

  on(method: string, handler: (...args: unknown[]) => void): void {
    this.handlers.set(method, handler);
  }
  onreconnected(cb: () => void): void {
    this.reconnected.push(cb);
  }
  onclose(cb: () => void): void {
    this.closed.push(cb);
  }
  async start(): Promise<void> {
    this.starts++;
    if (this.failStart) {
      throw new Error('no server');
    }
    this.state = HubConnectionState.Connected;
  }
  async stop(): Promise<void> {
    this.stops++;
    this.state = HubConnectionState.Disconnected;
  }
  async invoke(method: string, ...args: unknown[]): Promise<unknown> {
    this.invocations.push({ method, args });
    return method === 'JoinCreative' ? (this.joinResults.shift() ?? []) : undefined;
  }

  raise(method: string, ...args: unknown[]): void {
    this.handlers.get(method)?.(...args);
  }
  simulateReconnect(): void {
    this.reconnected.forEach((cb) => cb());
  }
  simulateClose(): void {
    this.state = HubConnectionState.Disconnected;
    this.closed.forEach((cb) => cb());
  }
  methods(): string[] {
    return this.invocations.map((i) => i.method);
  }
}

const creative: Creative = {
  id: 1,
  name: 'n',
  headline: 'h',
  body: 'b',
  cta: 'c',
  imageUrl: null,
  version: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Grace',
};

describe('CreativeHubService', () => {
  let connection: FakeConnection;
  let hub: CreativeHubService;
  let created: number;

  beforeEach(() => {
    connection = new FakeConnection();
    created = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: HUB_CONNECTION_FACTORY,
          useValue: () => {
            created++;
            return connection as unknown as HubConnection;
          },
        },
      ],
    });
    hub = TestBed.inject(CreativeHubService);
  });

  it('joins the Creative with the Display name and exposes Presence from the reply', async () => {
    connection.joinResults[0] = ['Ada', 'Grace'];

    await hub.join(1, 'Ada');

    expect(connection.invocations).toEqual([{ method: 'JoinCreative', args: [1, 'Ada'] }]);
    expect(hub.presence()).toEqual(['Ada', 'Grace']);
    expect(hub.connected()).toBe(true);
  });

  it('adds one entry per UserJoined and removes one entry per UserLeft', async () => {
    connection.joinResults[0] = ['Ada'];
    await hub.join(1, 'Ada');

    connection.raise('UserJoined', 'Grace');
    connection.raise('UserJoined', 'Grace');
    expect(hub.presence()).toEqual(['Ada', 'Grace', 'Grace']);

    connection.raise('UserLeft', 'Grace');
    expect(hub.presence()).toEqual(['Ada', 'Grace']);

    connection.raise('UserLeft', 'Nobody');
    expect(hub.presence()).toEqual(['Ada', 'Grace']);
  });

  it('emits each updated Creative', async () => {
    await hub.join(1, 'Ada');
    const seen: Creative[] = [];
    hub.updated.subscribe((c) => seen.push(c));

    connection.raise('CreativeUpdated', creative);

    expect(seen).toEqual([creative]);
  });

  it('joins again after a reconnect and replaces Presence', async () => {
    connection.joinResults.push(['Ada', 'Linus']);
    await hub.join(1, 'Ada');

    connection.simulateReconnect();
    await Promise.resolve();
    await Promise.resolve();

    expect(connection.methods()).toEqual(['JoinCreative', 'JoinCreative']);
    expect(connection.invocations[1].args).toEqual([1, 'Ada']);
    expect(hub.presence()).toEqual(['Ada', 'Linus']);
  });

  it('leave tells the server, clears Presence and keeps the connection open', async () => {
    await hub.join(1, 'Ada');

    await hub.leave();

    expect(connection.methods()).toEqual(['JoinCreative', 'LeaveCreative']);
    expect(connection.invocations[1].args).toEqual([1]);
    expect(hub.presence()).toEqual([]);
    expect(connection.stops).toBe(0);
  });

  it('dispose leaves the group, stops the connection, and is safe to repeat', async () => {
    await hub.join(1, 'Ada');

    await hub.dispose();
    await hub.dispose();

    expect(connection.methods()).toEqual(['JoinCreative', 'LeaveCreative']);
    expect(connection.stops).toBe(1);
    expect(hub.presence()).toEqual([]);
    expect(hub.connected()).toBe(false);
  });

  it('switches Creatives by leaving the old group first', async () => {
    connection.joinResults.push(['Ada']);
    await hub.join(1, 'Ada');
    await hub.join(2, 'Ada');

    expect(connection.invocations.map((i) => [i.method, i.args[0]])).toEqual([
      ['JoinCreative', 1],
      ['LeaveCreative', 1],
      ['JoinCreative', 2],
    ]);
    expect(created).toBe(1);
  });

  it('opens a fresh connection when joined again after dispose', async () => {
    await hub.join(1, 'Ada');
    await hub.dispose();
    connection.joinResults.push(['Ada']);

    await hub.join(1, 'Ada');

    expect(created).toBe(2);
    expect(connection.starts).toBe(2);
  });

  it('reports no live connection and empty Presence when the server cannot be reached', async () => {
    connection.failStart = true;

    await hub.join(1, 'Ada');

    expect(hub.connected()).toBe(false);
    expect(hub.unavailable()).toBe(true);
    expect(hub.presence()).toEqual([]);
  });

  it('marks the connection as not live when it closes', async () => {
    await hub.join(1, 'Ada');

    connection.simulateClose();

    expect(hub.connected()).toBe(false);
    expect(hub.unavailable()).toBe(true);
  });

  it('does not join if disposed while still connecting', async () => {
    const joining = hub.join(1, 'Ada');
    const disposing = hub.dispose();
    await Promise.all([joining, disposing]);

    expect(connection.methods()).not.toContain('JoinCreative');
    expect(hub.presence()).toEqual([]);
  });
});
