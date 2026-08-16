import { useRef, useState } from "react";
import { createProductProposal, ProductProposalApiError } from "./api/productProposalsApi";
import { CatalogChat } from "./components/CatalogChat";
import { ProposalResult } from "./components/ProposalResult";
import { SideRail } from "./components/SideRail";
import { TopBar } from "./components/TopBar";
import { productProposalMock } from "./mock/productProposal.mock";
import type { ProductProposal } from "./models/productProposal";

const samplePrompt = productProposalMock.source.originalInput;

export function App() {
  const [prompt, setPrompt] = useState(samplePrompt);
  const [sentPrompt, setSentPrompt] = useState<string>();
  const [proposal, setProposal] = useState<ProductProposal>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();
  const requestController = useRef<AbortController | undefined>(undefined);

  async function handleSubmit() {
    const input = prompt.trim();
    if (!input || loading) return;

    requestController.current?.abort();
    const controller = new AbortController();
    requestController.current = controller;

    setSentPrompt(input);
    setProposal(undefined);
    setError(undefined);
    setLoading(true);

    try {
      const useMock = import.meta.env.VITE_USE_MOCK === "true";
      const result = useMock
        ? await simulateProposal(controller.signal)
        : await createProductProposal({ input, locale: "pt-BR" }, controller.signal);

      setProposal(result);
    } catch (caught) {
      if (controller.signal.aborted) return;

      if (caught instanceof ProductProposalApiError) {
        setError(mapApiError(caught));
      } else {
        setError("Não foi possível conectar à API da YUNU. Verifique se ela está em execução e se o CORS está configurado.");
      }
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }

  return (
    <main className="app-shell">
      <TopBar />
      <div className="workspace">
        <SideRail />
        <section className="content">
          <div className="page-heading">
            <div>
              <p className="eyebrow">CATÁLOGO INTELIGENTE</p>
              <h1>Crie produtos conversando.</h1>
              <p className="subtitle">Descreva o produto do seu jeito. A YUNU interpreta, classifica e prepara tudo para sua revisão.</p>
            </div>
            <div className="environment-badge"><span>LAB</span><strong>pt-BR</strong></div>
          </div>

          <CatalogChat
            prompt={prompt}
            sentPrompt={sentPrompt}
            loading={loading}
            onPromptChange={setPrompt}
            onSubmit={handleSubmit}
          />

          {error && <div className="error-banner" role="alert"><strong>Não conseguimos gerar a proposta.</strong><span>{error}</span></div>}
          {proposal && !loading && <ProposalResult proposal={proposal} />}

          <footer><span>YUNU Commerce AI</span><p>Uma intenção. Um catálogo completo.</p></footer>
        </section>
      </div>
    </main>
  );
}

function simulateProposal(signal: AbortSignal) {
  return new Promise<ProductProposal>((resolve, reject) => {
    const timer = window.setTimeout(() => resolve(productProposalMock), 1350);
    signal.addEventListener("abort", () => {
      window.clearTimeout(timer);
      reject(new DOMException("Aborted", "AbortError"));
    }, { once: true });
  });
}

function mapApiError(error: ProductProposalApiError) {
  if (error.status === 400) return error.detail || "Revise a intenção informada.";
  if (error.status === 422) return error.detail || "A intenção precisa de mais informações para gerar uma proposta.";
  if (error.status === 503) return "O serviço de inteligência está temporariamente indisponível. Tente novamente em alguns instantes.";
  return error.detail || "Ocorreu um erro inesperado.";
}
