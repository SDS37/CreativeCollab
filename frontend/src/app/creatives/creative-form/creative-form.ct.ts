import { expect, test } from '@sand4rt/experimental-ct-angular';
import { Creative, CreativeFields } from '../../core/creative.model';
import { CreativeForm } from './creative-form';

const creative: Creative = {
  id: '1',
  name: 'Spring',
  headline: 'Fresh',
  body: 'Buy now',
  cta: 'Shop',
  imageUrl: null,
  version: 2,
  updatedAt: '2026-01-01T00:00:00Z',
  updatedBy: 'Ada',
};

test('shows the given Creative in its fields', async ({ mount }) => {
  const component = await mount(CreativeForm, { props: { creative } });

  await expect(component.locator('[name="headline"]')).toHaveValue('Fresh');
  await expect(component.locator('[name="imageUrl"]')).toHaveValue('');
});

test('emits the current Draft as the Collaborator types', async ({ mount }) => {
  const drafts: CreativeFields[] = [];
  const component = await mount(CreativeForm, {
    props: { creative },
    on: { draftChange: (draft: CreativeFields) => drafts.push(draft) },
  });

  await component.locator('[name="headline"]').fill('Fresher');

  // Events cross from the browser to the test asynchronously, so wait for the last one.
  await expect.poll(() => drafts.at(-1)).toEqual({
    name: 'Spring',
    headline: 'Fresher',
    body: 'Buy now',
    cta: 'Shop',
    imageUrl: '',
  });
});

test('emits the Draft on submit', async ({ mount }) => {
  const saved: CreativeFields[] = [];
  const component = await mount(CreativeForm, {
    props: { creative },
    on: { saved: (draft: CreativeFields) => saved.push(draft) },
  });

  await component.locator('[name="cta"]').fill('Buy');
  await component.getByRole('button', { name: 'Save' }).click();

  expect(saved).toHaveLength(1);
  expect(saved[0].cta).toBe('Buy');
});

test('cannot be saved while saving or when a required field is empty', async ({ mount }) => {
  const component = await mount(CreativeForm, { props: { creative } });
  const save = component.locator('button[type="submit"]');
  await expect(save).toBeEnabled();

  await component.locator('[name="headline"]').fill('');
  await expect(save).toBeDisabled();

  await component.locator('[name="headline"]').fill('Back');
  await component.update({ props: { saving: true } });
  await expect(save).toBeDisabled();
});

test('is read-only for a Viewer: fields are locked and there is no Save', async ({ mount }) => {
  const component = await mount(CreativeForm, { props: { creative, readOnly: true } });

  await expect(component.locator('[name="headline"]')).toHaveValue('Fresh');
  await expect(component.locator('[name="headline"]')).toBeDisabled();
  await expect(component.getByRole('button', { name: 'Save' })).toHaveCount(0);
});

test('shows the server message for a rejected field',async ({ mount }) => {
  const component = await mount(CreativeForm, {
    props: { creative, fieldErrors: { headline: ['Headline is too long'] } },
  });

  await expect(component).toContainText('Headline is too long');
});

test('keeps the Draft when the same Creative is passed again, and resets for a different one', async ({ mount }) => {
  const component = await mount(CreativeForm, { props: { creative } });
  const headline = component.locator('[name="headline"]');
  await headline.fill('Typing');

  await component.update({ props: { fieldErrors: {} } });
  await expect(headline).toHaveValue('Typing');

  await component.update({ props: { creative: { ...creative, version: 3, headline: 'Server' } } });
  await expect(headline).toHaveValue('Server');
});
