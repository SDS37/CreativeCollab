import { Component, computed, inject, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CreativeSignalStore } from '../../core/creative-signal-store';
import { CreativeList } from '../creative-list/creative-list';

@Component({
  selector: 'app-creative-list-page',
  imports: [CreativeList, RouterLink],
  templateUrl: './creative-list-page.html',
})
export class CreativeListPage implements OnInit {
  protected readonly store = inject(CreativeSignalStore);
  private readonly router = inject(Router);

  protected readonly isEmpty = computed(() => this.store.creatives().length === 0);

  ngOnInit(): void {
    this.reload();
  }

  protected reload(): void {
    this.store.loadList();
  }

  protected open(id: string): void {
    void this.router.navigate(['/creatives', id]);
  }
}
