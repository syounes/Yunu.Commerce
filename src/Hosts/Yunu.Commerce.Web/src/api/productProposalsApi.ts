import type {
  CreateProductProposalRequest,
  CreateProductProposalResponse,
  ProductProposal,
} from "../models/productProposal";

const baseUrl = (import.meta.env.VITE_YUNU_API_BASE_URL ?? "https://localhost:7241").replace(/\/$/, "");

export class ProductProposalApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly detail?: string,
  ) {
    super(message);
  }
}

async function parseError(response: Response): Promise<ProductProposalApiError> {
  const body = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
  const detail = body?.detail ?? body?.title ?? response.statusText;
  return new ProductProposalApiError(detail || "Não foi possível processar a solicitação.", response.status, detail);
}

export async function getProductProposal(
  proposalId: string,
  signal?: AbortSignal,
): Promise<ProductProposal> {
  const response = await fetch(`${baseUrl}/api/catalog/product-proposals/${proposalId}`, {
    method: "GET",
    headers: { Accept: "application/json" },
    signal,
  });

  if (!response.ok) throw await parseError(response);
  return response.json() as Promise<ProductProposal>;
}

export async function createProductProposal(
  request: CreateProductProposalRequest,
  signal?: AbortSignal,
): Promise<ProductProposal> {
  const response = await fetch(`${baseUrl}/api/catalog/product-proposals`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) throw await parseError(response);

  const created = await response.json() as CreateProductProposalResponse;

  // Suporta o contrato futuro que devolve a proposta completa no próprio 201.
  if (created.product && created.skus && created.source && created.resolution) {
    return {
      ...created,
      locale: request.locale,
      product: created.product,
      skus: created.skus,
      source: created.source,
      resolution: created.resolution,
    };
  }

  // Contrato atual: POST retorna somente o ID e o GET devolve a proposta completa.
  return getProductProposal(created.proposalId, signal);
}
