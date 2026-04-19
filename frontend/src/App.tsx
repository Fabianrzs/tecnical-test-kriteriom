import { useState, useEffect } from "react";
import { decodeRole } from "./api";
import { LoginPage } from "./pages/LoginPage";
import { ClientsPage } from "./pages/ClientsPage";
import { CreditsPage } from "./pages/CreditsPage";
import { AuditPage } from "./pages/AuditPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ToastProvider } from "./components/Toast";
import "./App.css";

type Page = "dashboard" | "clients" | "credits" | "audit";

const NAV_ITEMS: { id: Page; label: string; icon: string; adminOnly?: boolean }[] = [
  { id: "dashboard", label: "Dashboard",   icon: "◈" },
  { id: "clients",   label: "Clientes",    icon: "◎" },
  { id: "credits",   label: "Créditos",    icon: "◇" },
  { id: "audit",     label: "Auditoría",   icon: "◉", adminOnly: true },
];

export default function App() {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem("token"));
  const [page, setPage] = useState<Page>("dashboard");
  const role = token ? decodeRole(token) : "";

  useEffect(() => {
    if (token) localStorage.setItem("token", token);
    else localStorage.removeItem("token");
  }, [token]);

  if (!token) return <LoginPage onLogin={setToken} />;

  return (
    <ToastProvider>
      <div style={{ display: "flex", height: "100vh", overflow: "hidden" }}>
        <nav style={s.nav}>
          {/* Brand */}
          <div style={s.brand}>
            <span style={s.brandMark}>K</span>
            <span style={s.brandName}>Kriteriom</span>
          </div>

          {/* Role badge */}
          <div style={s.roleBadge}>
            <span style={s.roleDot} />
            <span>{role}</span>
          </div>

          <div style={s.divider} />

          {/* Nav items */}
          <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 2 }}>
            {NAV_ITEMS.filter(n => !n.adminOnly || role === "Admin").map(n => (
              <NavBtn key={n.id} active={page === n.id} onClick={() => setPage(n.id)} icon={n.icon}>
                {n.label}
              </NavBtn>
            ))}
          </div>

          <div style={s.divider} />

          {/* Logout */}
          <button style={s.logoutBtn} onClick={() => setToken(null)}>
            <span style={{ opacity: 0.5, marginRight: 8 }}>⏻</span>
            Cerrar sesión
          </button>
        </nav>

        <main style={s.main}>
          {page === "dashboard" && <DashboardPage />}
          {page === "clients" && <ClientsPage />}
          {page === "credits" && <CreditsPage />}
          {page === "audit" && role === "Admin" && <AuditPage />}
        </main>
      </div>
    </ToastProvider>
  );
}

function NavBtn({
  active, onClick, icon, children,
}: {
  active: boolean; onClick: () => void; icon: string; children: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      style={{
        display: "flex",
        alignItems: "center",
        gap: 10,
        width: "100%",
        textAlign: "left",
        padding: "9px 14px",
        background: active ? "rgba(99,102,241,0.18)" : "transparent",
        color: active ? "#a5b4fc" : "#64748b",
        borderRadius: 8,
        border: "none",
        fontWeight: active ? 600 : 400,
        cursor: "pointer",
        fontSize: 13,
        borderLeft: active ? "3px solid #6366f1" : "3px solid transparent",
        transition: "all 0.15s",
      }}
      onMouseEnter={e => {
        if (!active) (e.currentTarget as HTMLButtonElement).style.background = "rgba(255,255,255,0.05)";
        if (!active) (e.currentTarget as HTMLButtonElement).style.color = "#94a3b8";
      }}
      onMouseLeave={e => {
        if (!active) (e.currentTarget as HTMLButtonElement).style.background = "transparent";
        if (!active) (e.currentTarget as HTMLButtonElement).style.color = "#64748b";
      }}
    >
      <span style={{ fontSize: 15, opacity: active ? 1 : 0.6, lineHeight: 1 }}>{icon}</span>
      {children}
    </button>
  );
}

const s: Record<string, React.CSSProperties> = {
  nav: {
    width: 192,
    minWidth: 192,
    background: "linear-gradient(180deg, #0f172a 0%, #1a1040 100%)",
    padding: "20px 10px 16px",
    display: "flex",
    flexDirection: "column",
    gap: 4,
    overflowY: "auto",
    boxShadow: "2px 0 12px rgba(0,0,0,0.18)",
  },
  brand: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    padding: "4px 14px 12px",
  },
  brandMark: {
    width: 28,
    height: 28,
    background: "linear-gradient(135deg, #6366f1, #8b5cf6)",
    borderRadius: 8,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: "#fff",
    fontWeight: 800,
    fontSize: 15,
    flexShrink: 0,
    boxShadow: "0 2px 8px rgba(99,102,241,0.4)",
  },
  brandName: {
    color: "#e2e8f0",
    fontWeight: 700,
    fontSize: 16,
    letterSpacing: "-0.4px",
  },
  roleBadge: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    padding: "0 14px 8px",
    fontSize: 10,
    color: "#475569",
    textTransform: "uppercase",
    letterSpacing: "0.1em",
    fontWeight: 600,
  },
  roleDot: {
    width: 6,
    height: 6,
    borderRadius: "50%",
    background: "#22c55e",
    flexShrink: 0,
  },
  divider: {
    height: 1,
    background: "rgba(255,255,255,0.06)",
    margin: "6px 0",
  },
  main: {
    flex: 1,
    padding: "28px 32px",
    overflowY: "auto",
    background: "#f1f5f9",
  },
  logoutBtn: {
    background: "transparent",
    color: "#475569",
    textAlign: "left",
    padding: "8px 14px",
    fontSize: 12,
    border: "none",
    cursor: "pointer",
    borderRadius: 7,
    display: "flex",
    alignItems: "center",
    width: "100%",
  },
};
