import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API } from '../config/api.config';
import { FileItem } from '../models';

@Injectable({ providedIn: 'root' })
export class FilesService {
  private http = inject(HttpClient);

  list() { return this.http.get<FileItem[]>(`${API}/files`); }
  download(name: string) {
    return this.http.get(`${API}/files/${encodeURIComponent(name)}`, { responseType: 'blob' });
  }
  remove(name: string) { return this.http.delete(`${API}/files/${encodeURIComponent(name)}`); }
}
