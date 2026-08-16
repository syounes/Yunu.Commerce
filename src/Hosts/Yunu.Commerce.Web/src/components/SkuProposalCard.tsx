import type { ProposedSku } from "../models/productProposal";
import { SkuAttributeItem } from "./SkuAttributeItem";

interface SkuProposalCardProps {
  sku: ProposedSku;
  index: number;
}

export function SkuProposalCard({ sku, index }: SkuProposalCardProps) {
  return (
    <article className="entity-card sku-card">
      <div className="card-accent sku-accent" />
      <div className="entity-header sku-header">
        <div className="entity-title">
          <div className="entity-icon sku-icon">▦</div>
          <div>
            <div className="label-row">
              <span className="entity-label">SKU {String(index + 1).padStart(2, "0")}</span>
              <span className="resolved-pill"><i /> {sku.attributes.length} atributos resolvidos</span>
            </div>
            <h3>{sku.suggestedCode || "Variação principal"}</h3>
            <p>Código e GTIN poderão ser informados durante a revisão.</p>
          </div>
        </div>
        <button className="secondary-button" type="button">Editar SKU</button>
      </div>

      <div className="sku-identifiers">
        <div><span>CÓDIGO DO SKU</span><strong>{sku.suggestedCode || "A definir"}</strong></div>
        <div><span>GTIN</span><strong>{sku.gtin || "Não informado"}</strong></div>
        <div><span>ID INTERNO DA PROPOSTA</span><code>{shortenId(sku.id)}</code></div>
      </div>

      <div className="attributes-section">
        <div className="section-title"><h4>Atributos do SKU</h4><span>Todos validados</span></div>
        <div className="attribute-grid">
          {sku.attributes.map((attribute) => (
            <SkuAttributeItem
              key={`${attribute.attributeDefinitionId}-${attribute.sequence}`}
              attribute={attribute}
            />
          ))}
        </div>
      </div>
    </article>
  );
}

function shortenId(id: string) {
  return id.length > 14 ? `${id.slice(0, 8)}…${id.slice(-4)}` : id;
}
