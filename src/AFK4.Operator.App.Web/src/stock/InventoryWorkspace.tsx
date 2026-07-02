import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, RotateCcw, ScanLine } from 'lucide-react';
import { useDeferredFlag } from '../useDeferredFlag';
import { EmptyState, Money } from '../operatorPrimitives';
import { StockSkeleton } from './StockSkeleton';
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
  mapCatalogToCountLines, setCounted, markFresh, resetCounts, markPosted,
  lineCounted, lineDiff, lineDiffSumMinorUnits, inventoryTotals, inventoryAdjustments,
  type CountLine,
} from './inventoryModel';

type TrackedProduct = PosProductDto & { barcodes: string[] };
type PostState = { kind: 'idle' } | { kind: 'posting' } | { kind: 'done'; count: number } | { kind: 'error'; detail: string };

export function InventoryWorkspace({
  backend,
  currencyCode,
  session,
  onStockChanged,
  refreshNonce = 0,
  active = true,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
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
  const [lines, setLines] = useState<CountLine[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [post, setPost] = useState<PostState>({ kind: 'idle' });
  const [reloadNonce, setReloadNonce] = useState(0);
  const inputRefs = useRef<Map<string, HTMLInputElement>>(new Map());

  const trackedCatalog = useMemo(() => catalog.filter((p) => readBoolean(p, 'trackStock')), [catalog]);

  useEffect(() => {
    if (!canManage || clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    clients.pos.getCatalog(backend.branchId)
      .then((loaded) => {
        if (!alive) return;
        const projected = (loaded as PosProductDto[]).map((p) => ({ ...p, barcodes: readArray<string>(p, 'barcodes') }));
        setCatalog(projected);
        setLines(mapCatalogToCountLines(projected));
        setPost({ kind: 'idle' });
      })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canManage, reloadNonce, refreshNonce]);

  const onScan = useCallback((code: string) => {
    const found = matchByBarcode(trackedCatalog, code);
    if (!found) { toast.info(t('op.pos.scan.unknown')); return; }
    const productId = readString(found, 'productId');
    setLines((cur) => markFresh(cur, productId));
    setSearch(''); // снять фильтр, чтобы отсканированная строка точно была видна и подсвечена
    const el = inputRefs.current.get(productId);
    if (el) { el.focus(); el.scrollIntoView?.({ block: 'nearest' }); el.select?.(); }
  }, [trackedCatalog, toast, t]);

  useBarcodeScanner(active && canManage && !loading, onScan);

  const showSkeleton = useDeferredFlag(loading);

  if (!canManage) {
    return <section className="stock-inventory"><p className="workspace-error">{t('op.stock.inventory.noPermission')}</p></section>;
  }
  if (loading && lines.length === 0) {
    return showSkeleton
      ? <StockSkeleton sectionClass="stock-inventory" label={t('op.stock.inventory.loading')} />
      : <div className="stock-layout" />;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-inventory"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const totals = inventoryTotals(lines);
  const adjustments = inventoryAdjustments(lines);
  const posting = post.kind === 'posting';
  const pct = totals.trackedCount === 0 ? 0 : Math.round((totals.countedCount / totals.trackedCount) * 100);
  const unit = t('op.stock.col.unit');
  const hasCounts = lines.some((l) => lineCounted(l) !== null);

  const query = search.trim().toLowerCase();
  const visibleLines = query
    ? lines.filter((l) => l.name.toLowerCase().includes(query) || l.sku.toLowerCase().includes(query))
    : lines;

  const signedUnits = (value: number) => (value > 0 ? `+${value}` : String(value));

  const postInventory = async () => {
    if (adjustments.length === 0 || posting) return;
    const nextBackend = requireBackend(backend, t);
    const api = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    const reason = t('op.stock.inventory.reasonBase');
    setPost({ kind: 'posting' });
    let posted = 0;
    try {
      for (const adj of adjustments) {
        await api.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: adj.productId,
          movementType: 'adjustment',
          quantityDelta: adj.quantityDelta,
          unitCost: { currencyCode, minorUnits: adj.unitCostMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create'),
        });
        // Учётный := факт, чтобы ретрай при сбое не провёл строку дважды.
        setLines((cur) => markPosted(cur, adj.productId));
        posted += 1;
      }
      setPost({ kind: 'done', count: posted });
      toast.info(t('op.stock.inventory.posted', { count: posted })); // переживёт сброс post-состояния при рефетче
      setReloadNonce((n) => n + 1); // свежие учётные остатки + сброс пересчёта
      onStockChanged?.(); // обновить метрики в шапке раздела
    } catch (error) {
      setPost(posted > 0
        ? { kind: 'error', detail: t('op.stock.inventory.partial', { posted, total: adjustments.length }) }
        : { kind: 'error', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <div className="stock-layout">
      <section className="stock-inventory">
        <div className="inv-scanbar">
          <span className="inv-scanbar-ico"><ScanLine size={18} aria-hidden="true" /></span>
          <span className="inv-scanbar-lbl" aria-label={t('op.pos.scan.active')}>
            {t('op.stock.inventory.scanHint')}<i className="inv-caret" aria-hidden="true" />
          </span>
          <input
            className="inv-search"
            type="search"
            aria-label={t('op.stock.inventory.search')}
            placeholder={t('op.stock.inventory.search')}
            value={search}
            onChange={(event) => setSearch(event.currentTarget.value)}
          />
          {hasCounts && (
            <button type="button" className="ui-btn ui-btn--sm ui-btn--ghost" disabled={posting} onClick={() => setLines((c) => resetCounts(c))}>
              <RotateCcw size={13} aria-hidden="true" />
              {t('op.stock.inventory.reset')}
            </button>
          )}
        </div>

        <div className="recv-doc" aria-label={t('op.stock.inventory.title')}>
          <h2>{t('op.stock.inventory.title')}</h2>
          {lines.length === 0 ? (
            <EmptyState icon={<Boxes size={28} aria-hidden="true" />} title={t('op.stock.inventory.empty')} />
          ) : (
            <>
              <div className="inv-cols" aria-hidden="true">
                <span />
                <span>{t('op.stock.inventory.colItem')}</span>
                <span className="r">{t('op.stock.inventory.colSystem')}</span>
                <span className="r">{t('op.stock.inventory.colFact')}</span>
                <span className="r">{t('op.stock.inventory.colDiff')}</span>
                <span className="r">{t('op.stock.inventory.colSum')}</span>
              </div>
              <ul className="inv-lines">
                {visibleLines.length === 0 && (
                  <li className="recv-noresults">{t('op.stock.levels.emptyFiltered')}</li>
                )}
                {visibleLines.map((line) => {
                  const diff = lineDiff(line);
                  const sum = lineDiffSumMinorUnits(line);
                  const pending = diff === null;
                  const hasDiff = diff !== null && diff !== 0;
                  const diffClass = pending ? 'none' : diff === 0 ? 'zero' : diff < 0 ? 'minus' : 'plus';
                  return (
                    <li key={line.productId} className={`inv-row${line.fresh ? ' fresh' : ''}${hasDiff ? ' diff' : ''}${pending ? ' pending' : ''}`}>
                      <Boxes size={15} aria-hidden="true" />
                      <div className="inv-name">
                        <strong>{line.name}</strong>
                        <em>{line.sku}</em>
                      </div>
                      <div className="inv-sys">{line.systemQty}</div>
                      <div className={`inv-fact${pending ? ' empty' : ''}`}>
                        <input
                          inputMode="numeric"
                          aria-label={`${t('op.stock.inventory.colFact')}: ${line.name}`}
                          placeholder="—"
                          value={line.countedText}
                          ref={(el) => { if (el) inputRefs.current.set(line.productId, el); else inputRefs.current.delete(line.productId); }}
                          onChange={(event) => { const v = event.currentTarget.value; setLines((c) => setCounted(c, line.productId, v)); }}
                        />
                      </div>
                      <div className={`inv-diff ${diffClass}`}>
                        {pending ? t('op.stock.inventory.notCounted') : diff === 0 ? '0' : signedUnits(diff)}
                      </div>
                      <div className={`inv-sum ${diffClass}`}>
                        {pending || diff === 0
                          ? '—'
                          : <Money minorUnits={sum} currencyCode={currencyCode} signed />}
                      </div>
                    </li>
                  );
                })}
              </ul>
            </>
          )}
        </div>
      </section>

      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.inventory.progressTitle')}</h3>
          <div className="inv-prog"><i style={{ width: `${pct}%` }} /></div>
          <div className="inv-progtxt"><span>{t('op.stock.inventory.counted')}</span><b>{totals.countedCount} / {totals.trackedCount}</b></div>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.inventory.totalTitle')}</h3>
          <div className="mv"><span>{t('op.stock.inventory.discrepancies')}</span><b>{totals.discrepancies}</b></div>
          <div className="mv"><span>{t('op.stock.inventory.shortage')}</span><b className="warning-text">-{totals.shortageUnits} {unit} · <Money minorUnits={totals.shortageSumMinorUnits} currencyCode={currencyCode} /></b></div>
          <div className="mv"><span>{t('op.stock.inventory.surplus')}</span><b className="inv-pos">+{totals.surplusUnits} {unit} · <Money minorUnits={totals.surplusSumMinorUnits} currencyCode={currencyCode} /></b></div>
          <div className="mv recv-grand"><span>{t('op.stock.inventory.netCost')}</span><b className={totals.netSumMinorUnits < 0 ? 'warning-text' : totals.netSumMinorUnits > 0 ? 'inv-pos' : undefined}><Money minorUnits={totals.netSumMinorUnits} currencyCode={currencyCode} signed={totals.netSumMinorUnits !== 0} /></b></div>
          <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={adjustments.length === 0 || posting} onClick={postInventory}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.inventory.posting') : t('op.stock.inventory.post')}
          </button>
          <p className="ctx-note">{t('op.stock.inventory.willCreate', { count: adjustments.length })}</p>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.inventory.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </div>
      </aside>
    </div>
  );
}
