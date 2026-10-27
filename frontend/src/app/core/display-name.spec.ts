import { TestBed } from '@angular/core/testing';
import { DisplayName } from './display-name';

describe('DisplayName', () => {
  beforeEach(() => localStorage.clear());

  function create(): DisplayName {
    return TestBed.inject(DisplayName);
  }

  it('is empty when no Collaborator has given a name', () => {
    const name = create();
    expect(name.value()).toBe('');
    expect(name.isSet()).toBe(false);
  });

  it('remembers the name across sessions', () => {
    create().set('  Ada  ');
    TestBed.resetTestingModule();

    const next = create();
    expect(next.value()).toBe('Ada');
    expect(next.isSet()).toBe(true);
  });

  it('ignores a blank name', () => {
    const name = create();
    name.set('Ada');
    name.set('   ');
    expect(name.value()).toBe('Ada');
  });
});
