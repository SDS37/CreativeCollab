import { Component, computed, effect, inject, input, output, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Creative, CreativeFields } from '../../core/creative.model';

interface FieldConfig {
  key: keyof CreativeFields;
  label: string;
  multiline: boolean;
}

const FIELDS: readonly FieldConfig[] = [
  { key: 'name', label: 'Name', multiline: false },
  { key: 'headline', label: 'Headline', multiline: false },
  { key: 'body', label: 'Body', multiline: true },
  { key: 'cta', label: 'Call to action', multiline: false },
  { key: 'imageUrl', label: 'Image URL (optional)', multiline: false },
];

/** Edits one Draft. It resets only when handed a different Creative; it never saves by itself. */
@Component({
  selector: 'app-creative-form',
  imports: [ReactiveFormsModule],
  templateUrl: './creative-form.html',
  styleUrl: './creative-form.css',
})
export class CreativeForm {
  readonly creative = input<Creative | null>(null);
  readonly saving = input(false);
  readonly fieldErrors = input<Record<string, string[]>>({});

  /** The Collaborator's current Draft, emitted on every change and after a reset. */
  readonly draftChange = output<CreativeFields>();
  readonly saved = output<CreativeFields>();

  protected readonly fields = FIELDS;
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    headline: ['', [Validators.required, Validators.maxLength(40)]],
    body: ['', [Validators.required, Validators.maxLength(200)]],
    cta: ['', [Validators.required, Validators.maxLength(20)]],
    imageUrl: ['', [Validators.maxLength(2048)]],
  });

  protected readonly saveLabel = computed(() => (this.saving() ? 'Saving…' : 'Save'));

  protected readonly messages = computed(() =>
    Object.fromEntries(Object.entries(this.fieldErrors()).map(([key, list]) => [key, list.join(' ')])),
  );

  private readonly emitDraft = this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
    this.draftChange.emit(this.draft());
  });

  private readonly adoptCreative = effect(() => {
    const creative = this.creative();
    untracked(() => {
      this.form.reset({
        name: creative?.name ?? '',
        headline: creative?.headline ?? '',
        body: creative?.body ?? '',
        cta: creative?.cta ?? '',
        imageUrl: creative?.imageUrl ?? '',
      });
    });
  });

  protected submit(): void {
    if (this.form.valid && !this.saving()) {
      this.saved.emit(this.draft());
    }
  }

  private draft(): CreativeFields {
    return this.form.getRawValue();
  }
}
