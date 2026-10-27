import { ComponentFixture, TestBed } from '@angular/core/testing';
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

describe('RemoteUpdateNotice', () => {
  let fixture: ComponentFixture<RemoteUpdateNotice>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [RemoteUpdateNotice] }).compileComponents();
    fixture = TestBed.createComponent(RemoteUpdateNotice);
  });

  it('names who saved and which Version arrived, without discarding the Draft', async () => {
    fixture.componentRef.setInput('update', update);
    await fixture.whenStable();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Grace updated this Creative (Version 4)');
    expect(text).toContain('Your edits stay until you do.');
  });

  it('offers to load the latest', async () => {
    let loads = 0;
    fixture.componentInstance.loadLatest.subscribe(() => loads++);
    fixture.componentRef.setInput('update', update);
    await fixture.whenStable();

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();

    expect(loads).toBe(1);
  });
});
