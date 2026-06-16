import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorPalette } from './FloorPalette';

const unplaced = [{ id: 's2', name: 'PC-02', seatType: 'pc' }, { id: 's3', name: 'PS-01', seatType: 'console' }];

describe('FloorPalette', () => {
  it('lists unplaced seats and fires onPlaceSeat', () => {
    let placed = '';
    const { getByText } = render(
      <I18nProvider><FloorPalette unplaced={unplaced} onPlaceSeat={(id) => { placed = id; }} /></I18nProvider>);
    fireEvent.click(getByText('PC-02'));
    expect(placed).toBe('s2');
  });
  it('shows an empty hint when everything is placed', () => {
    const { container } = render(
      <I18nProvider><FloorPalette unplaced={[]} onPlaceSeat={() => {}} /></I18nProvider>);
    expect(container.querySelector('.floor-palette-empty')).not.toBeNull();
  });
});
