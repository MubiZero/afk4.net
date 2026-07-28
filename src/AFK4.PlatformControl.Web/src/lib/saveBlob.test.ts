import { it, expect, afterEach, mock, spyOn } from 'bun:test';
import { saveBlob } from './saveBlob';

afterEach(() => { mock.restore(); });

it('creates an object URL, clicks an anchor with the filename, and revokes', () => {
  const createObjectURL = mock(() => 'blob:test');
  const revokeObjectURL = mock();
  (URL as unknown as { createObjectURL: typeof createObjectURL }).createObjectURL = createObjectURL;
  (URL as unknown as { revokeObjectURL: typeof revokeObjectURL }).revokeObjectURL = revokeObjectURL;
  const click = spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

  saveBlob(new Blob(['a,b,c']), 'report.csv');

  expect(createObjectURL).toHaveBeenCalledTimes(1);
  expect(click).toHaveBeenCalledTimes(1);
  expect(revokeObjectURL).toHaveBeenCalledWith('blob:test');
});
