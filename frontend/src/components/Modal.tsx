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
      <div style={{ ...s.box, maxWidth }}>
        <div style={s.header}>
          <h2 style={s.title}>{title}</h2>
          <button style={s.close} onClick={onClose} title="Cerrar">✕</button>
        </div>
        <div style={s.body}>
          {children}
        </div>
      </div>
    </div>
  );
}

const s: Record<string, React.CSSProperties> = {
  overlay: {
    position: "fixed",
    inset: 0,
    background: "rgba(15,23,42,0.55)",
    backdropFilter: "blur(4px)",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    zIndex: 50,
    padding: 20,
    animation: "fadeIn 0.15s ease",
  },
  box: {
    background: "#fff",
    borderRadius: 14,
    width: "100%",
    maxHeight: "90vh",
    display: "flex",
    flexDirection: "column",
    boxShadow: "0 25px 50px -12px rgba(0,0,0,0.35), 0 0 0 1px rgba(0,0,0,0.05)",
    overflow: "hidden",
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    padding: "20px 24px 16px",
    borderBottom: "1px solid #f1f5f9",
    flexShrink: 0,
  },
  title: {
    fontSize: 17,
    fontWeight: 700,
    color: "#0f172a",
    letterSpacing: "-0.2px",
  },
  close: {
    background: "#f1f5f9",
    border: "none",
    width: 30,
    height: 30,
    borderRadius: 8,
    fontSize: 14,
    color: "#64748b",
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    flexShrink: 0,
    padding: 0,
  },
  body: {
    padding: "20px 24px 24px",
    overflowY: "auto",
    flex: 1,
  },
};
