import { describe, it, expect } from 'vitest';
import { ACCEPTED_FILE_TYPES, validateAttachmentSelection } from '../fileAttachments';

function file(name: string, type: string, size = 10): File {
  const f = new File(['x'], name, { type });
  Object.defineProperty(f, 'size', { value: size });
  return f;
}

const codes = (files: File[]) => validateAttachmentSelection(files).map((i) => i.code);

describe('validateAttachmentSelection', () => {
  it('accepts supported images and documents', () => {
    expect(codes([file('a.png', 'image/png'), file('b.pdf', 'application/pdf'), file('c.md', '')])).toEqual([]);
  });

  it('rejects text/html and unknown types', () => {
    expect(codes([file('page.html', 'text/html'), file('app.exe', 'application/x-msdownload')])).toEqual([
      'unsupportedType',
      'unsupportedType',
    ]);
  });

  it('rejects a file whose extension does not match its type', () => {
    expect(codes([file('photo.exe', 'image/png')])).toEqual(['extensionMismatch']);
  });

  it('rejects empty files', () => {
    expect(codes([file('notes.txt', 'text/plain', 0)])).toEqual(['emptyFile']);
  });

  it('rejects oversized files with their limit', () => {
    const issues = validateAttachmentSelection([file('big.png', 'image/png', 6 * 1024 * 1024)]);
    expect(issues).toEqual([{ code: 'fileTooLarge', fileName: 'big.png', maxMb: 5 }]);
  });

  it('enforces image count, file count and total size', () => {
    const images = Array.from({ length: 6 }, (_, i) => file(`${i}.png`, 'image/png'));
    expect(codes(images)).toContain('tooManyImages');

    const docs = Array.from({ length: 11 }, (_, i) => file(`${i}.txt`, 'text/plain'));
    expect(codes(docs)).toContain('tooManyFiles');

    const heavy = [file('a.pdf', 'application/pdf', 15 * 1024 * 1024), file('b.pdf', 'application/pdf', 15 * 1024 * 1024)];
    expect(codes(heavy)).toEqual(['totalTooLarge']);
  });

  it('accept attribute lists supported types and no HTML', () => {
    expect(ACCEPTED_FILE_TYPES).toContain('application/pdf');
    expect(ACCEPTED_FILE_TYPES).toContain('.md');
    expect(ACCEPTED_FILE_TYPES).not.toContain('html');
  });
});
