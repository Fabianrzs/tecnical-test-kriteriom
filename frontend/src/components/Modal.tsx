import type { ReactNode } from "react";

interface Props {
  title: string;
  onClose: () => void;
  children: ReactNode;
  maxWidth?: number;
}

export function Modal({ title, onClose, children, maxWidth = 460 }: Props) {
  return (
    <div style={s.overlay} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ ...s.box, width: maxWidth }}>
        <div style={s.header}>
          <h2 style={s.title}>{title}</h2>
          <button style={s.close} onClick={onClose}>✕</button>
        </div>
        {children}
      </div>
    </div>
  );
}

const s: Record<string, React.CSSProperties> = {
  overlay: { position: "fixed", inset: 0, background: "rgba(0,0,0,0.4)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 50 },
  box: { background: "#fff", borderRadius: 12, padding: "24px 28px", maxHeight: "90vh", overflowY: "auto" },
  header: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 },
  title: { fontSize: 18, fontWeight: 600, color: "#1e293b" },
  close: { background: "none", border: "none", fontSize: 18, color: "#9ca3af", cursor: "pointer", lineHeight: 1 },
};
