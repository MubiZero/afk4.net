import type { BrandingHallDto } from '@/api/types';

/**
 * Зал сети, в который придёт игрок.
 *
 * Спрашивается один раз и только до первого действия в клубе: счёт человеку открывает бронь или
 * пополнение, и у сети с несколькими залами сервер не гадает, в каком именно, — иначе человек
 * пришёл бы в один зал, а его кошелёк и история оказались бы в другом. У игрока со счётом зал
 * уже записан, и названный заново его не переписывает: там спрашивать нечего.
 *
 * То же правило, что в мобильном приложении (`organization/branch_choice.dart`): два ответа на
 * «в каком вы зале» разъехались бы на первой же правке.
 */
export interface BranchChoice {
  /** Залы, из которых есть что выбрать. Пусто — вопроса нет. */
  halls: BrandingHallDto[];
  /** Что ответил игрок, или null — ещё не отвечал. */
  chosenId: string | null;
  /** Зал, который поедет на сервер. null — назвать нечего, запрос уходит как раньше. */
  branchId: string | null;
  /** Спрашивать ли игрока. Один зал в сети — не выбор, а данность. */
  asks: boolean;
  /** Вопрос задан и ещё не отвечен: действие сейчас обернулось бы отказом сервера. */
  unanswered: boolean;
}

export function branchChoice(
  halls: BrandingHallDto[],
  chosenId: string | null,
  // Зал счёта, если он у человека в этом клубе уже есть. Тогда выбирать нечего и не нужно.
  accountBranchId: string | null
): BranchChoice {
  if (accountBranchId !== null) {
    return { halls: [], chosenId: null, branchId: accountBranchId, asks: false, unanswered: false };
  }

  const only = halls.length === 1 ? halls[0].branchId : null;
  const branchId = chosenId ?? only;
  const asks = halls.length > 1;
  return { halls, chosenId, branchId, asks, unanswered: asks && chosenId === null };
}
