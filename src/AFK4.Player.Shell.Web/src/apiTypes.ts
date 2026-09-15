// Формы сетевых DTO живут в `@afk4/contracts` — они генерируются из AFK4.Shared.Contracts.
// Здесь только реэкспорт, чтобы существующие импорты `./apiTypes` продолжали работать.

export type {
  CashbackEntryDto,
  ExtendSessionRequest,
  MoneyDto,
  PackageOptionDto,
  PlaceShopOrderRequest,
  PlayerLoyaltyDto,
  PlayerNewsItemDto,
  PlayerTopUpIntentDto,
  ShopCatalogItemDto,
  ShopOrderDto,
  ShopOrderLineDto,
  ShopOrderLineInput,
  TariffOptionDto,
} from '@afk4/contracts';
