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

  if (!stats && loading) return (
    <div style={s.loading}>
      <div style={s.loadingDot} />
      <span>Cargando métricas...</span>
    </div>
  );
  if (!stats && error) return <p style={s.error}>{error}</p>;
  if (!stats) return null;

  const { byStatus } = stats;

  return (
    <div>
      <div style={s.header}>
        <div>
          <h1 style={s.title}>Dashboard</h1>
          <p style={s.subtitle}>Resumen operativo del sistema de créditos</p>
        </div>
        <button style={s.btnRefresh} onClick={load} disabled={loading}>
          {loading ? "Actualizando..." : "↺ Actualizar"}
        </button>
      </div>

      {/* KPI row */}
      <div style={s.kpiGrid}>
        <KpiCard
          label="Clientes activos"
          value={stats.totalClients}
          accent="#6366f1"
          bg="linear-gradient(135deg, #eef2ff, #e0e7ff)"
          icon="◎"
        />
        <KpiCard
          label="Créditos totales"
          value={stats.totalCredits}
          accent="#0ea5e9"
          bg="linear-gradient(135deg, #f0f9ff, #e0f2fe)"
          icon="◇"
        />
        <KpiCard
          label="Tasa de aprobación"
          value={`${stats.approvalRate}%`}
          accent="#22c55e"
          bg="linear-gradient(135deg, #f0fdf4, #dcfce7)"
          icon="◈"
        />
        <KpiCard
          label="Pendientes"
          value={byStatus.pending}
          accent="#f59e0b"
          bg="linear-gradient(135deg, #fffbeb, #fef3c7)"
          icon="◌"
        />
      </div>

      {/* Status breakdown */}
      <div style={{ marginTop: 28, display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }}>
        <div style={s.panel}>
          <div style={s.panelHeader}>Estado de créditos</div>
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {[
              { label: "En Revisión", value: byStatus.underReview, color: "#6366f1" },
              { label: "Aprobados",   value: byStatus.approved,    color: "#22c55e" },
              { label: "Rechazados",  value: byStatus.rejected,    color: "#ef4444" },
              { label: "Cerrados",    value: byStatus.closed,      color: "#6b7280" },
              { label: "En Mora",     value: byStatus.defaulted,   color: "#dc2626" },
            ].map(({ label, value, color }) => (
              <StatusRow key={label} label={label} value={value} total={stats.totalCredits} color={color} />
            ))}
          </div>
        </div>

        {stats.totalCredits > 0 && (
          <div style={s.panel}>
            <div style={s.panelHeader}>Distribución</div>
            <BarChart stats={stats} />
          </div>
        )}
      </div>
    </div>
  );
}

function KpiCard({
  label, value, accent, bg, icon,
}: {
  label: string; value: string | number; accent: string; bg: string; icon: string;
}) {
  return (
    <div style={{ ...s.kpiCard, background: bg, borderColor: accent + "40" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 12 }}>
        <p style={{ fontSize: 11, fontWeight: 700, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.07em" }}>
          {label}
        </p>
        <span style={{ fontSize: 18, color: accent, opacity: 0.7 }}>{icon}</span>
      </div>
      <p style={{ fontSize: 30, fontWeight: 800, color: accent, letterSpacing: "-0.5px", lineHeight: 1 }}>{value}</p>
    </div>
  );
}

function StatusRow({ label, value, total, color }: { label: string; value: number; total: number; color: string }) {
  const pct = total > 0 ? (value / total) * 100 : 0;
  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 5 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <div style={{ width: 8, height: 8, borderRadius: "50%", background: color }} />
          <span style={{ fontSize: 13, color: "#374151" }}>{label}</span>
        </div>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <span style={{ fontSize: 12, color: "#94a3b8" }}>{pct.toFixed(1)}%</span>
          <span style={{ fontWeight: 700, fontSize: 14, color: "#1e293b", minWidth: 28, textAlign: "right" }}>{value}</span>
        </div>
      </div>
      <div style={{ height: 5, background: "#f1f5f9", borderRadius: 3, overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${pct}%`, background: color, borderRadius: 3, transition: "width 0.6s ease" }} />
      </div>
    </div>
  );
}

function BarChart({ stats }: { stats: CreditStats }) {
  const { byStatus } = stats;
  const bars: { label: string; value: number; color: string }[] = [
    { label: "Pendiente",    value: byStatus.pending,     color: "#f59e0b" },
    { label: "En Revisión",  value: byStatus.underReview, color: "#6366f1" },
    { label: "Aprobado",     value: byStatus.approved,    color: "#22c55e" },
    { label: "Rechazado",    value: byStatus.rejected,    color: "#ef4444" },
    { label: "Cerrado",      value: byStatus.closed,      color: "#6b7280" },
    { label: "En Mora",      value: byStatus.defaulted,   color: "#dc2626" },
  ].filter(b => b.value > 0);

  const max = Math.max(...bars.map(b => b.value));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      {bars.map(b => (
        <div key={b.label}>
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 5, fontSize: 13 }}>
            <span style={{ color: "#475569" }}>{b.label}</span>
            <span style={{ fontWeight: 600, color: b.color }}>{b.value}</span>
          </div>
          <div style={{ height: 8, background: "#f1f5f9", borderRadius: 4, overflow: "hidden" }}>
            <div style={{
              height: "100%",
              width: `${(b.value / max) * 100}%`,
              background: b.color,
              borderRadius: 4,
              transition: "width 0.6s ease",
            }} />
          </div>
        </div>
      ))}
    </div>
  );
}

const s: Record<string, React.CSSProperties> = {
  loading: { display: "flex", alignItems: "center", gap: 10, padding: 40, color: "#64748b", justifyContent: "center" },
  loadingDot: { width: 10, height: 10, borderRadius: "50%", background: "#6366f1", animation: "pulse 1s infinite" },
  header: { display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 24 },
  title: { fontSize: 24, fontWeight: 800, color: "#0f172a", letterSpacing: "-0.5px" },
  subtitle: { fontSize: 13, color: "#64748b", marginTop: 3 },
  btnRefresh: {
    background: "#fff",
    color: "#374151",
    padding: "8px 16px",
    fontSize: 13,
    border: "1px solid #e2e8f0",
    borderRadius: 8,
    cursor: "pointer",
    boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
    fontWeight: 500,
  },
  kpiGrid: { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(170px, 1fr))", gap: 16 },
  kpiCard: {
    border: "1px solid",
    borderRadius: 12,
    padding: "18px 20px",
    boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
  },
  panel: {
    background: "#fff",
    border: "1px solid #e2e8f0",
    borderRadius: 12,
    padding: "20px 22px",
    boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
  },
  panelHeader: {
    fontSize: 11,
    fontWeight: 700,
    color: "#64748b",
    textTransform: "uppercase",
    letterSpacing: "0.07em",
    marginBottom: 16,
    paddingBottom: 12,
    borderBottom: "1px solid #f1f5f9",
  },
  error: { color: "#ef4444", fontSize: 13, padding: 20 },
};
