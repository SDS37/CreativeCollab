export interface Creative {
  id: string;
  name: string;
  headline: string;
  body: string;
  cta: string;
  imageUrl: string | null;
  version: number;
  updatedAt: string;
  updatedBy: string;
}

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
