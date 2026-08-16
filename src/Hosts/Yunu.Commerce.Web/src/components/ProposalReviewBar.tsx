export function ProposalReviewBar() {
  return (
    <div className="review-bar">
      <div>
        <span className="review-mark">✦</span>
        <div><strong>Tudo certo com a proposta?</strong><p>Revise os dados antes de criar o produto definitivo.</p></div>
      </div>
      <div className="review-actions">
        <button className="secondary-button" type="button">Salvar para depois</button>
        <button className="confirm-button" type="button" disabled title="A confirmação será implementada em uma etapa futura">
          Confirmar e criar produto <span>→</span>
        </button>
      </div>
    </div>
  );
}
