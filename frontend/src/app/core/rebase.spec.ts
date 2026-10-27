import { Creative, CreativeFields } from './creative.model';
import { rebase } from './rebase';

function creative(overrides: Partial<Creative> = {}): Creative {
  return {
    id: 1,
    name: 'Spring',
    headline: 'Fresh',
    body: 'Buy now',
    cta: 'Shop',
    imageUrl: null,
    version: 1,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: 'Ada',
    ...overrides,
  };
}

function draft(overrides: Partial<CreativeFields> = {}): CreativeFields {
  return { name: 'Spring', headline: 'Fresh', body: 'Buy now', cta: 'Shop', imageUrl: '', ...overrides };
}

describe('rebase', () => {
  it('keeps the fields the Collaborator changed', () => {
    const result = rebase(creative(), draft({ headline: 'Mine' }), creative({ version: 2, headline: 'Theirs' }));
    expect(result.headline).toBe('Mine');
  });

  it('takes the latest value for fields the Collaborator did not change', () => {
    const result = rebase(
      creative(),
      draft({ headline: 'Mine' }),
      creative({ version: 2, headline: 'Theirs', body: 'Their body', cta: 'Their cta', name: 'Their name' }),
    );
    expect(result).toEqual({
      name: 'Their name',
      headline: 'Mine',
      body: 'Their body',
      cta: 'Their cta',
      imageUrl: '',
    });
  });

  it('treats an unchanged empty image URL as unchanged, taking the latest image', () => {
    const result = rebase(creative(), draft(), creative({ version: 2, imageUrl: 'https://x.test/a.png' }));
    expect(result.imageUrl).toBe('https://x.test/a.png');
  });

  it('keeps an image URL the Collaborator cleared', () => {
    const base = creative({ imageUrl: 'https://x.test/a.png' });
    const result = rebase(base, draft({ imageUrl: '' }), creative({ version: 2, imageUrl: 'https://x.test/b.png' }));
    expect(result.imageUrl).toBe('');
  });

  it('keeps a changed field even when it now equals the latest value', () => {
    const result = rebase(creative(), draft({ cta: 'Go' }), creative({ version: 2, cta: 'Go' }));
    expect(result.cta).toBe('Go');
  });
});
