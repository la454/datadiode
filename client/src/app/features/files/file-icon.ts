import { Component, computed, input } from '@angular/core';

// Icons are the Windows shell icons for each file type, exported to public/icons/<ext>.png.
const KNOWN = new Set(['7z', 'aac', 'accdb', 'ai', 'avi', 'bat', 'bmp', 'cs', 'css', 'csv', 'dll', 'doc', 'docm', 'docx', 'dot', 'dotx', 'exe', 'flac', 'gif', 'gz', 'heic', 'html', 'ico', 'java', 'jpeg', 'jpg', 'js', 'json', 'log', 'm4a', 'md', 'mdb', 'mkv', 'mov', 'mp3', 'mp4', 'msg', 'msi', 'odp', 'ods', 'odt', 'ogg', 'pdf', 'png', 'pps', 'ppsx', 'ppt', 'pptm', 'pptx', 'psd', 'pub', 'py', 'rar', 'rtf', 'sh', 'sql', 'svg', 'tar', 'tgz', 'tif', 'tiff', 'ts', 'txt', 'wav', 'webm', 'webp', 'wmv', 'xls', 'xlsb', 'xlsm', 'xlsx', 'xml', 'yaml', 'yml', 'zip']);

@Component({
  selector: 'app-file-icon',
  template: `<img [src]="src()" [alt]="ext() || 'file'" width="36" height="36" draggable="false">`,
  styles: `:host { display: inline-flex; line-height: 0; } img { display: block; object-fit: contain; }`,
})
export class FileIcon {
  ext = input('');
  src = computed(() => {
    const e = (this.ext() || '').toLowerCase();
    return `icons/${KNOWN.has(e) ? e : 'default'}.png`;
  });
}
