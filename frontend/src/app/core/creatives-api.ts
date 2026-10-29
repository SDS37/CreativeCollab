import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { map, Observable } from 'rxjs';
import * as z from 'zod';
import { CREATIVES_URL } from './api-config';
import { Creative, creativeSchema, CreativeFields } from './creative.model';

/** Thin wrapper over the Creatives HTTP endpoints. Errors are left raw for the store to translate. */
@Service()
export class CreativesApi {
  private readonly http = inject(HttpClient);

  list(): Observable<Creative[]> {
    return this.http
      .get<unknown>(CREATIVES_URL)
      .pipe(map((body) => z.array(creativeSchema).parse(body)));
  }

  get(id: string): Observable<Creative> {
    return this.http
      .get<unknown>(`${CREATIVES_URL}/${id}`)
      .pipe(map((body) => creativeSchema.parse(body)));
  }

  create(fields: CreativeFields): Observable<Creative> {
    return this.http
      .post<unknown>(CREATIVES_URL, this.body(fields))
      .pipe(map((body) => creativeSchema.parse(body)));
  }

  update(id: string, fields: CreativeFields, version: number): Observable<Creative> {
    return this.http
      .put<unknown>(`${CREATIVES_URL}/${id}`, { ...this.body(fields), version })
      .pipe(map((body) => creativeSchema.parse(body)));
  }

  private body(fields: CreativeFields) {
    const { imageUrl, ...rest } = fields;
    return {
      ...rest,
      ...(imageUrl.trim() === '' ? {} : { imageUrl: imageUrl.trim() }),
    };
  }
}
