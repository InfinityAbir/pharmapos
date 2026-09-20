/**
 * Minimal CSV export for tabular reports. The rows passed in are the compact
 * server-side aggregates already fetched for display — no computation happens here
 * beyond text escaping. PDF export is out of scope (no export infrastructure exists).
 */
export function toCsvRecords(
  columns: readonly { key: string; header: string }[] | readonly string[],
  rows: readonly Record<string, unknown>[],
): string {
  const keys = columns.map((column) => (typeof column === 'string' ? column : column.key));
  const headers = columns.map((column) => (typeof column === 'string' ? column : column.header));
  const lines = [headers.map(escapeCell).join(',')];
  for (const row of rows) {
    lines.push(keys.map((key) => escapeCell(formatCell(row[key]))).join(','));
  }
  return lines.join('\r\n') + '\r\n';
}

export function downloadCsv(filename: string, csv: string): void {
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
}

function formatCell(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }
  return String(value);
}

function escapeCell(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}
