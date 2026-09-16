import type { TestingLibraryMatchers } from '@testing-library/jest-dom/matchers';

// Матчеры jest-dom подключены в test-setup.ts и работают в прогоне, но без этого объявления не
// существуют для типов: тест с `toBeInTheDocument` проходил локально и падал на сборке в CI.
// Те же шесть строк лежат у операторской панели, платформенной панели и оболочки игрока.
declare module 'bun:test' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface Matchers<T> extends TestingLibraryMatchers<typeof expect.stringContaining, T> {}
  interface AsymmetricMatchers extends TestingLibraryMatchers<unknown, unknown> {}
}
