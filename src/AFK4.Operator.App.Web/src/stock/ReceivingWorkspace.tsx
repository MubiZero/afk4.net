import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, Minus, Plus, Search, X } from 'lucide-react';
import { useDeferredFlag } from '../useDeferredFlag';
import { EmptyState, Money } from '../operatorPrimitives';
import { StockSkeleton } from './StockSkeleton';
import { StockHero } from './StockHero';
import { ScanSearchBar } from './ScanSearchBar';
import { createAuthenticatedOperatorClients, createIdempotencyKey, readArray, readBoolean, readString, requireBackend } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { matchByBarcode } from '../barcodeScanner';
import { useBarcodeScanner } from '../useBarcodeScanner';
import { useToast } from '../operatorToast';
import type { PosProductDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  addOrAccumulate, removeLine, setQuantity, setUnitCostText,
  lineSubtotalMinorUnits, lineUnitCostMinorUnits, receiptTotals, receiptReason,
  type ReceiptLine,
} from './receivingModel';

// Проекция каталога с полем barcodes для matchByBarcode.
type TrackedProduct = PosProductDto & { barcodes: string[] };

type PostState = { kind: 'idle' } | { kind: 'posting' } | { kind: 'done'; count: number } | { kind: 'error'; detail: string };

export function ReceivingWorkspace({
  backend,
  currencyCode,
  session,
  preload,
  onConsumePreload,
  onStockChanged,
  refreshNonce = 0,
  active = true,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  preload: { productId: string } | null;
  onConsumePreload: () => void;
  onStockChanged?: () => void;
  refreshNonce?: number;
  active?: boolean;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const canManage = hasPermission(session, permissionNames.manageInventoryStock);

  const clients = useMemo(
    () => (backend && canManage ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canManage]
  );

  const [catalog, setCatalog] = useState<TrackedProduct[]>([]);
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
      .then((loaded) => {
        if (alive) setCatalog((loaded as PosProductDto[]).map((p) => ({ ...p, barcodes: readArray<string>(p, 'barcodes') })));
      })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canManage, refreshNonce]);

  const onScan = useCallback((code: string) => {
    const found = matchByBarcode(trackedCatalog, code);
    if (found) {
      setLines((cur) => addOrAccumulate(cur, found));
    } else {
      toast.info(t('op.pos.scan.unknown'));
    }
  }, [trackedCatalog, toast, t]);

  useBarcodeScanner(active && canManage && !loading, onScan);

  // Преднабор товара (переход с Остатков по ＋). Срабатывает один раз, когда каталог загружен.
  useEffect(() => {
    if (!preload || loading) return;
    const product = trackedCatalog.find((p) => readString(p, 'productId') === preload.productId);
    if (product) setLines((current) => addOrAccumulate(current, product));
    onConsumePreload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preload, loading, trackedCatalog]);

  const showSkeleton = useDeferredFlag(loading);

  if (!canManage) {
    return <section className="stock-receiving"><p className="workspace-error">{t('op.stock.receiving.noPermission')}</p></section>;
  }
  if (loading && catalog.length === 0) {
    return showSkeleton
      ? <StockSkeleton sectionClass="stock-receiving" label={t('op.stock.receiving.loading')} />
      : <div className="stock-layout" />;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-receiving"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const query = search.trim().toLowerCase();
  const results = query
    ? trackedCatalog.filter((p) => readString(p, 'name').toLowerCase().includes(query) || readString(p, 'sku').toLowerCase().includes(query)).slice(0, 6)
    : [];

  const addProduct = (product: TrackedProduct) => {
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
      onStockChanged?.();
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
        <ScanSearchBar
          icon={<Search size={14} aria-hidden="true" />}
          value={search}
          onChange={setSearch}
          placeholder={t('op.stock.receiving.search')}
          ariaLabel={t('op.stock.receiving.addLabel')}
          hint={t('op.stock.receiving.addHint')}
        />
        {trackedCatalog.length === 0 && (
          <p className="recv-noresults">{t('op.stock.receiving.noTracked')}</p>
        )}
        {query && trackedCatalog.length > 0 && (
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
            <EmptyState icon={<Boxes size={28} aria-hidden="true" />} title={t('op.stock.receiving.empty')} />
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
                      <button type="button" aria-label="−" onClick={() => setLines((c) => setQuantity(c, line.productId, line.quantity - 1))}><Minus size={14} aria-hidden="true" /></button>
                      <input
                        inputMode="numeric"
                        aria-label={t('op.stock.receiving.colQty')}
                        value={String(line.quantity)}
                        onChange={(event) => {
                          const next = Number(event.currentTarget.value);
                          if (Number.isFinite(next)) setLines((c) => setQuantity(c, line.productId, next));
                        }}
                      />
                      <button type="button" aria-label="+" onClick={() => setLines((c) => setQuantity(c, line.productId, line.quantity + 1))}><Plus size={14} aria-hidden="true" /></button>
                    </div>
                    <div className="recv-cost">
                      <input
                        inputMode="decimal"
                        aria-label={t('op.stock.receiving.colCost')}
                        value={line.unitCostText}
                        onChange={(event) => {
                          const text = event.currentTarget.value;
                          setLines((c) => setUnitCostText(c, line.productId, text));
                        }}
                      />
                      <span className="recv-cost-cur">{currencyCode}</span>
                    </div>
                    <div className="recv-sum"><Money minorUnits={lineSubtotalMinorUnits(line)} currencyCode={currencyCode} /></div>
                    <button type="button" className="ui-btn ui-btn--sm ui-btn--danger" aria-label={t('op.stock.receiving.remove')} onClick={() => setLines((c) => removeLine(c, line.productId))}>
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
        <section className="stock-section">
          <h3 className="ctx-title">{t('op.stock.receiving.invoiceTitle')}</h3>
          <label className="ui-field">
            <span>{t('op.stock.receiving.supplier')}</span>
            <input value={supplier} disabled={posting} onChange={(event) => setSupplier(event.currentTarget.value)} />
          </label>
          <label className="ui-field">
            <span>{t('op.stock.receiving.invoiceNo')}</span>
            <input value={invoiceNo} disabled={posting} placeholder={t('op.stock.receiving.invoiceNoHint')} onChange={(event) => setInvoiceNo(event.currentTarget.value)} />
          </label>
        </section>

        <StockHero
          label={t('op.stock.receiving.totalSum')}
          value={<Money minorUnits={totals.sumMinorUnits} currencyCode={currencyCode} />}
          tone={lines.length > 0 ? 'neutral' : 'muted'}
        />

        <section className="stock-section">
          <div className="mv"><span>{t('op.stock.receiving.totalPositions')}</span><b>{totals.positions}</b></div>
          <div className="mv"><span>{t('op.stock.receiving.totalUnits')}</span><b>{totals.units}</b></div>
          <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={lines.length === 0 || posting} onClick={postReceipt}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.receiving.posting') : t('op.stock.receiving.post')}
          </button>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.receiving.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </section>
      </aside>
    </div>
  );
}
