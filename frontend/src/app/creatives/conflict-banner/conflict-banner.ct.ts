import { expect, test } from '@sand4rt/experimental-ct-angular';
import { Creative } from '../../core/creative.model';
import { ConflictBanner } from './conflict-banner';

const current: Creative = {
  id: '1',
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 3,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Bob',
};

test('names who saved and keeps the Draft on screen', async ({ mount }) => {
  const component = await mount(ConflictBanner, { props: { conflict: current } });

  await expect(component).toContainText('Bob saved a newer version');
  await expect(component).toContainText('Your edits are still in the form.');
});

test('says another Collaborator when the latest save has no Display name', async ({ mount }) => {
  const component = await mount(ConflictBanner, { props: { conflict: { ...current, updatedBy: '' } } });

  await expect(component).toContainText('Another Collaborator saved a newer version');
});

test('reports Load latest and Keep my edits', async ({ mount }) => {
  const choices: string[] = [];
  const component = await mount(ConflictBanner, {
    props: { conflict: current },
    on: {
      loadLatest: () => choices.push('load'),
      keepMine: () => choices.push('keep'),
    },
  });

  await component.getByRole('button', { name: 'Load latest' }).click();
  await component.getByRole('button', { name: 'Keep my edits' }).click();

  expect(choices).toEqual(['load', 'keep']);
});

test('disables both actions while a save is in flight', async ({ mount }) => {
  const component = await mount(ConflictBanner, { props: { conflict: current, saving: true } });

  await expect(component.getByRole('button', { name: 'Load latest' })).toBeDisabled();
  await expect(component.getByRole('button', { name: 'Keep my edits' })).toBeDisabled();
});
