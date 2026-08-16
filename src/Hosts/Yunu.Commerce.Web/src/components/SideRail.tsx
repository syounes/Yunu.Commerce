export function SideRail() {
  return (
    <aside className="rail" aria-label="Navegação principal">
      <button className="rail-button active" type="button" aria-label="Assistente de catálogo">✦</button>
      <button className="rail-button" type="button" aria-label="Produtos">◇</button>
      <button className="rail-button" type="button" aria-label="Histórico">↻</button>
      <div className="rail-spacer" />
      <button className="rail-button" type="button" aria-label="Configurações">⚙</button>
    </aside>
  );
}
