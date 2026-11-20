export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const u = ['KB', 'MB', 'GB', 'TB']; let i = -1;
  do { bytes /= 1024; i++; } while (bytes >= 1024 && i < u.length - 1);
  return `${bytes.toFixed(1)} ${u[i]}`;
}

/** The readable message of a failed request: the server answers { message } (BusinessException, refused calls) or { error } (older calls). */
export function errMsg(e: any): string {
  return e?.error?.message ?? e?.error?.error ?? (e?.error?.errors?.join?.('; '))
    ?? (e?.status === 0 ? 'Cannot reach the server' : e?.status === 403 ? 'You do not have permission to do this.' : 'Request failed');
}
