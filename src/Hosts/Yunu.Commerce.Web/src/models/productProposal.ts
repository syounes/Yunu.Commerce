export interface CreateProductProposalRequest {
  input: string;
  locale: string;
}

export interface CreateProductProposalResponse {
  proposalId: string;
  status: ProductProposalStatus;
  readyForReview: boolean;
  createdAtUtc: string;
  product?: ProposedProduct;
  skus?: ProposedSku[];
  source?: ProposalSource;
  resolution?: ProposalResolution;
}

export type ProductProposalStatus =
  | "AwaitingReview"
  | "Confirmed"
  | "Converted"
  | "Rejected"
  | "Failed";

export interface ProductProposal {
  proposalId: string;
  status: ProductProposalStatus;
  locale: string;
  source: ProposalSource;
  product: ProposedProduct;
  skus: ProposedSku[];
  resolution: ProposalResolution;
  createdAtUtc: string;
  updatedAtUtc?: string;
  confirmedAtUtc?: string | null;
  convertedAtUtc?: string | null;
  createdProductId?: string | null;
}

export interface ProposalSource {
  originalInput: string;
  normalizedQuery: string;
  semanticQuery: string;
  intent: string;
  detectedLanguage: string;
  targetLocale: string;
}

export interface ProposedProduct {
  suggestedName?: string | null;
  description?: string | null;
  brandId?: string | null;
  googleCategory: ProposedGoogleCategory;
}

export interface ProposedGoogleCategory {
  googleCategoryId: number;
  name: string;
  path: string;
  depth?: number | null;
  resolutionStrategy?: string | null;
  similarity?: number | null;
  rerankConfidence?: number | null;
}

export interface ProposedSku {
  id: string;
  suggestedCode?: string | null;
  gtin?: string | null;
  attributes: ProposedSkuAttribute[];
}

export interface ProposedSkuAttribute {
  attributeDefinitionId: number;
  attributeCode: string;
  attributeName: string;
  sequence: number;
  dataType: string;
  rawName: string;
  rawValue: string;
  normalizedValue: string;
  typedValue?: ProposedTypedValue | null;
  attributeOptionId?: number | null;
  optionCode?: string | null;
  optionName?: string | null;
  definitionResolutionStrategy?: string | null;
  optionResolutionStrategy?: string | null;
  definitionSimilarity?: number | null;
  valueSimilarity?: number | null;
  definitionRerankConfidence?: number | null;
  optionRerankConfidence?: number | null;
}

export interface ProposedTypedValue {
  displayValue?: string | null;
  textValue?: string | null;
  integerValue?: number | null;
  decimalValue?: number | null;
  booleanValue?: boolean | null;
  dateTimeValue?: string | null;
  moneyAmount?: number | null;
  currencyCode?: string | null;
  measurementValue?: number | null;
  unitCode?: string | null;
  jsonValue?: string | null;
}

export interface ProposalResolution {
  status: string;
  categoryResolved: boolean;
  allAttributesResolved: boolean;
  readyForProposal: boolean;
  intentConfidence?: number | null;
  warnings: string[];
}
