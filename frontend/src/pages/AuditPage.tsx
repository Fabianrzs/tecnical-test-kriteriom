import { useState, useEffect, useCallback, useRef } from "react";
import { api } from "../api";
import type { AuditRecord, AuditPagedResult, BatchCheckpoint } from "../api";
import { Modal } from "../components/Modal";
import { useToast } from "../components/Toast";

const EVENT_LABELS: Record<string, { label: string; color: string }> = {
  CreditCreatedIntegrationEvent:  { label: "Crédito Creado",          color: "#6366f1" },
  CreditUpdatedIntegrationEvent:  { label: "Crédito Actualizado",      color: "#f59e0b" },
  RiskAssessedIntegrationEvent:   { label: "Riesgo Evaluado",          color: "#22c55e" },
};

function eventMeta(type: string) {
  return EVENT_LABELS[type] ?? { label: type.replace("IntegrationEvent", ""), color: "#6b7280" };
}

export function AuditPage() {
  const [data, setData] = useState<AuditPagedResult | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<AuditRecord | null>(null);
  const [creditTrail, setCreditTrail] = useState<{ creditId: string; records: AuditRecord[] } | null>(null);
  const [trailLoading, setTrailLoading] = useState(false);
  const [batchRunning, setBatchRunning] = useState(false);
  const [batchJobs, setBatchJobs] = useState<BatchCheckpoint[]>([]);
  const batchPollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const { toast } = useToast();

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setData(await api.getAuditRecent(page, 20));
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error cargando registros");
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => { load(); }, [load]);

  async function loadCreditTrail(creditId: string) {
    setTrailLoading(true);
    try {
      const records = await api.getAuditByCredit(creditId);
      setCreditTrail({ creditId, records });
    } finally {
      setTrailLoading(false);
    }
  }

  async function triggerBatch() {
    setBatchRunning(true);
    try {
      await api.triggerBatch();
      toast("Batch de recalculación iniciado", "info");
      batchPollRef.current = setInterval(async () => {
        try {
          const jobs = await api.getBatchStatus();
          setBatchJobs(jobs);
          const allDone = jobs.length > 0 && jobs.every(j => j.status === "Completed" || j.status === "Failed");
          if (allDone) {
            clearInterval(batchPollRef.current!);
            batchPollRef.current = null;
            setBatchRunning(false);
            toast("Batch completado", "success");
          }
        } catch { /* keep polling */ }
      }, 3000);
    } catch (e: unknown) {
      toast(e instanceof Error ? e.message : "Error al iniciar batch", "error");
      setBatchRunning(false);
    }
  }

  useEffect(() => () => { if (batchPollRef.current) clearInterval(batchPollRef.current); }, []);

  function exportCsv() {
    if (!data?.items.length) return;
    const header = ["ID", "Tipo", "Credit ID", "Correlation ID", "Ocurrido", "Registrado"];
    const rows = data.items.map(r => [
      r.id, r.eventType, r.entityId ?? "", r.correlationId,
      r.occurredOn, r.recordedAt
    ]);
    const csv = [header, ...rows].map(row => row.map(c => `"${c}"`).join(",")).join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `audit_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    toast("CSV exportado", "success");
  }

  return (
    <div>
      <div style={s.header}>
        <div>
          <h1 style={s.title}>Auditoría</h1>
          <p style={s.subtitle}>Registro inmutable de todos los eventos del sistema</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <button style={s.btnSm} onClick={load}>Actualizar</button>
          <button style={s.btnSm} onClick={exportCsv} disabled={!data?.items.length}>Exportar CSV</button>
          <button
            style={{ ...s.btnSm, background: batchRunning ? "#fef9c3" : "#f1f5f9", color: batchRunning ? "#92400e" : "#374151" }}
            onClick={triggerBatch}
            disabled={batchRunning}
          >
            {batchRunning ? "Ejecutando..." : "Recalcular Riesgos"}
          </button>
        </div>
      </div>

      {batchJobs.length > 0 && (
        <div style={{ marginBottom: 16, background: "#fff", border: "1px solid #e2e8f0", borderRadius: 8, padding: "12px 16px" }}>
          <p style={{ fontSize: 12, fontWeight: 600, color: "#374151", margin: "0 0 8px" }}>Jobs activos</p>
          {batchJobs.map(j => (
            <div key={j.id} style={{ display: "flex", gap: 12, alignItems: "center", fontSize: 12, color: "#64748b", marginBottom: 4 }}>
              <span style={{ fontWeight: 600, color: j.status === "Completed" ? "#22c55e" : j.status === "Failed" ? "#ef4444" : "#6366f1" }}>{j.status}</span>
              <span>{j.jobName}</span>
              {j.totalRecords > 0 && <span>{j.processedRecords}/{j.totalRecords} registros</span>}
              {j.errorMessage && <span style={{ color: "#ef4444" }}>{j.errorMessage}</span>}
            </div>
          ))}
        </div>
      )}

      {error && <p style={s.error}>{error}</p>}

      <div style={s.card}>
        {loading ? (
          <p style={s.empty}>Cargando...</p>
        ) : data?.items.length === 0 ? (
          <p style={s.empty}>No hay eventos registrados aún.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Evento</th>
                <th>Credit ID</th>
                <th>Correlation ID</th>
                <th>Ocurrido</th>
                <th>Registrado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {data?.items.map(r => {
                const meta = eventMeta(r.eventType);
                return (
                  <tr key={r.id}>
                    <td>
                      <span style={{ background: meta.color + "20", color: meta.color, padding: "3px 8px", borderRadius: 10, fontSize: 12, fontWeight: 600, whiteSpace: "nowrap" }}>
                        {meta.label}
                      </span>
                    </td>
                    <td>
                      {r.entityId ? (
                        <span style={s.link} onClick={() => loadCreditTrail(r.entityId!)}>
                          {r.entityId.slice(0, 8)}…
                        </span>
                      ) : "—"}
                    </td>
                    <td style={{ fontFamily: "monospace", fontSize: 12, color: "#64748b" }}>
                      {r.correlationId.slice(0, 16)}…
                    </td>
                    <td style={s.ts}>{fmtDate(r.occurredOn)}</td>
                    <td style={s.ts}>{fmtDate(r.recordedAt)}</td>
                    <td>
                      <button style={s.btnXs} onClick={() => setSelected(r)}>JSON</button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {data && data.totalPages > 1 && (
        <div style={s.pager}>
          <button style={s.btnSm} disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← Anterior</button>
          <span style={{ color: "#64748b", fontSize: 13 }}>Página {page} de {data.totalPages} · {data.totalCount} eventos</span>
          <button style={s.btnSm} disabled={page >= data.totalPages} onClick={() => setPage(p => p + 1)}>Siguiente →</button>
        </div>
      )}

      {selected && (
        <Modal title={`Payload — ${eventMeta(selected.eventType).label}`} onClose={() => setSelected(null)}>
          <PayloadViewer record={selected} />
        </Modal>
      )}

      {creditTrail && (
        <Modal title={`Trayectoria del Crédito ${creditTrail.creditId.slice(0, 8)}…`} onClose={() => setCreditTrail(null)}>
          {trailLoading ? (
            <p style={{ color: "#64748b", textAlign: "center", padding: 20 }}>Cargando...</p>
          ) : (
            <CreditTimeline records={creditTrail.records} />
          )}
        </Modal>
      )}
    </div>
  );
}

function CreditTimeline({ records }: { records: AuditRecord[] }) {
  if (records.length === 0) return <p style={{ color: "#94a3b8", textAlign: "center", padding: 20 }}>Sin eventos.</p>;

  const sorted = [...records].sort((a, b) => new Date(a.occurredOn).getTime() - new Date(b.occurredOn).getTime());

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
      {sorted.map((r, i) => {
        const meta = eventMeta(r.eventType);
        const payload = tryParsePayload(r.payload);
        return (
          <div key={r.id} style={{ display: "flex", gap: 16, paddingBottom: 20, position: "relative" }}>
            {/* Vertical line */}
            {i < sorted.length - 1 && (
              <div style={{ position: "absolute", left: 11, top: 24, bottom: 0, width: 2, background: "#e2e8f0" }} />
            )}
            {/* Dot */}
            <div style={{ width: 24, height: 24, borderRadius: "50%", background: meta.color, flexShrink: 0, display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1 }}>
              <div style={{ width: 8, height: 8, borderRadius: "50%", background: "#fff" }} />
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                <span style={{ fontWeight: 600, fontSize: 14, color: meta.color }}>{meta.label}</span>
                <span style={{ fontSize: 11, color: "#94a3b8" }}>{fmtDate(r.occurredOn)}</span>
              </div>
              <TimelineDetails type={r.eventType} payload={payload} />
            </div>
          </div>
        );
      })}
    </div>
  );
}

function TimelineDetails({ type, payload }: { type: string; payload: Record<string, unknown> | null }) {
  if (!payload) return null;

  if (type === "CreditCreatedIntegrationEvent") {
    return (
      <div style={s.details}>
        <span>Monto: <strong>{fmtCOP(payload["Amount"] as number)}</strong></span>
        <span>Tasa: <strong>{((payload["InterestRate"] as number) * 100).toFixed(1)}%</strong></span>
        <span>Ingreso: <strong>{fmtCOP(payload["MonthlyIncome"] as number)}</strong></span>
        <span>Score: <strong>{payload["ClientCreditScore"] as number}</strong></span>
      </div>
    );
  }

  if (type === "RiskAssessedIntegrationEvent") {
    const decision = payload["Decision"] as string;
    const color = decision === "Approved" ? "#22c55e" : decision === "Rejected" ? "#ef4444" : "#f59e0b";
    return (
      <div style={s.details}>
        <span>Decisión: <strong style={{ color }}>{decision === "Approved" ? "Aprobado" : decision === "Rejected" ? "Rechazado" : "En Revisión"}</strong></span>
        <span>Risk Score: <strong>{(payload["RiskScore"] as number).toFixed(2)}</strong></span>
        <span style={{ gridColumn: "1 / -1" }}>Razón: {String(payload["Reason"])}</span>
      </div>
    );
  }

  if (type === "CreditUpdatedIntegrationEvent") {
    return (
      <div style={s.details}>
        <span>Estado: <strong>{String(payload["NewStatus"])}</strong></span>
        {payload["Reason"] != null && <span>Razón: {String(payload["Reason"])}</span>}
      </div>
    );
  }

  return null;
}

function PayloadViewer({ record }: { record: AuditRecord }) {
  const parsed = tryParsePayload(record.payload);
  return (
    <div>
      <div style={{ marginBottom: 12, display: "flex", gap: 8, fontSize: 12 }}>
        <span style={{ color: "#64748b" }}>ID: <code style={{ fontSize: 11 }}>{record.id}</code></span>
      </div>
      <pre style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: 8, padding: 16, fontSize: 12, overflow: "auto", maxHeight: 320, margin: 0 }}>
        {parsed ? JSON.stringify(parsed, null, 2) : record.payload}
      </pre>
    </div>
  );
}

function tryParsePayload(payload: string): Record<string, unknown> | null {
  try { return JSON.parse(payload); } catch { return null; }
}

function fmtDate(iso: string) {
  const d = new Date(iso);
  return d.toLocaleString("es-CO", { dateStyle: "short", timeStyle: "medium" });
}

function fmtCOP(n: number) {
  return new Intl.NumberFormat("es-CO", { style: "currency", currency: "COP", maximumFractionDigits: 0 }).format(n);
}

const s: Record<string, React.CSSProperties> = {
  header: { display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 20 },
  title: { fontSize: 22, fontWeight: 700, color: "#1e293b", margin: 0 },
  subtitle: { fontSize: 13, color: "#64748b", marginTop: 4 },
  card: { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 10, overflow: "hidden" },
  btnSm: { background: "#f1f5f9", color: "#374151", padding: "7px 14px", fontSize: 13, border: "none", borderRadius: 6, cursor: "pointer" },
  btnXs: { background: "#f1f5f9", color: "#374151", padding: "4px 10px", fontSize: 12, border: "none", borderRadius: 5, cursor: "pointer" },
  error: { color: "#ef4444", fontSize: 13, marginBottom: 12 },
  empty: { padding: 32, textAlign: "center", color: "#94a3b8" },
  link: { color: "#6366f1", cursor: "pointer", fontWeight: 500, fontFamily: "monospace", fontSize: 13 },
  ts: { fontSize: 12, color: "#64748b", whiteSpace: "nowrap" },
  pager: { display: "flex", gap: 12, alignItems: "center", justifyContent: "flex-end", marginTop: 12 },
  details: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 16px", marginTop: 6, fontSize: 13, color: "#475569" },
};
