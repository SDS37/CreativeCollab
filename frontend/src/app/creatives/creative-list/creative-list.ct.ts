import { expect, test } from '@sand4rt/experimental-ct-angular';
import { Creative } from '../../core/creative.model';
import { CreativeList } from './creative-list';

function creative(overrides: Partial<Creative> = {}): Creative {
  return {
    id: '41f18372-7214-4fa9-8968-529416310b43',
    name: 'Spring sale banner',
    headline: 'Spring into savings',
    body: 'Up to 40% off.',
    cta: 'Shop now',
    imageUrl: null,
    version: 1,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: 'Ada',
    ...overrides,
  };
}

test('shows each Creative and reports the one the Collaborator opens', async ({ mount }) => {
  const opened: string[] = [];
  const component = await mount(CreativeList, {
    props: { creatives: [creative(), creative({ id: 'other', name: 'Newsletter signup' })] },
    on: { opened: (id: string) => opened.push(id) },
  });

  const buttons = component.getByRole('button');
  await expect(buttons.nth(0)).toContainText('Spring sale banner');
  await expect(buttons.nth(0)).toContainText('Spring into savings');
  await expect(buttons.nth(0)).toContainText('Ada');

  await buttons.nth(1).click();

  expect(opened).toEqual(['other']);
});

test('does not invent a Display name for a seeded Creative', async ({ mount }) => {
  const component = await mount(CreativeList, {
    props: { creatives: [creative({ updatedBy: '' })] },
  });

  await expect(component).toContainText('Version 1');
  await expect(component).not.toContainText('·');
});
