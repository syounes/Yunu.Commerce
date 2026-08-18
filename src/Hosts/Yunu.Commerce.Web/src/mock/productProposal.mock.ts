import type { ProductProposal } from "../models/productProposal";

export const productProposalMock: ProductProposal = {
  proposalId: "cf0bc3a3-f1e4-4b0c-898a-4c58045c3878",
  status: "AwaitingReview",
  locale: "pt-BR",
  createdAtUtc: "2026-08-16T19:43:07.1013676Z",
  source: {
    originalInput: "Quero cadastrar um microfone condensador com conexão USB, na cor preta, corpo fabricado em alumínio e condição novo. O produto é indicado para gravação de podcasts e uso em estúdio. O peso para cálculo do frete é 850 g. A embalagem possui 25 cm de comprimento, 15 cm de largura e 10 cm de altura.",
    normalizedQuery: "Quero cadastrar um microfone condensador com conexão USB, na cor preta, corpo fabricado em alumínio e condição novo.",
    semanticQuery: "microfone condensador USB preto com corpo de alumínio, novo, para podcasts e estúdio",
    intent: "ProductCreation",
    detectedLanguage: "pt-BR",
    targetLocale: "pt-BR",
  },
  product: {
    suggestedName: "Microfone condensador USB",
    description: "Corpo em alumínio para podcasts e gravações em estúdio.",
    brandId: null,
    googleCategory: {
      googleCategoryId: 234,
      name: "Microfones",
      path: "Eletrônicos > Áudio > Componentes para equipamentos de áudio > Microfones",
      depth: 3,
      resolutionStrategy: "ExactMatch",
      similarity: 1,
      rerankConfidence: null,
    },
  },
  skus: [
    {
      id: "7e8cae4b-4c33-4ddd-bec8-8709fe6bea12",
      suggestedCode: null,
      gtin: null,
      attributes: [
        attribute(68, "feature", "Tipo", "tipo", "condensador", "condensador", "Text"),
        attribute(69, "connection_type", "Conexão", "tipo de conexão", "USB", "USB", "Enum", "USB", "USB"),
        attribute(14, "color", "Cor", "cor", "preta", "Preta", "Text"),
        attribute(18, "material", "Material", "material", "alumínio", "Alumínio", "Text"),
        attribute(23, "condition", "Condição", "estado", "novo", "Novo", "Enum", "NEW", "Novo"),
        attribute(66, "occasion", "Uso indicado", "uso", "gravação de podcasts e uso em estúdio", "Podcasts e estúdio", "Text"),
        measurement(37, "shipping_weight", "Peso para frete", "peso para entrega", 850, "g"),
        measurement(38, "shipping_length", "Comprimento", "comprimento da embalagem", 25, "cm"),
        measurement(39, "shipping_width", "Largura", "largura da embalagem", 15, "cm"),
        measurement(40, "shipping_height", "Altura", "altura da embalagem", 10, "cm"),
      ],
    },
  ],
  resolution: {
    status: "Resolved",
    categoryResolved: true,
    allAttributesResolved: true,
    readyForProposal: true,
    intentConfidence: 0.99,
    warnings: [],
  },
};

function attribute(
  attributeDefinitionId: number,
  attributeCode: string,
  attributeName: string,
  rawName: string,
  rawValue: string,
  normalizedValue: string,
  dataType: string,
  optionCode?: string,
  optionName?: string,
) {
  return {
    attributeDefinitionId,
    attributeCode,
    attributeName,
    sequence: 1,
    dataType,
    rawName,
    rawValue,
    normalizedValue,
    typedValue: dataType === "Text" ? { displayValue: normalizedValue, textValue: normalizedValue } : null,
    attributeOptionId: optionCode ? attributeDefinitionId * 100 : null,
    optionCode: optionCode ?? null,
    optionName: optionName ?? null,
    definitionResolutionStrategy: "ExactMatch",
    optionResolutionStrategy: optionCode ? "ExactMatch" : null,
    definitionSimilarity: 1,
    valueSimilarity: optionCode ? 1 : null,
    definitionRerankConfidence: null,
    optionRerankConfidence: null,
  };
}

function measurement(
  attributeDefinitionId: number,
  attributeCode: string,
  attributeName: string,
  rawName: string,
  value: number,
  unitCode: string,
) {
  const displayValue = `${value} ${unitCode}`;
  return {
    attributeDefinitionId,
    attributeCode,
    attributeName,
    sequence: 1,
    dataType: "Measurement",
    rawName,
    rawValue: displayValue,
    normalizedValue: displayValue,
    typedValue: { displayValue, measurementValue: value, unitCode },
    attributeOptionId: null,
    optionCode: null,
    optionName: null,
    definitionResolutionStrategy: "ExactMatch",
    optionResolutionStrategy: null,
    definitionSimilarity: 1,
    valueSimilarity: null,
    definitionRerankConfidence: null,
    optionRerankConfidence: null,
  };
}
