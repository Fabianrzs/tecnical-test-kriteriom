import { useState, useEffect, useCallback } from "react";
import { api } from "../api";
import type { CreditStats } from "../api";

export function DashboardPage() {
  const [stats, setStats] = useState<CreditStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setStats(await api.getCreditStats());
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al cargar métricas");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  if (!stats && loading) return <p style={s.empty}>Cargando métricas...</p>;
  if (!stats && error) return <p style={s.error}>{error}</p>;
  if (!stats) return null;

  const { byStatus } = stats;

  return (
    <div>
      <div style={s.header}>
        <div>
          <h1 style={s.title}>Dashboard</h1>
          <p style={s.subtitle}>Resumen del sistema de créditos</p>
        </div>
        <button style={s.btnSm} onClick={load} disabled={loading}>
          {loading ? "Actualizando..." : "Actualizar"}
        </button>
      </div>

      <div style={s.grid4}>
        <KpiCard label="Clientes" value={stats.totalClients} accent="#6366f1" />
        <KpiCard label="Créditos totales" value={stats.totalCredits} accent="#0ea5e9" />
        <KpiCard label="Tasa de aprobación" value={`${stats.approvalRate}%`} accent="#22c55e" />
        <KpiCard label="Pendientes" value={byStatus.pending} accent="#f59e0b" />
      </div>

      <div style={{ marginTop: 28 }}>
        <h2 style={s.sectionTitle}>Estado de créditos</h2>
        <div style={s.grid5}>
          <StatusCard label="En Revisión" value={byStatus.underReview} color="#6366f1" />
          <StatusCard label="Aprobados" value={byStatus.approved} color="#22c55e" />
          <StatusCard label="Rechazados" value={byStatus.rejected} color="#ef4444" />
          <StatusCard label="Cerrados" value={byStatus.closed} color="#6b7280" />
          <StatusCard label="En Mora" value={byStatus.defaulted} color="#dc2626" />
        </div>
      </div>

      {stats.totalCredits > 0 && (
        <div style={{ marginTop: 28 }}>
          <h2 style={s.sectionTitle}>Distribución</h2>
          <BarChart stats={stats} />
        </div>
      )}
    </div>
  );
}

function KpiCard({ label, value, accent }: { label: string; value: string | number; accent: string }) {
  return (
    <div style={{ ...s.card, borderLeft: `3px solid ${accent}` }}>
      <p style={{ fontSize: 11, color: "#64748b", margin: "0 0 8px", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.06em" }}>{label}</p>
      <p style={{ fontSize: 26, fontWeight: 700, color: "#1e293b", margin: 0 }}>{value}</p>
    </div>
  );
}

function StatusCard({ label, value, color }: { label: string; value: number; color: string }) {
  return (
    <div style={{ ...s.card, display: "flex", alignItems: "center", gap: 10 }}>
      <div style={{ width: 10, height: 10, borderRadius: "50%", background: color, flexShrink: 0 }} />
      <div>
        <p style={{ fontSize: 11, color: "#64748b", margin: "0 0 2px" }}>{label}</p>
        <p style={{ fontSize: 20, fontWeight: 700, color: "#1e293b", margin: 0 }}>{value}</p>
      </div>
    </div>
  );
}

function BarChart({ stats }: { stats: CreditStats }) {
  const { byStatus, totalCredits } = stats;
  const bars: { label: string; value: number; color: string }[] = [
    { label: "Pendiente", value: byStatus.pending, color: "#f59e0b" },
    { label: "En Revisión", value: byStatus.underReview, color: "#6366f1" },
    { label: "Aprobado", value: byStatus.approved, color: "#22c55e" },
    { label: "Rechazado", value: byStatus.rejected, color: "#ef4444" },
    { label: "Cerrado", value: byStatus.closed, color: "#6b7280" },
    { label: "En Mora", value: byStatus.defaulted, color: "#dc2626" },
  ].filter(b => b.value > 0);

  return (
    <div style={s.card}>
      {bars.map(b => (
        <div key={b.label} style={{ marginBottom: 14 }}>
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 5, fontSize: 13 }}>
            <span style={{ color: "#475569" }}>{b.label}</span>
            <span style={{ color: "#94a3b8" }}>{b.value} &nbsp;{((b.value / totalCredits) * 100).toFixed(1)}%</span>
          </div>
          <div style={{ height: 6, background: "#f1f5f9", borderRadius: 3, overflow: "hidden" }}>
            <div style={{ height: "100%", width: `${(b.value / totalCredits) * 100}%`, background: b.color, borderRadius: 3 }} />
          </div>
        </div>
      ))}
    </div>
  );
}

const s: Record<string, React.CSSProperties> = {
  header: { display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 24 },
  title: { fontSize: 22, fontWeight: 700, color: "#1e293b", margin: 0 },
  subtitle: { fontSize: 13, color: "#64748b", marginTop: 4 },
  btnSm: { background: "#f1f5f9", color: "#374151", padding: "7px 14px", fontSize: 13, border: "none", borderRadius: 6, cursor: "pointer" },
  grid4: { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 14 },
  grid5: { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))", gap: 14 },
  card: { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 8, padding: "16px 18px" },
  sectionTitle: { fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 12, textTransform: "uppercase", letterSpacing: "0.05em" },
  empty: { textAlign: "center", color: "#94a3b8", padding: 40 },
  error: { color: "#ef4444", fontSize: 13 },
};
