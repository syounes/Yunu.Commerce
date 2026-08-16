export function TopBar() {
  return (
    <header className="topbar">
      <div className="brand-lockup">
        <div className="brand-mark" aria-hidden="true"><span>Y</span></div>
        <div><strong>YUNU</strong><span>Commerce AI</span></div>
      </div>
      <div className="topbar-actions">
        <div className="connection-pill"><span className="pulse-dot" />API conectada</div>
        <button className="icon-button" type="button" aria-label="Abrir notificações">◌</button>
        <div className="avatar" aria-label="Perfil do usuário">SY</div>
      </div>
    </header>
  );
}
