import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseContracts } from '../scripts/parse.ts';

/**
 * Генератор умеет ошибаться молча: пустой разбор даст пустой файл, а промах в комментарии —
 * тип без половины полей. Обе ошибки выглядят как успешный прогон, поэтому проверяется и
 * разбор, и вывод.
 */
const repoRoot = join(import.meta.dir, '..', '..', '..');
const contracts = parseContracts(join(repoRoot, 'src', 'AFK4.Shared.Contracts'));
const generated = readFileSync(join(import.meta.dir, 'contracts.ts'), 'utf8');

describe('разбор контрактов', () => {
  it('находит записи, а не пустоту', () => {
    expect(contracts.length).toBeGreaterThan(400);
  });

  it('читает и параметры записи, и поля из её тела', () => {
    // `CreateProductRequest` объявлен не позиционной записью, а телом со свойствами.
    const request = contracts.find((record) => record.name === 'CreateProductRequest')!;
    expect(request.fields.map((field) => field.name)).toContain('availableInShell');

    // `AuditRecordDto` объявляет два последних поля свойствами рядом с позиционными.
    const audit = contracts.find((record) => record.name === 'AuditRecordDto')!;
    expect(audit.fields.map((field) => field.name)).toContain('amountMinorUnits');
  });

  /**
   * Комментарий над параметром полон запятых, и разрез списка по ним рвал его на осколки фраз:
   * «за что. Цена — за» становилось именем типа. Тип поля от соседнего комментария не зависит.
   */
  it('не путает комментарий внутри списка параметров с самими параметрами', () => {
    const session = contracts.find((record) => record.name === 'ActiveSessionDto')!;
    const tariff = session.fields.find((field) => field.name === 'tariffName')!;
    expect(tariff.type).toBe('string');
    expect(tariff.nullable).toBe(true);
    expect(tariff.doc.join(' ')).toContain('врать');
    // Хвостовой комментарий поясняет параметр, закончившийся на той же строке. Сверяется
    // вхождение, а не весь текст: объяснение над параметром дописывают, и проверка, требующая
    // его слово в слово, ломается на каждой правке комментария, ничего при этом не защищая.
    const duration = session.fields.find((field) => field.name === 'durationMode')!;
    expect(duration.type).toBe('string');
    expect(duration.doc).toContain('"open" | "fixed"');
  });

  it('снимает пространство имён с полного имени типа', () => {
    const referenced = contracts.flatMap((record) => record.fields).map((field) => field.type);
    expect(referenced.some((type) => type.includes('AFK4.Shared.Contracts'))).toBe(false);
  });

  it('отличает необязательное поле от просто обнуляемого', () => {
    const row = contracts.find((record) => record.name === 'CashOperationReportRowDto')!;
    // Значение по умолчанию есть — поле можно не присылать.
    expect(row.fields.find((field) => field.name === 'createdByDisplayName')!.optional).toBe(true);
    // Обнуляемое, но присылается всегда.
    expect(row.fields.find((field) => field.name === 'shiftId')).toMatchObject({ nullable: true, optional: false });
  });
});

describe('сгенерированный файл', () => {
  it('несёт тип на каждую запись, которая ходит по сети', () => {
    const emitted = [...generated.matchAll(/^export interface ([A-Za-z0-9_]+)/gm)].map((match) => match[1]);
    expect(emitted.length).toBeGreaterThan(400);
    for (const name of ['MoneyDto', 'PosSaleDto', 'SessionDto', 'ShiftReportRowDto', 'AuditRecordDto']) {
      expect(emitted).toContain(name);
    }
  });

  // Служебные записи лежат в общих контрактах, но по сети не ходят. Их отсутствие — решение,
  // записанное в генераторе с объяснением, а не случайный пропуск.
  it('не тащит наружу внутренний интерфейс сервера', () => {
    expect(generated).not.toContain('export interface NotificationRequest ');
    expect(generated).not.toContain('export interface NotificationAttachment ');
  });

  it('не подменяет неизвестный тип на any', () => {
    expect(generated).not.toContain(': any');
    // Тип `unknown`, а не слово: код `warning-reason-unknown` из словаря — это данные.
    expect(generated).not.toMatch(/:\s*unknown\b/);
  });

  // Словари кодов доезжают до клиентов вместе с записями, и поле, помеченное словарём, не
  // принимает кода, которого сервер не присылает: «Посадить за ПК» сравнивало состояние места с
  // «free», а сервер пишет «Free» (#423).
  // В Dart у языка нет объединений строк, поэтому словарь — класс констант: приложение игрока
  // сравнивает с ReservationStateNames.pending, и опечатка в имени — ошибка компиляции.
  it('выпускает словари кодов и в Dart — классом констант', () => {
    const dart = readFileSync(join(import.meta.dir, '..', '..', '..', 'src', 'afk4_customer_app', 'lib', 'api', 'contracts.dart'), 'utf8');
    expect(dart).toContain("abstract final class SeatStateNames {\n  static const String free = 'Free';");
    expect(dart).toContain("static const String noShow = 'no_show';");
    // Зарезервированное слово Dart получает подчёркивание.
    expect(dart).toContain("static const String void_ = 'void';");
  });

  it('выпускает словари кодов и типизирует ими помеченные поля', () => {
    expect(generated).toContain("export const SeatStateNames = {\n  Free: 'Free',");
    expect(generated).toContain('export type SeatStateName = (typeof SeatStateNames)[keyof typeof SeatStateNames];');
    expect(generated).toMatch(/export interface SeatStatusDto \{[^}]*\n  state: SeatStateName;/);
  });

  it('переносит объяснение из контракта в тип', () => {
    expect(generated).toContain('Кто провёл операцию. Журнал кассы отвечает на вопрос');
  });

  it('называет файл контракта, чтобы из типа был путь к источнику', () => {
    expect(generated).toContain('Контракт: Billing/MoneyDto.cs');
  });
});
