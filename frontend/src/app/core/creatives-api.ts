import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { CREATIVES_URL } from './api-config';
import { Creative, CreativeFields } from './creative.model';
import { DisplayName } from './display-name';

/** Thin wrapper over the Creatives HTTP endpoints. Errors are left raw for the store to translate. */
@Service()
export class CreativesApi {
  private readonly http = inject(HttpClient);
  private readonly displayName = inject(DisplayName);

  list(): Observable<Creative[]> {
    return this.http.get<Creative[]>(CREATIVES_URL);
  }

  get(id: number): Observable<Creative> {
    return this.http.get<Creative>(`${CREATIVES_URL}/${id}`);
  }

  create(fields: CreativeFields): Observable<Creative> {
    return this.http.post<Creative>(CREATIVES_URL, this.body(fields));
  }

  update(id: number, fields: CreativeFields, version: number): Observable<Creative> {
    return this.http.put<Creative>(`${CREATIVES_URL}/${id}`, { ...this.body(fields), version });
  }

  private body(fields: CreativeFields) {
    const { imageUrl, ...rest } = fields;
    return {
      ...rest,
      ...(imageUrl.trim() === '' ? {} : { imageUrl: imageUrl.trim() }),
      displayName: this.displayName.value(),
    };
  }
}
