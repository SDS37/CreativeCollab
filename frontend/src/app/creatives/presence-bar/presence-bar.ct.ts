import { expect, test } from '@sand4rt/experimental-ct-angular';
import { PresenceBar } from './presence-bar';

test('renders nothing when nobody has the Creative open', async ({ mount }) => {
  const component = await mount(PresenceBar, { props: { names: [] } });

  await expect(component).toHaveText('');
});

test('lists one Display name per open session, including a repeated name', async ({ mount }) => {
  const component = await mount(PresenceBar, { props: { names: ['Ada', 'Ada'] } });

  await expect(component).toContainText('Open now: Ada, Ada');
});
