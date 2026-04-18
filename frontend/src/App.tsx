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
          <div style={s.navLogo}>Kriteriom</div>
          <div style={s.navRole}>{role}</div>
          <NavBtn active={page === "dashboard"} onClick={() => setPage("dashboard")}>Dashboard</NavBtn>
          <NavBtn active={page === "clients"} onClick={() => setPage("clients")}>Clientes</NavBtn>
          <NavBtn active={page === "credits"} onClick={() => setPage("credits")}>Créditos</NavBtn>
          {role === "Admin" && (
            <NavBtn active={page === "audit"} onClick={() => setPage("audit")}>Auditoría</NavBtn>
          )}
          <button style={s.logoutBtn} onClick={() => setToken(null)}>Cerrar sesión</button>
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

function NavBtn({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      style={{
        display: "block",
        width: "100%",
        textAlign: "left",
        padding: "9px 14px",
        background: active ? "rgba(99,102,241,0.12)" : "transparent",
        color: active ? "#818cf8" : "#64748b",
        borderRadius: 6,
        border: "none",
        fontWeight: active ? 600 : 400,
        cursor: "pointer",
        fontSize: 13,
      }}
    >
      {children}
    </button>
  );
}

const s: Record<string, React.CSSProperties> = {
  nav: {
    width: 176,
    minWidth: 176,
    background: "#0f172a",
    padding: "20px 10px 16px",
    display: "flex",
    flexDirection: "column",
    gap: 2,
    overflowY: "auto",
  },
  navLogo: {
    color: "#818cf8",
    fontWeight: 700,
    fontSize: 17,
    padding: "6px 14px 2px",
    letterSpacing: "-0.3px",
  },
  navRole: {
    color: "#334155",
    fontSize: 10,
    padding: "0 14px 16px",
    textTransform: "uppercase",
    letterSpacing: "0.08em",
  },
  main: {
    flex: 1,
    padding: "24px 28px",
    overflowY: "auto",
    background: "#f8fafc",
  },
  logoutBtn: {
    marginTop: "auto",
    background: "transparent",
    color: "#334155",
    textAlign: "left",
    padding: "9px 14px",
    fontSize: 12,
    border: "none",
    cursor: "pointer",
    borderRadius: 6,
  },
};
