// Barrel for the operator API clients. Each domain lives in ./api/clients/<domain>.ts;
// shared primitives in ./api/types.ts. Re-exported here so existing imports
// (`from './operatorApiClients'`) keep working unchanged.
export * from './api/types';
export * from './api/clients';
export * from './api/clients/floorMap';
export * from './api/clients/sessions';
export * from './api/clients/pos';
export * from './api/clients/players';
export * from './api/clients/dashboard';
export * from './api/clients/reservations';
export * from './api/clients/shifts';
export * from './api/clients/shiftRevenue';
export * from './api/clients/settings';
export * from './api/clients/inventory';
export * from './api/clients/orgBilling';
export * from './api/clients/devices';
export * from './api/clients/diagnostics';
export * from './api/clients/updates';
export * from './api/clients/audit';
export * from './api/clients/moneyActions';
export * from './api/clients/account';
export * from './api/clients/shopOrders';
export * from './api/clients/loyaltySettings';
export * from './api/clients/eskhataConfig';
export * from './api/clients/news';
export * from './api/clients/media';
export * from './api/clients/dcTopUps';
export * from './api/clients/dcConfig';
