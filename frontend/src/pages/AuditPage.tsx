import { useState, useEffect, useCallback, useRef } from "react";
import { api } from "../api";
import type { AuditRecord, AuditPagedResult, BatchCheckpoint, AuditFilter } from "../api";
import { Modal } from "../components/Modal";
import { useToast } from "../components/Toast";

const EVENT_LABELS: Record<string, { label: string; color: string }> = {
  CreditCreatedIntegrationEvent:  { label: "Crédito Creado",     color: "#6366f1" },
  CreditUpdatedIntegrationEvent:  { label: "Crédito Actualizado", color: "#f59e0b" },
  RiskAssessedIntegrationEvent:   { label: "Riesgo Evaluado",     color: "#22c55e" },
  ClientCreatedIntegrationEvent:  { label: "Cliente Creado",      color: "#0ea5e9" },
  ClientUpdatedIntegrationEvent:  { label: "Cliente Actualizado", color: "#8b5cf6" },
};

function eventMeta(type: string) {
  return EVENT_LABELS[type] ?? { label: type.replace("IntegrationEvent", ""), color: "#6b7280" };
}

const EMPTY_AUDIT_FILTER: AuditFilter = {};

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
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const { toast } = useToast();

  const [inputEventType, setInputEventType] = useState("");
  const [inputDateFrom, setInputDateFrom] = useState("");
  const [inputDateTo, setInputDateTo] = useState("");
  const [inputEntityId, setInputEntityId] = useState("");
  const [activeFilter, setActiveFilter] = useState<AuditFilter>(EMPTY_AUDIT_FILTER);

  const activeFilterCount = [
    activeFilter.eventType,
    activeFilter.dateFrom,
    activeFilter.dateTo,
    activeFilter.entityId,
  ].filter(Boolean).length;

  function commitFilter(overrides: Partial<AuditFilter> = {}) {
    const next: AuditFilter = {
      eventType: inputEventType || undefined,
      dateFrom: inputDateFrom || undefined,
      dateTo: inputDateTo || undefined,
      entityId: inputEntityId || undefined,
      ...overrides,
    };
    setActiveFilter(next);
    setPage(1);
  }

  function clearFilters() {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    setInputEventType(""); setInputDateFrom(""); setInputDateTo(""); setInputEntityId("");
    setActiveFilter(EMPTY_AUDIT_FILTER);
    setPage(1);
  }

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setData(await api.getAuditRecent(page, 50, activeFilter));
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error cargando registros");
    } finally {
      setLoading(false);
    }
  }, [page, activeFilter]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => () => {
    if (batchPollRef.current) clearInterval(batchPollRef.current);
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  const items = data?.items ?? [];

  // Event type counts from current (already-filtered) page
  const eventCounts: Record<string, number> = {};
  items.forEach(r => { eventCounts[r.eventType] = (eventCounts[r.eventType] ?? 0) + 1; });

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

  function exportCsv() {
    if (!items.length) return;
    const header = ["ID", "Tipo", "Entity ID", "Correlation ID", "Ocurrido", "Registrado"];
    const rows = items.map(r => [r.id, r.eventType, r.entityId ?? "", r.correlationId, r.occurredOn, r.recordedAt]);
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
      {/* Header */}
      <div style={s.header}>
        <div>
          <h1 style={s.title}>Auditoría</h1>
          <p style={s.subtitle}>Registro inmutable de todos los eventos del sistema</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <button style={s.btnSm} onClick={load}>Actualizar</button>
          <button style={s.btnSm} onClick={exportCsv} disabled={!items.length}>Exportar CSV</button>
          <button
            style={{ ...s.btnSm, background: batchRunning ? "#fef9c3" : "#f1f5f9", color: batchRunning ? "#92400e" : "#374151" }}
            onClick={triggerBatch}
            disabled={batchRunning}
          >
            {batchRunning ? "Ejecutando..." : "Recalcular Riesgos"}
          </button>
        </div>
      </div>

      {/* Filter bar — always visible */}
      <div style={s.filterBar}>
        <div style={s.filterGrid}>
          <FilterField label="Tipo de evento">
            <select value={inputEventType} onChange={e => {
              setInputEventType(e.target.value);
              commitFilter({ eventType: e.target.value || undefined });
            }}>
              <option value="">Todos los eventos</option>
              {Object.entries(EVENT_LABELS).map(([key, meta]) => (
                <option key={key} value={key}>{meta.label}</option>
              ))}
            </select>
          </FilterField>
          <FilterField label="Entity ID">
            <input
              placeholder="UUID parcial..."
              value={inputEntityId}
              onChange={e => {
                const v = e.target.value;
                setInputEntityId(v);
                if (debounceRef.current) clearTimeout(debounceRef.current);
                debounceRef.current = setTimeout(() => commitFilter({ entityId: v || undefined }), 400);
              }}
            />
          </FilterField>
          <FilterField label="Desde (fecha)">
            <input type="date" value={inputDateFrom} onChange={e => {
              setInputDateFrom(e.target.value);
              commitFilter({ dateFrom: e.target.value || undefined });
            }} />
          </FilterField>
          <FilterField label="Hasta (fecha)">
            <input type="date" value={inputDateTo} onChange={e => {
              setInputDateTo(e.target.value);
              commitFilter({ dateTo: e.target.value || undefined });
            }} />
          </FilterField>
          <FilterField label=" ">
            <button
              style={{ ...s.btnSm, width: "100%", color: activeFilterCount > 0 ? "#ef4444" : "#94a3b8" }}
              onClick={clearFilters}
            >
              {activeFilterCount > 0 ? `✕ Limpiar (${activeFilterCount})` : "Sin filtros"}
            </button>
          </FilterField>
        </div>
        {activeFilterCount > 0 && data && (
          <div style={{ fontSize: 12, color: "#6366f1", marginTop: 8 }}>
            {data.totalCount} evento{data.totalCount !== 1 ? "s" : ""} · {activeFilterCount} filtro{activeFilterCount !== 1 ? "s" : ""} activo{activeFilterCount !== 1 ? "s" : ""}
          </div>
        )}
      </div>

      {/* Event type pills */}
      {Object.keys(eventCounts).length > 0 && (
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 14 }}>
          {Object.entries(eventCounts).map(([type, count]) => {
            const meta = eventMeta(type);
            const isActive = activeFilter.eventType === type;
            return (
              <button
                key={type}
                onClick={() => {
                  const next = isActive ? "" : type;
                  setInputEventType(next);
                  commitFilter({ eventType: next || undefined });
                }}
                style={{
                  background: isActive ? meta.color : meta.color + "15",
                  color: isActive ? "#fff" : meta.color,
                  border: `1px solid ${meta.color}40`,
                  borderRadius: 20, padding: "4px 12px", fontSize: 12, fontWeight: 600,
                  cursor: "pointer", transition: "all 0.15s",
                }}
              >
                {meta.label} <span style={{ opacity: 0.8, marginLeft: 4 }}>{count}</span>
              </button>
            );
          })}
        </div>
      )}

      {/* Batch jobs panel */}
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

      {/* Table */}
      <div style={s.card}>
        {loading ? (
          <p style={s.empty}>Cargando...</p>
        ) : items.length === 0 ? (
          <p style={s.empty}>
            {activeFilterCount > 0
              ? "Ningún evento coincide con los filtros aplicados."
              : "No hay eventos registrados aún."}
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Evento</th>
                <th>Detalles</th>
                <th>Credit ID</th>
                <th>Correlation ID</th>
                <th>Ocurrido</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map(r => {
                const meta = eventMeta(r.eventType);
                const payload = tryParsePayload(r.payload);
                return (
                  <tr key={r.id}>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <span style={{ background: meta.color + "20", color: meta.color, padding: "3px 8px", borderRadius: 10, fontSize: 12, fontWeight: 600 }}>
                        {meta.label}
                      </span>
                    </td>
                    <td style={{ maxWidth: 260 }}>
                      <InlineDetails type={r.eventType} payload={payload} />
                    </td>
                    <td>
                      {r.entityId ? (
                        <span style={s.link} onClick={() => loadCreditTrail(r.entityId!)}>
                          {r.entityId.slice(0, 8)}…
                        </span>
                      ) : "—"}
                    </td>
                    <td style={{ fontFamily: "monospace", fontSize: 11, color: "#94a3b8" }}>
                      {r.correlationId.slice(0, 12)}…
                    </td>
                    <td style={s.ts}>{fmtDate(r.occurredOn)}</td>
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

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div style={s.pager}>
          <span style={{ color: "#64748b", fontSize: 12 }}>{data.totalCount} eventos totales</span>
          <button style={s.btnSm} disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← Anterior</button>
          <span style={{ color: "#64748b", fontSize: 13 }}>Página {page} de {data.totalPages}</span>
          <button style={s.btnSm} disabled={page >= data.totalPages} onClick={() => setPage(p => p + 1)}>Siguiente →</button>
        </div>
      )}

      {/* Modals */}
      {selected && (
        <Modal title={`Payload — ${eventMeta(selected.eventType).label}`} onClose={() => setSelected(null)}>
          <PayloadViewer record={selected} />
        </Modal>
      )}

      {creditTrail && (
        <Modal title={`Trayectoria del Crédito ${creditTrail.creditId.slice(0, 8)}…`} onClose={() => setCreditTrail(null)} maxWidth={560}>
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

// ─── Inline compact detail shown in table ────────────────────────────────────

function InlineDetails({ type, payload }: { type: string; payload: Record<string, unknown> | null }) {
  if (!payload) return <span style={{ color: "#94a3b8", fontSize: 12 }}>—</span>;

  if (type === "CreditCreatedIntegrationEvent") {
    return (
      <span style={{ fontSize: 12, color: "#475569" }}>
        {fmtCOP(payload["Amount"] as number)}
        {" · "}
        {((payload["InterestRate"] as number) * 100).toFixed(1)}% E.A.
        {" · Score: "}
        {payload["ClientCreditScore"] as number}
      </span>
    );
  }
  if (type === "RiskAssessedIntegrationEvent") {
    const decision = payload["Decision"] as string;
    const color = decision === "Approved" ? "#22c55e" : decision === "Rejected" ? "#ef4444" : "#f59e0b";
    return (
      <span style={{ fontSize: 12, color }}>
        {decision === "Approved" ? "Aprobado" : decision === "Rejected" ? "Rechazado" : "En Revisión"}
        {" · Score: "}
        <strong>{(payload["RiskScore"] as number).toFixed(2)}</strong>
      </span>
    );
  }
  if (type === "CreditUpdatedIntegrationEvent") {
    return (
      <span style={{ fontSize: 12, color: "#475569" }}>
        → <strong>{String(payload["NewStatus"])}</strong>
        {payload["Reason"] ? ` · ${String(payload["Reason"]).slice(0, 40)}` : ""}
      </span>
    );
  }
  if (type === "ClientCreatedIntegrationEvent" || type === "ClientUpdatedIntegrationEvent") {
    return (
      <span style={{ fontSize: 12, color: "#475569" }}>
        {String(payload["FullName"] ?? "")}
      </span>
    );
  }
  return <span style={{ fontSize: 12, color: "#94a3b8" }}>—</span>;
}

// ─── Credit Timeline ──────────────────────────────────────────────────────────

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
            {i < sorted.length - 1 && (
              <div style={{ position: "absolute", left: 11, top: 24, bottom: 0, width: 2, background: "#e2e8f0" }} />
            )}
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
  if (type === "ClientCreatedIntegrationEvent" || type === "ClientUpdatedIntegrationEvent") {
    return (
      <div style={s.details}>
        {payload["FullName"] != null && <span>Nombre: <strong>{String(payload["FullName"])}</strong></span>}
        {payload["MonthlyIncome"] != null && <span>Ingreso: <strong>{fmtCOP(payload["MonthlyIncome"] as number)}</strong></span>}
      </div>
    );
  }
  return null;
}

// ─── Payload Viewer ───────────────────────────────────────────────────────────

function PayloadViewer({ record }: { record: AuditRecord }) {
  const parsed = tryParsePayload(record.payload);
  const meta = eventMeta(record.eventType);
  return (
    <div>
      <div style={{ marginBottom: 12, display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
        <span style={{ background: meta.color + "20", color: meta.color, padding: "3px 8px", borderRadius: 8, fontSize: 12, fontWeight: 600 }}>{meta.label}</span>
        <span style={{ fontSize: 12, color: "#94a3b8" }}>{fmtDate(record.occurredOn)}</span>
      </div>
      <pre style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: 8, padding: 16, fontSize: 12, overflow: "auto", maxHeight: 360, margin: 0, lineHeight: 1.6 }}>
        {parsed ? JSON.stringify(parsed, null, 2) : record.payload}
      </pre>
    </div>
  );
}

// ─── Filter field ─────────────────────────────────────────────────────────────

function FilterField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <label style={{ fontSize: 11, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: 0.5 }}>{label}</label>
      {children}
    </div>
  );
}

// ─── Utils ────────────────────────────────────────────────────────────────────

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
  header: { display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 18 },
  title: { fontSize: 24, fontWeight: 800, color: "#0f172a", letterSpacing: "-0.5px", margin: 0 },
  subtitle: { fontSize: 13, color: "#64748b", marginTop: 3 },
  card: { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden", boxShadow: "0 1px 3px rgba(0,0,0,0.06)" },
  filterBar: { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: "16px 18px", marginBottom: 16, boxShadow: "0 1px 3px rgba(0,0,0,0.06)" },
  filterGrid: { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))", gap: 12 },
  btnSm: { background: "#f8fafc", color: "#374151", padding: "7px 14px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 7, cursor: "pointer" },
  btnXs: { background: "#f1f5f9", color: "#374151", padding: "4px 10px", fontSize: 12, border: "1px solid #e2e8f0", borderRadius: 6, cursor: "pointer" },
  error: { color: "#ef4444", fontSize: 13, marginBottom: 12, background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, padding: "10px 14px" },
  empty: { padding: 48, textAlign: "center", color: "#94a3b8", fontSize: 14 },
  link: { color: "#6366f1", cursor: "pointer", fontWeight: 600, fontFamily: "monospace", fontSize: 12 },
  ts: { fontSize: 12, color: "#64748b", whiteSpace: "nowrap" },
  pager: { display: "flex", gap: 12, alignItems: "center", justifyContent: "flex-end", marginTop: 14 },
  details: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 16px", marginTop: 6, fontSize: 13, color: "#475569" },
};
