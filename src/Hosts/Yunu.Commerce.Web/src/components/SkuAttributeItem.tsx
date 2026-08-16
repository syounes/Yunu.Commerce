import type { ProposedSkuAttribute } from "../models/productProposal";

interface SkuAttributeItemProps {
  attribute: ProposedSkuAttribute;
}

export function SkuAttributeItem({ attribute }: SkuAttributeItemProps) {
  const kind = getKind(attribute.dataType);
  const value =
    attribute.optionName ||
    attribute.typedValue?.displayValue ||
    attribute.normalizedValue ||
    attribute.rawValue;

  return (
    <div className="attribute-item">
      <div className={`attribute-kind ${kind}`} aria-hidden="true">
        {kind === "enum" ? "⌄" : kind === "measure" ? "↔" : "Aa"}
      </div>
      <div><span>{attribute.attributeName}</span><strong>{value}</strong></div>
      <i className="attribute-check">✓</i>
    </div>
  );
}

function getKind(dataType: string) {
  if (dataType.toLowerCase() === "enum") return "enum";
  if (dataType.toLowerCase() === "measurement") return "measure";
  return "text";
}
