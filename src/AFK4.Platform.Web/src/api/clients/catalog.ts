import type { ApiTransport } from '../apiTransport';
import type {
  CreateProductCategoryRequest,
  CreateProductRequest,
  PosProduct,
  PosProductCategory,
  UpdateProductRequest
} from '../types';

export class CatalogApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getCatalog(branchId: string): Promise<PosProduct[]> {
    return this.transport.send<PosProduct[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/pos/catalog`);
  }

  public createProductCategory(branchId: string, request: CreateProductCategoryRequest): Promise<PosProductCategory> {
    return this.transport.send<PosProductCategory>('POST', `/api/branches/${encodeURIComponent(branchId)}/pos/categories`, request);
  }

  public createProduct(branchId: string, request: CreateProductRequest): Promise<PosProduct> {
    return this.transport.send<PosProduct>('POST', `/api/branches/${encodeURIComponent(branchId)}/pos/products`, request);
  }

  public updateProduct(branchId: string, productId: string, request: UpdateProductRequest): Promise<PosProduct> {
    return this.transport.send<PosProduct>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/pos/products/${encodeURIComponent(productId)}`,
      request
    );
  }
}
