import * as z from 'zod';

/** A Creative as the API and the hub send it. */
export const creativeSchema = z.object({
  id: z.string().min(1),
  name: z.string(),
  headline: z.string(),
  body: z.string(),
  cta: z.string(),
  imageUrl: z.string().nullable(),
  version: z.number().int().positive(),
  updatedAt: z.iso.datetime({ offset: true }),
  updatedBy: z.string(),
});

export type Creative = z.infer<typeof creativeSchema>;

/** The editable fields of a Creative: what a Draft holds and what a save sends. */
export interface CreativeFields {
  name: string;
  headline: string;
  body: string;
  cta: string;
  imageUrl: string;
}

/** The editable fields of a saved Creative, with an absent image as an empty string. */
export function toFields(creative: Creative): CreativeFields {
  return {
    name: creative.name,
    headline: creative.headline,
    body: creative.body,
    cta: creative.cta,
    imageUrl: creative.imageUrl ?? '',
  };
}

export const EDITABLE_FIELDS: readonly (keyof CreativeFields)[] = [
  'name',
  'headline',
  'body',
  'cta',
  'imageUrl',
];

export function sameFields(a: CreativeFields, b: CreativeFields): boolean {
  return EDITABLE_FIELDS.every((field) => a[field] === b[field]);
}
