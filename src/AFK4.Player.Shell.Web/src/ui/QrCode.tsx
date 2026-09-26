import { useMemo } from 'react';
import QRCode from 'qrcode';

/**
 * QR прямо в разметке — квадратами SVG, без картинки и без innerHTML. Тихая зона в четыре
 * модуля обязательна: без неё камера телефона в полутёмном зале код не видит.
 */
export function QrCode({ value, label }: { value: string; label: string }) {
  const { size, cells } = useMemo(() => {
    const qr = QRCode.create(value, { errorCorrectionLevel: 'M' });
    const moduleCount = qr.modules.size;
    const filled: [number, number][] = [];
    for (let row = 0; row < moduleCount; row++) {
      for (let col = 0; col < moduleCount; col++) {
        if (qr.modules.get(row, col)) filled.push([col, row]);
      }
    }
    return { size: moduleCount, cells: filled };
  }, [value]);

  const quiet = 4;
  const total = size + quiet * 2;
  return (
    <svg className="qr" viewBox={`0 0 ${total} ${total}`} role="img" aria-label={label} shapeRendering="crispEdges">
      <rect width={total} height={total} fill="#ffffff" />
      {cells.map(([x, y]) => (
        <rect key={`${x}:${y}`} x={x + quiet} y={y + quiet} width={1} height={1} fill="#04120d" />
      ))}
    </svg>
  );
}
