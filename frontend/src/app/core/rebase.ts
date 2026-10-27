import { Creative, CreativeFields, EDITABLE_FIELDS, toFields } from './creative.model';

/**
 * Re-applies only the fields the Collaborator changed (Draft versus the Creative
 * they loaded) onto the latest Creative. Fields they did not change take the latest values.
 */
export function rebase(loaded: Creative, draft: CreativeFields, latest: Creative): CreativeFields {
  const base = toFields(loaded);
  const result = toFields(latest);
  for (const field of EDITABLE_FIELDS) {
    if (draft[field] !== base[field]) {
      result[field] = draft[field];
    }
  }
  return result;
}
