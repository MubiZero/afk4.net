// Контракты кита, которые держит компилятор, а не договорённость. Файл не исполняется: его читает
// `tsc -p packages/ui` в `bun run test`. Каждая строка с @ts-expect-error обязана НЕ компилироваться —
// если однажды она скомпилируется, tsc упадёт на «unused @ts-expect-error», и правило, ради
// которого проп обязателен, тихо не исчезнет.
import { Button, CloseButton, EmptyState, IconButton, Inspector, RowActions, SectionHeader, StatusBadge, Tabs } from './index';

export const contracts = [
  // Кнопка без текста без подписи — «кнопка» для диктора.
  // @ts-expect-error label обязателен
  <IconButton icon={<svg />} />,
  // @ts-expect-error label обязателен
  <CloseButton onClick={() => {}} />,
  // У пустого списка следующий шаг — решение, а не забытый аргумент.
  // @ts-expect-error next обязателен
  <EmptyState title="Пусто" />,
  // «Нет прав» без слов, у кого они есть, — тупик.
  // @ts-expect-error hint обязателен для denied
  <EmptyState title="Пусто" next={{ kind: 'denied' }} />,
  // «Делается в другом месте» обязано назвать где, «пусто, и это нормально» — что здесь появится.
  // @ts-expect-error hint обязателен для elsewhere
  <EmptyState title="Пусто" next={{ kind: 'elsewhere' }} />,
  // @ts-expect-error hint обязателен для calm
  <EmptyState title="Пусто" next={{ kind: 'calm' }} />,
  // @ts-expect-error подпись меню обязательна
  <RowActions actions={[]} />,
  // @ts-expect-error подпись полосы вкладок обязательна
  <Tabs items={[]} value="a" onChange={() => {}} />,
  // Цвет статуса — из словаря, а не любой.
  // @ts-expect-error тона «purple» нет
  <StatusBadge tone="purple">x</StatusBadge>,
  // Главная кнопка у IconButton не бывает: иконка без слов не может быть главным шагом.
  // @ts-expect-error variant primary недоступен
  <IconButton label="x" icon={<svg />} variant="primary" />,
  // В шапке не больше трёх счётчиков.
  <SectionHeader
    title="Раздел"
    // @ts-expect-error четвёртый счётчик
    counts={[{ label: 'a', value: 1 }, { label: 'b', value: 2 }, { label: 'c', value: 3 }, { label: 'd', value: 4 }]}
  />,
  // Второстепенных в инспекторе не больше трёх.
  <Inspector.Actions
    // @ts-expect-error четвёртая второстепенная
    secondary={[<Button key="1" />, <Button key="2" />, <Button key="3" />, <Button key="4" />]}
  />,
];
