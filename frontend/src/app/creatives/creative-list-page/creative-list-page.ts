import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CreativeStore } from '../../core/creative-store';
import { CreativeList } from '../creative-list/creative-list';

@Component({
  selector: 'app-creative-list-page',
  imports: [CreativeList, RouterLink],
  templateUrl: './creative-list-page.html',
})
export class CreativeListPage {
  protected readonly store = inject(CreativeStore);
  private readonly router = inject(Router);

  protected readonly isEmpty = computed(() => this.store.creatives().length === 0);

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.store.loadList();
  }

  protected open(id: string): void {
    void this.router.navigate(['/creatives', id]);
  }
}
