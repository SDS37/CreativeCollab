import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Creative } from './creative.model';
import { CreativeHubService, HUB_CONNECTION_FACTORY } from './creative-hub';
import { Session } from './session';
import { fakeAccessToken, signInAs } from './session.testing';

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
  accessTokenFactory: () => string | Promise<string> = () => '';

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
  id: '1',
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

  const REFRESH_URL = 'http://localhost:5114/api/auth/refresh';

  beforeEach(() => {
    connection = new FakeConnection();
    created = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: HUB_CONNECTION_FACTORY,
          useValue: (accessTokenFactory: () => string | Promise<string>) => {
            created++;
            connection.accessTokenFactory = accessTokenFactory;
            return connection as unknown as HubConnection;
          },
        },
      ],
    });
    hub = TestBed.inject(CreativeHubService);
  });

  it('joins the Creative by id alone and exposes Presence from the reply', async () => {
    connection.joinResults[0] = ['Ada', 'Grace'];

    await hub.join('1');

    expect(connection.invocations).toEqual([{ method: 'JoinCreative', args: ['1'] }]);
    expect(hub.presence()).toEqual(['Ada', 'Grace']);
    expect(hub.connected()).toBe(true);
  });

  it('adds one entry per UserJoined and removes one entry per UserLeft', async () => {
    connection.joinResults[0] = ['Ada'];
    await hub.join('1');

    connection.raise('UserJoined', 'Grace');
    connection.raise('UserJoined', 'Grace');
    expect(hub.presence()).toEqual(['Ada', 'Grace', 'Grace']);

    connection.raise('UserLeft', 'Grace');
    expect(hub.presence()).toEqual(['Ada', 'Grace']);

    connection.raise('UserLeft', 'Nobody');
    expect(hub.presence()).toEqual(['Ada', 'Grace']);
  });

  it('emits each updated Creative', async () => {
    await hub.join('1');
    const seen: Creative[] = [];
    hub.updated.subscribe((c) => seen.push(c));

    connection.raise('CreativeUpdated', creative);

    expect(seen).toEqual([creative]);
  });

  it('ignores a CreativeUpdated that is not a Creative', async () => {
    await hub.join('1');
    const seen: Creative[] = [];
    hub.updated.subscribe((c) => seen.push(c));

    connection.raise('CreativeUpdated', { id: '1' });

    expect(seen).toEqual([]);
  });

  it('joins again after a reconnect and replaces Presence', async () => {
    connection.joinResults.push(['Ada', 'Linus']);
    await hub.join('1');

    connection.simulateReconnect();
    await Promise.resolve();
    await Promise.resolve();

    expect(connection.methods()).toEqual(['JoinCreative', 'JoinCreative']);
    expect(connection.invocations[1].args).toEqual(['1']);
    expect(hub.presence()).toEqual(['Ada', 'Linus']);
  });

  it('leave tells the server, clears Presence and keeps the connection open', async () => {
    await hub.join('1');

    await hub.leave();

    expect(connection.methods()).toEqual(['JoinCreative', 'LeaveCreative']);
    expect(connection.invocations[1].args).toEqual(['1']);
    expect(hub.presence()).toEqual([]);
    expect(connection.stops).toBe(0);
  });

  it('dispose leaves the group, stops the connection, and is safe to repeat', async () => {
    await hub.join('1');

    await hub.dispose();
    await hub.dispose();

    expect(connection.methods()).toEqual(['JoinCreative', 'LeaveCreative']);
    expect(connection.stops).toBe(1);
    expect(hub.presence()).toEqual([]);
    expect(hub.connected()).toBe(false);
  });

  it('switches Creatives by leaving the old group first', async () => {
    connection.joinResults.push(['Ada']);
    await hub.join('1');
    await hub.join('2');

    expect(connection.invocations.map((i) => [i.method, i.args[0]])).toEqual([
      ['JoinCreative', '1'],
      ['LeaveCreative', '1'],
      ['JoinCreative', '2'],
    ]);
    expect(created).toBe(1);
  });

  it('opens a fresh connection when joined again after dispose', async () => {
    await hub.join('1');
    await hub.dispose();
    connection.joinResults.push(['Ada']);

    await hub.join('1');

    expect(created).toBe(2);
    expect(connection.starts).toBe(2);
  });

  it('reports no live connection and empty Presence when the server cannot be reached', async () => {
    connection.failStart = true;

    await hub.join('1');

    expect(hub.connected()).toBe(false);
    expect(hub.unavailable()).toBe(true);
    expect(hub.presence()).toEqual([]);
  });

  it('marks the connection as not live when it closes', async () => {
    await hub.join('1');

    connection.simulateClose();

    expect(hub.connected()).toBe(false);
    expect(hub.unavailable()).toBe(true);
  });

  it('does not join if disposed while still connecting', async () => {
    const joining = hub.join('1');
    const disposing = hub.dispose();
    await Promise.all([joining, disposing]);

    expect(connection.methods()).not.toContain('JoinCreative');
    expect(hub.presence()).toEqual([]);
  });

  describe('access token on every connect and reconnect', () => {
    it('hands the current token to the connection while it is still valid, without a refresh', async () => {
      await signInAs('Ada', 'Editor', 900);
      const http = TestBed.inject(HttpTestingController);
      await hub.join('1');

      const token = await connection.accessTokenFactory();

      expect(token).toBe(TestBed.inject(Session).accessToken());
      http.expectNone(REFRESH_URL);
    });

    it('refreshes first when the token has expired and passes the new token', async () => {
      await signInAs('Ada', 'Editor', -60);
      const http = TestBed.inject(HttpTestingController);
      await hub.join('1');

      const pending = Promise.resolve(connection.accessTokenFactory());
      const renewed = fakeAccessToken('Ada', 'Editor', 900);
      http.expectOne(REFRESH_URL).flush({ accessToken: renewed, expiresIn: 900 });

      expect(await pending).toBe(renewed);
    });

    it('refreshes when the token is about to expire', async () => {
      await signInAs('Ada', 'Editor', 5);
      const http = TestBed.inject(HttpTestingController);
      await hub.join('1');

      const pending = Promise.resolve(connection.accessTokenFactory());
      const renewed = fakeAccessToken('Ada', 'Editor', 900);
      http.expectOne(REFRESH_URL).flush({ accessToken: renewed, expiresIn: 900 });

      expect(await pending).toBe(renewed);
    });

    it('shares one refresh with a request that renews at the same time', async () => {
      await signInAs('Ada', 'Editor', -60);
      const http = TestBed.inject(HttpTestingController);
      await hub.join('1');

      const other = new Promise((resolve) => TestBed.inject(Session).refresh().subscribe(resolve));
      const pending = Promise.resolve(connection.accessTokenFactory());
      http
        .expectOne(REFRESH_URL)
        .flush({ accessToken: fakeAccessToken('Ada', 'Editor', 900), expiresIn: 900 });

      expect(await pending).toBe(await other);
    });

    it('stops reconnecting and reports live updates unavailable when the refresh fails', async () => {
      await signInAs('Ada', 'Editor', -60);
      const http = TestBed.inject(HttpTestingController);
      await hub.join('1');

      const pending = Promise.resolve(connection.accessTokenFactory()).then(
        () => 'resolved',
        () => 'rejected',
      );
      http.expectOne(REFRESH_URL).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(await pending).toBe('rejected');
      expect(connection.stops).toBe(1);
      expect(hub.unavailable()).toBe(true);
      expect(hub.connected()).toBe(false);
      expect(TestBed.inject(Session).isSignedIn()).toBe(false);
      http.expectNone(REFRESH_URL);
    });
  });
});
