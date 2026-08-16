import type { ProductProposal } from "../models/productProposal";
import { ProductProposalCard } from "./ProductProposalCard";
import { ProposalReviewBar } from "./ProposalReviewBar";
import { SkuProposalCard } from "./SkuProposalCard";

interface ProposalResultProps {
  proposal: ProductProposal;
}

export function ProposalResult({ proposal }: ProposalResultProps) {
  const attributeCount = proposal.skus.reduce((total, sku) => total + sku.attributes.length, 0);

  return (
    <section className="proposal-area" aria-live="polite">
      <div className="result-heading">
        <div>
          <span className="success-icon">✓</span>
          <div><h2>Proposta pronta para revisão</h2><p>Categoria e {attributeCount} atributos resolvidos com sucesso.</p></div>
        </div>
        <div className="proposal-id"><span>ID DA PROPOSTA</span><code>{shortenId(proposal.proposalId)}</code></div>
      </div>

      <ProductProposalCard proposal={proposal} />
      {proposal.skus.map((sku, index) => <SkuProposalCard key={sku.id} sku={sku} index={index} />)}
      <ProposalReviewBar />
    </section>
  );
}

function shortenId(id: string) {
  return id.length > 14 ? `${id.slice(0, 8)}…${id.slice(-5)}` : id;
}
