export interface Creative {
  id: number;
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

export const EDITABLE_FIELDS: readonly (keyof CreativeFields)[] = [
  'name',
  'headline',
  'body',
  'cta',
  'imageUrl',
];
