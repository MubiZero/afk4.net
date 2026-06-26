import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, Plus, X } from 'lucide-react';
import { createAuthenticatedOperatorClients, createIdempotencyKey, readBoolean, readString, requireBackend } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { PosProductDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  addOrAccumulate, removeLine, setQuantity, setUnitCostText,
  lineSubtotalMinorUnits, lineUnitCostMinorUnits, receiptTotals, receiptReason,
  type ReceiptLine,
} from './receivingModel';

type PostState = { kind: 'idle' } | { kind: 'posting' } | { kind: 'done'; count: number } | { kind: 'error'; detail: string };

export function ReceivingWorkspace({
  backend,
  currencyCode,
  session,
  preload,
  onConsumePreload,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  preload: { productId: string } | null;
  onConsumePreload: () => void;
}) {
  const { t } = useI18n();
  const canManage = hasPermission(session, permissionNames.manageInventoryStock);

  const clients = useMemo(
    () => (backend && canManage ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canManage]
  );

  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [lines, setLines] = useState<ReceiptLine[]>([]);
  const [search, setSearch] = useState('');
  const [supplier, setSupplier] = useState('');
  const [invoiceNo, setInvoiceNo] = useState('');
  const [post, setPost] = useState<PostState>({ kind: 'idle' });

  // Только товары с учётом остатка — приходовать имеет смысл только их.
  const trackedCatalog = useMemo(() => catalog.filter((p) => readBoolean(p, 'trackStock')), [catalog]);

  useEffect(() => {
    if (!canManage || clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    clients.pos.getCatalog(backend.branchId)
      .then((loaded) => { if (alive) setCatalog(loaded as PosProductDto[]); })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canManage]);

  // Преднабор товара (переход с Остатков по ＋). Срабатывает один раз, когда каталог загружен.
  useEffect(() => {
    if (!preload || loading) return;
    const product = trackedCatalog.find((p) => readString(p, 'productId') === preload.productId);
    if (product) setLines((current) => addOrAccumulate(current, product));
    onConsumePreload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preload, loading, trackedCatalog]);

  if (!canManage) {
    return <section className="stock-receiving"><p className="workspace-error">{t('op.stock.receiving.noPermission')}</p></section>;
  }
  if (loading) {
    return <div className="stock-layout"><section className="stock-receiving"><p className="workspace-loading">{t('op.stock.receiving.loading')}</p></section></div>;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-receiving"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const query = search.trim().toLowerCase();
  const results = query
    ? trackedCatalog.filter((p) => readString(p, 'name').toLowerCase().includes(query) || readString(p, 'sku').toLowerCase().includes(query)).slice(0, 6)
    : [];

  const addProduct = (product: PosProductDto) => {
    setLines((current) => addOrAccumulate(current, product));
    setSearch('');
    setPost({ kind: 'idle' });
  };

  const totals = receiptTotals(lines);

  const postReceipt = async () => {
    if (lines.length === 0 || post.kind === 'posting') return;
    const nextBackend = requireBackend(backend, t);
    const api = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    const reason = receiptReason(t('op.stock.receiving.reasonBase'), supplier, invoiceNo);
    setPost({ kind: 'posting' });
    const remaining = [...lines];
    let posted = 0;
    try {
      for (const line of lines) {
        await api.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: line.productId,
          movementType: 'purchase',
          quantityDelta: line.quantity,
          unitCost: { currencyCode, minorUnits: lineUnitCostMinorUnits(line) },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create'),
        });
        remaining.shift();
        posted += 1;
      }
      setLines([]);
      setSupplier('');
      setInvoiceNo('');
      setPost({ kind: 'done', count: posted });
    } catch (error) {
      setLines(remaining);
      setPost(posted > 0
        ? { kind: 'error', detail: t('op.stock.receiving.partial', { posted, total: lines.length }) }
        : { kind: 'error', detail: projectOperatorError(error, t).detail });
    }
  };

  const posting = post.kind === 'posting';

  return (
    <div className="stock-layout">
      {/* ── Документ прихода ── */}
      <section className="stock-receiving">
        {/* Полоса добавления товара (в S3 сюда подключится сканер) */}
        <div className="recv-add">
          <div className="recv-add-ico"><Boxes size={20} aria-hidden="true" /></div>
          <div className="recv-add-field">
            <input
              type="search"
              aria-label={t('op.stock.receiving.addLabel')}
              placeholder={t('op.stock.receiving.search')}
              value={search}
              onChange={(event) => setSearch(event.currentTarget.value)}
            />
            <span className="recv-add-hint">{t('op.stock.receiving.addHint')}</span>
          </div>
        </div>
        {trackedCatalog.length === 0 && (
          <p className="recv-noresults">{t('op.stock.receiving.noTracked')}</p>
        )}
        {query && (
          <ul className="recv-results">
            {results.length === 0
              ? <li className="recv-noresults">{t('op.stock.receiving.noResults')}</li>
              : results.map((product) => (
                <li key={readString(product, 'productId')}>
                  <button type="button" onClick={() => addProduct(product)}>
                    <Plus size={14} aria-hidden="true" />
                    <strong>{readString(product, 'name')}</strong>
                    <em>{readString(product, 'sku')}</em>
                  </button>
                </li>
              ))}
          </ul>
        )}

        <div className="recv-doc" aria-label={t('op.stock.receiving.linesTitle')}>
          <h2>{t('op.stock.receiving.linesTitle')}</h2>
          {lines.length === 0 ? (
            <p className="cash-shift-empty-note">{t('op.stock.receiving.empty')}</p>
          ) : (
            <>
              <div className="recv-cols" aria-hidden="true">
                <span />
                <span>{t('op.stock.receiving.colItem')}</span>
                <span>{t('op.stock.receiving.colQty')}</span>
                <span className="r">{t('op.stock.receiving.colCost')}</span>
                <span className="r">{t('op.stock.receiving.colSum')}</span>
                <span />
              </div>
              <ul className="recv-lines">
                {lines.map((line) => (
                  <li key={line.productId} className={`recv-row${line.fresh ? ' fresh' : ''}`}>
                    <Boxes size={15} aria-hidden="true" />
                    <div className="recv-name">
                      <strong>{line.name}</strong>
                      <em>{line.sku}</em>
                    </div>
                    <div className="recv-step">
                      <button type="button" aria-label="−" onClick={() => setLines((c) => setQuantity(c, line.productId, line.quantity - 1))}>−</button>
                      <input
                        inputMode="numeric"
                        aria-label={t('op.stock.receiving.colQty')}
                        value={String(line.quantity)}
                        onChange={(event) => {
                          const next = Number(event.currentTarget.value);
                          if (Number.isFinite(next)) setLines((c) => setQuantity(c, line.productId, next));
                        }}
                      />
                      <button type="button" aria-label="+" onClick={() => setLines((c) => setQuantity(c, line.productId, line.quantity + 1))}>+</button>
                    </div>
                    <div className="recv-cost">
                      <input
                        inputMode="decimal"
                        aria-label={t('op.stock.receiving.colCost')}
                        value={line.unitCostText}
                        onChange={(event) => setLines((c) => setUnitCostText(c, line.productId, event.currentTarget.value))}
                      />
                      <span className="recv-cost-cur">{currencyCode}</span>
                    </div>
                    <div className="recv-sum">{formatMinorUnits(lineSubtotalMinorUnits(line), currencyCode)}</div>
                    <button type="button" className="recv-del" aria-label={t('op.stock.receiving.remove')} onClick={() => setLines((c) => removeLine(c, line.productId))}>
                      <X size={14} aria-hidden="true" />
                    </button>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      </section>

      {/* ── Накладная (правая колонка) ── */}
      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.receiving.invoiceTitle')}</h3>
          <label className="recv-field">
            <span>{t('op.stock.receiving.supplier')}</span>
            <input value={supplier} disabled={posting} onChange={(event) => setSupplier(event.currentTarget.value)} />
          </label>
          <label className="recv-field">
            <span>{t('op.stock.receiving.invoiceNo')}</span>
            <input value={invoiceNo} disabled={posting} placeholder={t('op.stock.receiving.invoiceNoHint')} onChange={(event) => setInvoiceNo(event.currentTarget.value)} />
          </label>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.receiving.totalTitle')}</h3>
          <div className="mv"><span>{t('op.stock.receiving.totalPositions')}</span><b>{totals.positions}</b></div>
          <div className="mv"><span>{t('op.stock.receiving.totalUnits')}</span><b>{totals.units}</b></div>
          <div className="mv recv-grand"><span>{t('op.stock.receiving.totalSum')}</span><b>{formatMinorUnits(totals.sumMinorUnits, currencyCode)}</b></div>
          <button type="button" className="ctx-btn" disabled={lines.length === 0 || posting} onClick={postReceipt}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.receiving.posting') : t('op.stock.receiving.post')}
          </button>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.receiving.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </div>
      </aside>
    </div>
  );
}
