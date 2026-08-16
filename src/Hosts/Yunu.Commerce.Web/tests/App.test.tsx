import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "../src/App";
import { productProposalMock } from "../src/mock/productProposal.mock";

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function jsonResponse(body: unknown, status = 200) {
  return new Promise<Response>((resolve) => {
    setTimeout(() => {
      resolve({
        ok: status >= 200 && status < 300,
        status,
        statusText: "",
        json: () => Promise.resolve(body),
      } as Response);
    }, 10);
  });
}

describe("App - renderização inicial", () => {
  it("renderiza o cabeçalho, a navegação lateral e o título principal", () => {
    render(<App />);

    expect(screen.getByText("YUNU")).toBeInTheDocument();
    expect(screen.getByText("API conectada")).toBeInTheDocument();
    expect(screen.getByLabelText("Navegação principal")).toBeInTheDocument();
    expect(screen.getByText("Crie produtos conversando.")).toBeInTheDocument();
  });

  it("exibe o contador de caracteres 0/2000 quando o campo está vazio", () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText("Descreva o produto"), { target: { value: "" } });

    expect(screen.getByText("0/2000")).toBeInTheDocument();
  });

  it("atualiza o contador de caracteres conforme o usuário digita", async () => {
    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "tênis");

    expect(screen.getByText("5/2000")).toBeInTheDocument();
  });

  it("mantém o botão de gerar proposta desabilitado quando o input está vazio", async () => {
    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);

    expect(screen.getByRole("button", { name: /gerar proposta/i })).toBeDisabled();
  });
});

describe("App - fluxo funcional (POST seguido de GET)", () => {
  it("envia a intenção, mostra o loading e renderiza a proposta após POST + GET", async () => {
    fetchMock
      .mockImplementationOnce(() =>
        jsonResponse(
          {
            proposalId: productProposalMock.proposalId,
            status: "AwaitingReview",
            readyForReview: true,
            createdAtUtc: productProposalMock.createdAtUtc,
          },
          201,
        ),
      )
      .mockImplementationOnce(() => jsonResponse(productProposalMock, 200));

    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");

    await userEvent.click(screen.getByRole("button", { name: /gerar proposta/i }));

    expect(await screen.findByText("Preparando sua proposta")).toBeInTheDocument();

    await waitFor(() => expect(screen.getByText("Proposta pronta para revisão")).toBeInTheDocument());

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[0][0]).toContain("/api/catalog/product-proposals");
    expect(fetchMock.mock.calls[1][0]).toContain(`/api/catalog/product-proposals/${productProposalMock.proposalId}`);
  });

  it("usa diretamente o retorno do POST quando ele já contém a proposta completa", async () => {
    fetchMock.mockImplementationOnce(() =>
      jsonResponse(
        {
          proposalId: productProposalMock.proposalId,
          status: productProposalMock.status,
          readyForReview: true,
          createdAtUtc: productProposalMock.createdAtUtc,
          product: productProposalMock.product,
          skus: productProposalMock.skus,
          source: productProposalMock.source,
          resolution: productProposalMock.resolution,
        },
        201,
      ),
    );

    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");
    await userEvent.click(screen.getByRole("button", { name: /gerar proposta/i }));

    await waitFor(() => expect(screen.getByText("Proposta pronta para revisão")).toBeInTheDocument());

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("renderiza o card do produto com os dados retornados", async () => {
    fetchMock.mockImplementationOnce(() => jsonResponse(productProposalMock, 201));

    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");
    await userEvent.click(screen.getByRole("button", { name: /gerar proposta/i }));

    await waitFor(() => expect(screen.getByText("Microfone condensador USB")).toBeInTheDocument());
    expect(screen.getAllByText("Microfones").length).toBeGreaterThan(0);
  });

  it("renderiza um card por SKU retornado", async () => {
    const twoSkusProposal = {
      ...productProposalMock,
      skus: [
        productProposalMock.skus[0],
        { ...productProposalMock.skus[0], id: "second-sku-id" },
      ],
    };
    fetchMock.mockImplementationOnce(() => jsonResponse(twoSkusProposal, 201));

    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");
    await userEvent.click(screen.getByRole("button", { name: /gerar proposta/i }));

    await waitFor(() => expect(screen.getByText("SKU 01")).toBeInTheDocument());
    expect(screen.getByText("SKU 02")).toBeInTheDocument();
  });

  it("renderiza atributos Text, Enum e Measurement", async () => {
    fetchMock.mockImplementationOnce(() => jsonResponse(productProposalMock, 201));

    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");
    await userEvent.click(screen.getByRole("button", { name: /gerar proposta/i }));

    await waitFor(() => expect(screen.getByText("Cor")).toBeInTheDocument());
    expect(screen.getByText("Conexão")).toBeInTheDocument();
    expect(screen.getByText("Peso para frete")).toBeInTheDocument();
  });
});

describe("App - estados de erro", () => {
  async function submitIntent() {
    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");
    await userEvent.click(screen.getByRole("button", { name: /gerar proposta/i }));
  }

  it("exibe mensagem apropriada para erro 400", async () => {
    fetchMock.mockImplementationOnce(() => jsonResponse({ detail: "Entrada inválida." }, 400));
    await submitIntent();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByText("Entrada inválida.")).toBeInTheDocument();
  });

  it("exibe mensagem apropriada para erro 422", async () => {
    fetchMock.mockImplementationOnce(() => jsonResponse({ detail: "Faltam informações." }, 422));
    await submitIntent();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByText("Faltam informações.")).toBeInTheDocument();
  });

  it("exibe mensagem apropriada para erro 503", async () => {
    fetchMock.mockImplementationOnce(() => jsonResponse({}, 503));
    await submitIntent();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(
      screen.getByText(/serviço de inteligência está temporariamente indisponível/i),
    ).toBeInTheDocument();
  });

  it("exibe mensagem de falha de conexão quando o fetch rejeita", async () => {
    fetchMock.mockImplementationOnce(() => Promise.reject(new TypeError("Failed to fetch")));
    await submitIntent();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByText(/não foi possível conectar/i)).toBeInTheDocument();
  });
});

describe("App - navegação por teclado", () => {
  it("permite navegar até o textarea e o botão via Tab e disparar envio com Ctrl+Enter", async () => {
    fetchMock.mockImplementationOnce(() => jsonResponse(productProposalMock, 201));

    render(<App />);
    const textarea = screen.getByLabelText("Descreva o produto");
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "microfone condensador USB preto");
    textarea.focus();
    expect(textarea).toHaveFocus();

    fireEvent.keyDown(textarea, { key: "Enter", ctrlKey: true });

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
  });
});
