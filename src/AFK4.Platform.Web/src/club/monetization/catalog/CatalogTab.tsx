import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useCatalog } from './useCatalog';
import { CategoryCreateDialog } from './CategoryCreateDialog';
import { ProductFormDialog } from './ProductFormDialog';
import { deriveCategories, type CategoryOption, type ProductRow } from './catalogModel';

type Client = Pick<ClubApiClient, 'getCatalog' | 'createProductCategory' | 'createProduct' | 'updateProduct'>;

export function CatalogTab({ client, branchId, organizationId, canManage }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canManage: boolean;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = useCatalog(client, branchId);
  const [sessionCategories, setSessionCategories] = useState<CategoryOption[]>([]);
  const [creatingCategory, setCreatingCategory] = useState(false);
  const [creatingProduct, setCreatingProduct] = useState(false);
  const [editing, setEditing] = useState<ProductRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, retry } = state;
  const categories = deriveCategories(rows.map(r => r.categoryId), sessionCategories, t('products.categoryUnknown'));
  const categoryName = (id: string) => categories.find(c => c.categoryId === id)?.name ?? id;

  function onCategoryCreated(category: CategoryOption) {
    setSessionCategories(prev => [...prev.filter(c => c.categoryId !== category.categoryId), category]);
    retry();
  }

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => setCreatingCategory(true)}>{t('products.createCategory')}</Button>
          <Button onClick={() => setCreatingProduct(true)}>{t('products.create')}</Button>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState message={t('products.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('products.col.category')}</TableHead>
              <TableHead>{t('products.col.name')}</TableHead>
              <TableHead>{t('products.col.sku')}</TableHead>
              <TableHead>{t('products.col.price')}</TableHead>
              <TableHead>{t('products.col.stock')}</TableHead>
              <TableHead>{t('products.col.status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.productId} data-clickable={canManage ? 'true' : undefined}
                onClick={canManage ? () => setEditing(row) : undefined}>
                <TableCell className="text-sm text-muted-foreground">{categoryName(row.categoryId)}</TableCell>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="text-sm">{row.sku}</TableCell>
                <TableCell className="tabular-nums">{formatCurrency(row.price, row.currencyCode)}</TableCell>
                <TableCell className="tabular-nums">{row.trackStock ? formatNumber(row.stockOnHand) : '—'}</TableCell>
                <TableCell>
                  <Badge variant={row.isActive ? 'default' : 'secondary'}>
                    {row.isActive ? t('products.status.active') : t('products.status.inactive')}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('products.categoryNote')}</p>

      {creatingCategory && (
        <CategoryCreateDialog
          open branchId={branchId} organizationId={organizationId} client={client}
          onCreated={onCategoryCreated}
          onOpenChange={o => { if (!o) setCreatingCategory(false); }}
        />
      )}
      {creatingProduct && (
        <ProductFormDialog
          open mode="create" branchId={branchId} organizationId={organizationId} client={client} categories={categories}
          onOpenChange={o => { if (!o) setCreatingProduct(false); }}
          onDone={() => retry()}
        />
      )}
      {editing !== null && (
        <ProductFormDialog
          key={editing.productId}
          open mode="edit" branchId={branchId} organizationId={organizationId} client={client} categories={categories} initial={editing}
          onOpenChange={o => { if (!o) setEditing(null); }}
          onDone={() => retry()}
        />
      )}
    </div>
  );
}
