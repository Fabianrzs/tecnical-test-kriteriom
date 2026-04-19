import { useState } from "react";
import { api } from "../api";

interface Props {
  onLogin: (token: string) => void;
}

export function LoginPage({ onLogin }: Props) {
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("Admin@2026!");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");
    try {
      const { token } = await api.login(username, password);
      onLogin(token);
    } catch {
      setError("Credenciales inválidas. Verifica usuario y contraseña.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={s.wrapper}>
      <div style={s.card}>
        {/* Logo */}
        <div style={s.logoArea}>
          <div style={s.logoMark}>K</div>
          <div>
            <div style={s.logoName}>Kriteriom</div>
            <div style={s.logoSub}>Gestión de Créditos</div>
          </div>
        </div>

        <form onSubmit={handleSubmit} style={s.form}>
          <div style={s.field}>
            <label style={s.label}>Usuario</label>
            <input
              value={username}
              onChange={e => setUsername(e.target.value)}
              placeholder="Nombre de usuario"
              required
              autoFocus
            />
          </div>
          <div style={s.field}>
            <label style={s.label}>Contraseña</label>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="••••••••"
              required
            />
          </div>
          {error && (
            <div style={s.errorBox}>
              <span style={{ marginRight: 6 }}>⚠</span>
              {error}
            </div>
          )}
          <button type="submit" style={s.btn} disabled={loading}>
            {loading ? (
              <span style={{ opacity: 0.8 }}>Ingresando...</span>
            ) : (
              "Ingresar →"
            )}
          </button>
        </form>

        <div style={s.hintBox}>
          <p style={s.hintTitle}>Credenciales de acceso</p>
          {[
            ["admin", "Admin@2026!", "Admin"],
            ["analyst", "Analyst@2026!", "Analyst"],
            ["readonly", "ReadOnly@2026!", "Read-Only"],
          ].map(([u, p, r]) => (
            <div key={u} style={s.hintRow}>
              <span style={s.hintUser}>{u}</span>
              <span style={s.hintPass}>{p}</span>
              <span style={s.hintRole}>{r}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

const s: Record<string, React.CSSProperties> = {
  wrapper: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    minHeight: "100vh",
    background: "linear-gradient(135deg, #0f172a 0%, #1e1b4b 45%, #312e81 100%)",
    padding: 20,
  },
  card: {
    background: "#fff",
    borderRadius: 16,
    padding: "36px 36px 28px",
    width: 400,
    boxShadow: "0 25px 50px -12px rgba(0,0,0,0.4), 0 0 0 1px rgba(255,255,255,0.05)",
  },
  logoArea: {
    display: "flex",
    alignItems: "center",
    gap: 12,
    marginBottom: 32,
  },
  logoMark: {
    width: 44,
    height: 44,
    background: "linear-gradient(135deg, #6366f1, #8b5cf6)",
    borderRadius: 12,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: "#fff",
    fontWeight: 800,
    fontSize: 20,
    boxShadow: "0 4px 12px rgba(99,102,241,0.4)",
    flexShrink: 0,
  },
  logoName: {
    fontSize: 22,
    fontWeight: 700,
    color: "#0f172a",
    letterSpacing: "-0.5px",
  },
  logoSub: {
    fontSize: 12,
    color: "#64748b",
    marginTop: 1,
  },
  form: { display: "flex", flexDirection: "column", gap: 16 },
  field: { display: "flex", flexDirection: "column", gap: 6 },
  label: { fontSize: 13, fontWeight: 600, color: "#374151" },
  errorBox: {
    background: "#fef2f2",
    border: "1px solid #fecaca",
    borderRadius: 8,
    padding: "10px 14px",
    fontSize: 13,
    color: "#dc2626",
    display: "flex",
    alignItems: "center",
  },
  btn: {
    background: "linear-gradient(135deg, #6366f1, #8b5cf6)",
    color: "#fff",
    padding: "11px 0",
    fontWeight: 700,
    fontSize: 14,
    marginTop: 4,
    letterSpacing: "0.02em",
    boxShadow: "0 4px 12px rgba(99,102,241,0.35)",
    border: "none",
    borderRadius: 8,
  },
  hintBox: {
    marginTop: 24,
    background: "#f8fafc",
    border: "1px solid #e2e8f0",
    borderRadius: 10,
    padding: "14px 16px",
  },
  hintTitle: {
    fontSize: 11,
    fontWeight: 700,
    color: "#64748b",
    textTransform: "uppercase",
    letterSpacing: "0.08em",
    marginBottom: 10,
  },
  hintRow: {
    display: "flex",
    gap: 8,
    alignItems: "center",
    marginBottom: 6,
    fontSize: 12,
  },
  hintUser: {
    fontWeight: 700,
    color: "#1e293b",
    minWidth: 70,
    fontFamily: "monospace",
    fontSize: 12,
  },
  hintPass: {
    color: "#475569",
    flex: 1,
    fontFamily: "monospace",
    fontSize: 11,
  },
  hintRole: {
    background: "#e0e7ff",
    color: "#4338ca",
    padding: "2px 8px",
    borderRadius: 10,
    fontSize: 10,
    fontWeight: 600,
  },
};
