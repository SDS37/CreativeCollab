import { Component, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DisplayName } from './core/display-name';
import { DisplayNamePrompt } from './display-name-prompt/display-name-prompt';

@Component({
  imports: [RouterOutlet, DisplayNamePrompt],
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly displayName = inject(DisplayName);
  private readonly changing = signal(false);

  protected readonly needsName = computed(() => !this.displayName.isSet() || this.changing());

  protected startChange(): void {
    this.changing.set(true);
  }

  protected confirm(name: string): void {
    this.displayName.set(name);
    this.changing.set(false);
  }
}
