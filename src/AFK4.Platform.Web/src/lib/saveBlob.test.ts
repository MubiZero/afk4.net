import { it, expect, vi, afterEach } from 'vitest';
import { saveBlob } from './saveBlob';

afterEach(() => { vi.restoreAllMocks(); });

it('creates an object URL, clicks an anchor with the filename, and revokes', () => {
  const createObjectURL = vi.fn(() => 'blob:test');
  const revokeObjectURL = vi.fn();
  (URL as unknown as { createObjectURL: typeof createObjectURL }).createObjectURL = createObjectURL;
  (URL as unknown as { revokeObjectURL: typeof revokeObjectURL }).revokeObjectURL = revokeObjectURL;
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

  saveBlob(new Blob(['a,b,c']), 'report.csv');

  expect(createObjectURL).toHaveBeenCalledTimes(1);
  expect(click).toHaveBeenCalledTimes(1);
  expect(revokeObjectURL).toHaveBeenCalledWith('blob:test');
});
