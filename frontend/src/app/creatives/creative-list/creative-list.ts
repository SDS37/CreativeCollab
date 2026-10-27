import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { Creative } from '../../core/creative.model';

/** Shows Creatives as given and reports which one the Collaborator chose. */
@Component({
  selector: 'app-creative-list',
  imports: [DatePipe],
  template: `
    <ul class="creatives">
      @for (creative of creatives(); track creative.id) {
        <li>
          <button type="button" class="open" (click)="opened.emit(creative.id)">
            <span class="creative-name">{{ creative.name }}</span>
            <span class="creative-headline">{{ creative.headline }}</span>
            <span class="creative-meta">
              Version {{ creative.version }}
              @if (creative.updatedBy) {
                · {{ creative.updatedBy }}, {{ creative.updatedAt | date: 'medium' }}
              }
            </span>
          </button>
        </li>
      }
    </ul>
  `,
  styles: `
    .creatives { list-style: none; padding: 0; display: grid; gap: 0.5rem; }
    button.open { width: 100%; text-align: left; display: grid; gap: 0.15rem; padding: 0.75rem; font: inherit; cursor: pointer; }
    .creative-name { font-weight: 600; }
    .creative-meta { color: #555; font-size: 0.85rem; }
  `,
})
export class CreativeList {
  readonly creatives = input.required<Creative[]>();
  readonly opened = output<string>();
}
