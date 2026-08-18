import type { ProductProposal } from "../models/productProposal";

interface ProductProposalCardProps {
  proposal: ProductProposal;
}

export function ProductProposalCard({ proposal }: ProductProposalCardProps) {
  const { product } = proposal;
  const category = product.googleCategory;
  const confidence = category.rerankConfidence ?? category.similarity;

  return (
    <article className="entity-card product-card">
      <div className="card-accent product-accent" />
      <div className="entity-header">
        <div className="entity-title">
          <div className="entity-icon product-icon">◇</div>
          <div>
            <div className="label-row">
              <span className="entity-label">PRODUTO</span>
              <span className="status-pill">Aguardando revisão</span>
            </div>
            <h3>{product.suggestedName || category.name}</h3>
            <p>{product.description || "Nome e descrição poderão ser refinados durante a revisão."}</p>
          </div>
        </div>
        <button className="secondary-button" type="button">Editar detalhes</button>
      </div>

      <div className="classification-box">
        <div className="taxonomy-mark">G</div>
        <div className="classification-main">
          <span>CATEGORIA GOOGLE</span><strong>{category.name}</strong>
          <p>{category.path.split(" > ").map((item, index) => <span key={item}>{index > 0 && <b>›</b>}{item}</span>)}</p>
        </div>
        <div className="classification-stats">
          <div><span>ID</span><strong>{category.googleCategoryId}</strong></div>
          <div><span>ESTRATÉGIA</span><strong>{formatStrategy(category.resolutionStrategy)}</strong></div>
          <div><span>CONFIANÇA</span><strong className="confidence">{formatPercent(confidence)}</strong></div>
        </div>
      </div>

      <div className="metadata-row">
        <div><span>Marca</span><strong>{product.brandId || "A definir"}</strong></div>
        <div><span>Idioma</span><strong>{proposal.locale === "pt-BR" ? "Português (Brasil)" : proposal.locale}</strong></div>
        <div><span>Criado em</span><strong>{formatDate(proposal.createdAtUtc)}</strong></div>
      </div>
    </article>
  );
}

function formatPercent(value?: number | null) {
  if (value == null) return "—";
  return `${Math.round(value * 100)}%`;
}

function formatStrategy(value?: string | null) {
  if (!value) return "—";
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}
