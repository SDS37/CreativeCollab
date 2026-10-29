import { expect, test } from '@sand4rt/experimental-ct-angular';
import { Creative } from '../../core/creative.model';
import { RemoteUpdateNotice } from './remote-update-notice';

const update: Creative = {
  id: '1',
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 4,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Grace',
};

test('names who saved and which Version arrived, without discarding the Draft', async ({ mount }) => {
  const component = await mount(RemoteUpdateNotice, { props: { update } });

  await expect(component).toContainText('Grace updated this Creative (Version 4)');
  await expect(component).toContainText('Your edits stay until you do.');
});

test('offers to load the latest', async ({ mount }) => {
  let loads = 0;
  const component = await mount(RemoteUpdateNotice, {
    props: { update },
    on: { loadLatest: () => loads++ },
  });

  await component.getByRole('button').click();

  expect(loads).toBe(1);
});
