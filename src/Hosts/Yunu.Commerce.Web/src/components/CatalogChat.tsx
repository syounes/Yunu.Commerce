import { useMemo } from "react";

interface CatalogChatProps {
  prompt: string;
  sentPrompt?: string;
  loading: boolean;
  onPromptChange: (value: string) => void;
  onSubmit: () => void;
}

export function CatalogChat({ prompt, sentPrompt, loading, onPromptChange, onSubmit }: CatalogChatProps) {
  const characterCount = useMemo(() => prompt.length, [prompt]);

  return (
    <section className="chat-card" aria-label="Conversa com o assistente de catálogo">
      <div className="chat-header">
        <div className="assistant-identity">
          <div className="assistant-avatar">✦</div>
          <div><strong>YUNU Catalog</strong><span>Assistente de criação de produtos</span></div>
        </div>
        <span className="model-pill">RAG ativo</span>
      </div>

      <div className="conversation">
        <div className="message assistant-message">
          <div className="mini-avatar">✦</div>
          <div className="bubble">
            <p>Olá, Sulaiman. Qual produto vamos cadastrar hoje?</p>
            <span>Inclua características, condição e dados da embalagem. Eu organizo o restante.</span>
          </div>
        </div>

        {sentPrompt && <div className="message user-message"><div className="bubble"><p>{sentPrompt}</p></div></div>}

        {loading && (
          <div className="message assistant-message" aria-live="polite">
            <div className="mini-avatar">✦</div>
            <div className="processing-bubble">
              <div className="thinking-dots"><i /><i /><i /></div>
              <div><strong>Preparando sua proposta</strong><span>Interpretando intenção, categoria e atributos…</span></div>
            </div>
          </div>
        )}
      </div>

      <div className="composer">
        <label htmlFor="product-intent">Descreva o produto</label>
        <textarea
          id="product-intent"
          value={prompt}
          maxLength={2000}
          onChange={(event) => onPromptChange(event.target.value)}
          onKeyDown={(event) => {
            if ((event.metaKey || event.ctrlKey) && event.key === "Enter") onSubmit();
          }}
          placeholder="Ex.: Quero cadastrar um tênis masculino preto para corrida…"
        />
        <div className="composer-footer">
          <div className="composer-meta">
            <button type="button" className="attach-button" aria-label="Anexar informação">＋</button>
            <span>{characterCount}/2000</span><span className="shortcut">Ctrl ↵ para enviar</span>
          </div>
          <button className="primary-button" type="button" onClick={onSubmit} disabled={!prompt.trim() || loading}>
            {loading ? "Analisando…" : "Gerar proposta"}<span aria-hidden="true">→</span>
          </button>
        </div>
      </div>
    </section>
  );
}
