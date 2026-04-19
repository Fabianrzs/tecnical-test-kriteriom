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
      setError("Credenciales inválidas");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={s.wrapper}>
      <div style={s.card}>
        <div style={s.logo}>Kriteriom</div>
        <p style={s.subtitle}>Plataforma de Gestión de Créditos</p>
        <form onSubmit={handleSubmit} style={s.form}>
          <div style={s.field}>
            <label style={s.label}>Usuario</label>
            <input value={username} onChange={e => setUsername(e.target.value)} required />
          </div>
          <div style={s.field}>
            <label style={s.label}>Contraseña</label>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} required />
          </div>
          {error && <p style={s.error}>{error}</p>}
          <button type="submit" style={s.btn} disabled={loading}>
            {loading ? "Ingresando..." : "Ingresar"}
          </button>
        </form>
        <p style={s.hint}>admin / Admin@2026! · analyst / Analyst@2026! · readonly / ReadOnly@2026!</p>
      </div>
    </div>
  );
}

const s: Record<string, React.CSSProperties> = {
  wrapper: { display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", background: "#f8fafc" },
  card: { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: "40px 36px", width: 360 },
  logo: { fontSize: 28, fontWeight: 700, color: "#6366f1", marginBottom: 6 },
  subtitle: { color: "#64748b", fontSize: 13, marginBottom: 28 },
  form: { display: "flex", flexDirection: "column", gap: 16 },
  field: { display: "flex", flexDirection: "column", gap: 6 },
  label: { fontSize: 13, fontWeight: 500, color: "#374151" },
  error: { color: "#ef4444", fontSize: 13, textAlign: "center" },
  btn: { background: "#6366f1", color: "#fff", padding: "10px 0", fontWeight: 600, marginTop: 4 },
  hint: { fontSize: 11, color: "#9ca3af", marginTop: 20, textAlign: "center", lineHeight: 1.6 },
};
