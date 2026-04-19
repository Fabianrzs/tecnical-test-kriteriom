import { useState, useEffect, useCallback, useRef } from "react";
import { api, CREDIT_STATUS, STATUS_COLORS } from "../api";
import type { Credit, Client, PagedResult, ClientFinancialSummary, CreditsFilter } from "../api";
import { Modal } from "../components/Modal";
import { useToast } from "../components/Toast";

type ClientMap = Record<string, Client>;

const STATUS_OPTIONS = [
  { value: "", label: "Todos los estados" },
  { value: "Pending",     label: "Pendiente" },
  { value: "UnderReview", label: "En Revisión" },
  { value: "Active",      label: "Aprobado" },
  { value: "Rejected",    label: "Rechazado" },
  { value: "Closed",      label: "Cerrado" },
  { value: "Defaulted",   label: "En Mora" },
];

const RISK_OPTIONS = [
  { value: "", label: "Cualquier riesgo" },
  { value: "none",   label: "Sin evaluar" },
  { value: "low",    label: "Bajo (< 30)" },
  { value: "medium", label: "Medio (30–60)" },
  { value: "high",   label: "Alto (> 60)" },
];

const EMPTY_FILTER: CreditsFilter = {};

export function CreditsPage() {
  const [data, setData]           = useState<PagedResult<Credit> | null>(null);
  const [page, setPage]           = useState(1);
  const [loading, setLoading]     = useState(false);
  const [error, setError]         = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [detail, setDetail]       = useState<Credit | null>(null);
  const [updating, setUpdating]   = useState<Credit | null>(null);
  const [pollingId, setPollingId] = useState<string | null>(null);
  const [clientsMap, setClientsMap] = useState<ClientMap>({});
  const pollRef    = useRef<ReturnType<typeof setInterval> | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const { toast }  = useToast();

  // ─── Filter state ───────────────────────────────────────────────────────────
  const [filterStatus,    setFilterStatus]    = useState("");
  const [filterRisk,      setFilterRisk]      = useState("");
  const [filterClientName,setFilterClientName]= useState("");
  const [filterAmountMin, setFilterAmountMin] = useState("");
  const [filterAmountMax, setFilterAmountMax] = useState("");
  const [filterDateFrom,  setFilterDateFrom]  = useState("");
  const [filterDateTo,    setFilterDateTo]    = useState("");

  // committed filter sent to API (debounced for text inputs)
  const [activeFilter, setActiveFilter] = useState<CreditsFilter>(EMPTY_FILTER);

  const activeFilterCount = [
    filterStatus, filterRisk, filterClientName,
    filterAmountMin, filterAmountMax, filterDateFrom, filterDateTo,
  ].filter(Boolean).length;

  // Commit all current field values to the active filter (immediate for selects)
  const commitFilter = useCallback((overrides?: Partial<{
    status: string; risk: string; clientName: string;
    amountMin: string; amountMax: string; dateFrom: string; dateTo: string;
  }>) => {
    const s  = overrides?.status     ?? filterStatus;
    const r  = overrides?.risk       ?? filterRisk;
    const cn = overrides?.clientName ?? filterClientName;
    const mn = overrides?.amountMin  ?? filterAmountMin;
    const mx = overrides?.amountMax  ?? filterAmountMax;
    const df = overrides?.dateFrom   ?? filterDateFrom;
    const dt = overrides?.dateTo     ?? filterDateTo;

    setActiveFilter({
      status:     s  || undefined,
      riskLevel:  r  || undefined,
      clientName: cn || undefined,
      amountMin:  mn ? Number(mn) : undefined,
      amountMax:  mx ? Number(mx) : undefined,
      dateFrom:   df || undefined,
      dateTo:     dt || undefined,
    });
    setPage(1);
  }, [filterStatus, filterRisk, filterClientName, filterAmountMin, filterAmountMax, filterDateFrom, filterDateTo]);

  // Debounce for text/number inputs
  function scheduleCommit(field: string, value: string) {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      commitFilter({ [field]: value } as never);
    }, 400);
  }

  function handleStatusChange(v: string) {
    setFilterStatus(v);
    commitFilter({ status: v });
  }
  function handleRiskChange(v: string) {
    setFilterRisk(v);
    commitFilter({ risk: v });
  }
  function handleClientNameChange(v: string) {
    setFilterClientName(v);
    scheduleCommit("clientName", v);
  }
  function handleAmountMinChange(v: string) {
    setFilterAmountMin(v);
    scheduleCommit("amountMin", v);
  }
  function handleAmountMaxChange(v: string) {
    setFilterAmountMax(v);
    scheduleCommit("amountMax", v);
  }
  function handleDateFromChange(v: string) {
    setFilterDateFrom(v);
    commitFilter({ dateFrom: v });
  }
  function handleDateToChange(v: string) {
    setFilterDateTo(v);
    commitFilter({ dateTo: v });
  }

  function clearFilters() {
    setFilterStatus(""); setFilterRisk(""); setFilterClientName("");
    setFilterAmountMin(""); setFilterAmountMax("");
    setFilterDateFrom(""); setFilterDateTo("");
    setActiveFilter(EMPTY_FILTER);
    setPage(1);
  }

  // ─── Data loading ───────────────────────────────────────────────────────────
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await api.getCredits(page, 20, activeFilter);
      setData(result);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al cargar créditos");
    } finally {
      setLoading(false);
    }
  }, [page, activeFilter]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    api.getClients(1, 200).then(r => {
      const map: ClientMap = {};
      r.items.forEach(c => { map[c.id] = c; });
      setClientsMap(map);
    }).catch(() => {});
  }, []);

  function startPolling(creditId: string) {
    setPollingId(creditId);
    pollRef.current = setInterval(async () => {
      try {
        const credit = await api.getCredit(creditId);
        if (credit.status !== "Pending") {
          clearInterval(pollRef.current!);
          pollRef.current = null;
          setPollingId(null);
          load();
          toast(
            credit.status === "Active"      ? "Crédito aprobado" :
            credit.status === "Rejected"    ? "Crédito rechazado por el motor de riesgo" :
                                              "Evaluación completada. Estado: En revisión",
            credit.status === "Active" ? "success" : credit.status === "Rejected" ? "error" : "info"
          );
        }
      } catch {
        clearInterval(pollRef.current!);
        pollRef.current = null;
        setPollingId(null);
      }
    }, 2000);
  }

  useEffect(() => () => {
    if (pollRef.current) clearInterval(pollRef.current);
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  // ─── Render ─────────────────────────────────────────────────────────────────
  return (
    <div>
      {/* Header */}
      <div style={s.header}>
        <h1 style={s.title}>Créditos</h1>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          {pollingId && (
            <span style={s.pollingBadge}>Evaluando riesgo...</span>
          )}
          <button style={s.btnSm} onClick={load} disabled={loading}>
            {loading ? "Cargando..." : "Actualizar"}
          </button>
          <button style={s.btnPrimary} onClick={() => setShowCreate(true)}>
            + Nuevo Crédito
          </button>
        </div>
      </div>

      {/* Filter bar — always visible */}
      <div style={s.filterBar}>
        <div style={s.filterGrid}>
          <FilterField label="Estado">
            <select value={filterStatus} onChange={e => handleStatusChange(e.target.value)}>
              {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </FilterField>

          <FilterField label="Nivel de riesgo">
            <select value={filterRisk} onChange={e => handleRiskChange(e.target.value)}>
              {RISK_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </FilterField>

          <FilterField label="Nombre del cliente">
            <input
              placeholder="Buscar por nombre..."
              value={filterClientName}
              onChange={e => handleClientNameChange(e.target.value)}
            />
          </FilterField>

          <FilterField label="Monto mínimo (COP)">
            <input
              type="number"
              placeholder="ej: 1.000.000"
              value={filterAmountMin}
              onChange={e => handleAmountMinChange(e.target.value)}
            />
          </FilterField>

          <FilterField label="Monto máximo (COP)">
            <input
              type="number"
              placeholder="ej: 50.000.000"
              value={filterAmountMax}
              onChange={e => handleAmountMaxChange(e.target.value)}
            />
          </FilterField>

          <FilterField label="Fecha desde">
            <input type="date" value={filterDateFrom} onChange={e => handleDateFromChange(e.target.value)} />
          </FilterField>

          <FilterField label="Fecha hasta">
            <input type="date" value={filterDateTo} onChange={e => handleDateToChange(e.target.value)} />
          </FilterField>

          <FilterField label=" ">
            <button
              style={{ ...s.btnClear, opacity: activeFilterCount > 0 ? 1 : 0.4 }}
              onClick={clearFilters}
              disabled={activeFilterCount === 0}
            >
              {activeFilterCount > 0 ? `✕ Limpiar (${activeFilterCount})` : "Sin filtros"}
            </button>
          </FilterField>
        </div>

        {activeFilterCount > 0 && data && (
          <div style={s.filterSummary}>
            {data.totalCount} resultado{data.totalCount !== 1 ? "s" : ""} con {activeFilterCount} filtro{activeFilterCount !== 1 ? "s" : ""} activo{activeFilterCount !== 1 ? "s" : ""}
          </div>
        )}
      </div>

      {error && <p style={s.error}>{error}</p>}

      {/* Table */}
      <div style={s.card}>
        {loading ? (
          <p style={s.empty}>Cargando...</p>
        ) : data?.items.length === 0 ? (
          <p style={s.empty}>
            {activeFilterCount > 0
              ? "Ningún crédito coincide con los filtros aplicados."
              : "No hay créditos. Crea un cliente primero, luego solicita un crédito."}
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Cliente</th>
                <th>Monto / Cuota</th>
                <th>Condiciones</th>
                <th>Estado</th>
                <th>Risk Score</th>
                <th>Creado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {data?.items.map(c => {
                const client = clientsMap[c.clientId];
                const cuota  = computeMonthlyPayment(c.amount, c.interestRate, c.termMonths);
                const dti    = client && client.monthlyIncome > 0 ? (cuota / client.monthlyIncome) * 100 : null;
                return (
                  <tr key={c.id}>
                    <td>
                      <span style={s.link} onClick={() => setDetail(c)}>
                        {client ? client.fullName : c.clientId.slice(0, 8) + "…"}
                      </span>
                      {client && <><br /><span style={s.muted}>Score: {client.creditScore}</span></>}
                    </td>
                    <td>
                      <span style={{ fontWeight: 600 }}>{fmt(c.amount)}</span>
                      <br /><span style={s.muted}>{fmt(cuota)}/mes</span>
                    </td>
                    <td>
                      <span>{(c.interestRate * 100).toFixed(1)}% E.A.</span>
                      <br /><span style={s.muted}>{c.termMonths} meses</span>
                    </td>
                    <td>
                      <StatusBadge status={c.status} />
                      {dti !== null && c.status === "Active" && (
                        <><br />
                          <span style={{ fontSize: 11, color: dti < 30 ? "#22c55e" : dti < 60 ? "#f59e0b" : "#ef4444" }}>
                            DTI {dti.toFixed(1)}%
                          </span>
                        </>
                      )}
                    </td>
                    <td>
                      {c.riskScore != null ? <RiskScoreIndicator score={c.riskScore} /> : "—"}
                    </td>
                    <td style={s.muted}>{new Date(c.createdAt).toLocaleDateString("es-CO")}</td>
                    <td>
                      <button style={s.btnSm} onClick={() => setUpdating(c)}>Estado</button>
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
          <span style={s.muted}>{data.totalCount} créditos</span>
          <button style={s.btnSm} disabled={!data.hasPreviousPage} onClick={() => setPage(p => p - 1)}>← Anterior</button>
          <span style={s.muted}>Página {page} de {data.totalPages}</span>
          <button style={s.btnSm} disabled={!data.hasNextPage} onClick={() => setPage(p => p + 1)}>Siguiente →</button>
        </div>
      )}

      {/* Modals */}
      {showCreate && (
        <CreateCreditModal
          onClose={() => setShowCreate(false)}
          onSaved={creditId => {
            setShowCreate(false);
            load();
            toast("Crédito creado. Evaluando riesgo...", "info");
            startPolling(creditId);
          }}
        />
      )}

      {detail && (
        <Modal title="Detalle del Crédito" onClose={() => setDetail(null)} maxWidth={520}>
          <CreditDetail credit={detail} client={clientsMap[detail.clientId]} />
        </Modal>
      )}

      {updating && (
        <UpdateStatusModal
          credit={updating}
          onClose={() => setUpdating(null)}
          onSaved={() => { setUpdating(null); load(); toast("Estado actualizado", "success"); }}
        />
      )}
    </div>
  );
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function FilterField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <label style={{ fontSize: 11, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: 0.4 }}>
        {label}
      </label>
      {children}
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const color = STATUS_COLORS[status] ?? "#6b7280";
  return (
    <span style={{ background: color + "20", color, padding: "3px 8px", borderRadius: 12, fontSize: 12, fontWeight: 600 }}>
      {CREDIT_STATUS[status] ?? status}
    </span>
  );
}

function RiskScoreIndicator({ score }: { score: number }) {
  const color = score < 30 ? "#22c55e" : score < 60 ? "#f59e0b" : "#ef4444";
  const label = score < 30 ? "Bajo" : score < 60 ? "Medio" : "Alto";
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
        <span style={{ fontWeight: 600, fontSize: 13, color }}>{score.toFixed(1)}</span>
        <span style={{ fontSize: 11, color, background: color + "20", padding: "1px 6px", borderRadius: 8 }}>{label}</span>
      </div>
      <div style={{ height: 4, width: 60, background: "#e2e8f0", borderRadius: 2, overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${Math.min(100, score)}%`, background: color, borderRadius: 2 }} />
      </div>
    </div>
  );
}

function computeMonthlyPayment(amount: number, annualRate: number, termMonths: number): number {
  const r = annualRate / 12;
  if (r === 0) return amount / termMonths;
  return amount * r / (1 - Math.pow(1 + r, -termMonths));
}

// ─── Credit detail modal ──────────────────────────────────────────────────────

function CreditDetail({ credit, client }: { credit: Credit; client?: Client }) {
  const cuota       = computeMonthlyPayment(credit.amount, credit.interestRate, credit.termMonths);
  const dti         = client && client.monthlyIncome > 0 ? (cuota / client.monthlyIncome) * 100 : null;
  const statusColor = STATUS_COLORS[credit.status] ?? "#6b7280";
  const riskColor   = credit.riskScore != null
    ? (credit.riskScore < 30 ? "#22c55e" : credit.riskScore < 60 ? "#f59e0b" : "#ef4444")
    : "#6b7280";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 10 }}>
        <SummaryCard label="Monto"      value={fmt(credit.amount)}                         sub={`${fmt(cuota)}/mes`} color="#6366f1" />
        <SummaryCard label="Estado"     value={CREDIT_STATUS[credit.status] ?? credit.status} color={statusColor} />
        {credit.riskScore != null
          ? <SummaryCard label="Risk Score" value={credit.riskScore.toFixed(1)} sub={credit.riskScore < 30 ? "Bajo riesgo" : credit.riskScore < 60 ? "Riesgo medio" : "Alto riesgo"} color={riskColor} />
          : <SummaryCard label="Risk Score" value="Pendiente" color="#94a3b8" />
        }
      </div>

      {client && (
        <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 8, padding: "10px 14px", fontSize: 13 }}>
          <div style={{ fontWeight: 600, color: "#166534", marginBottom: 4 }}>{client.fullName}</div>
          <div style={{ display: "flex", gap: 16, color: "#166534", fontSize: 12 }}>
            <span>Score: <strong>{client.creditScore}</strong></span>
            <span>Ingresos: <strong>{fmt(client.monthlyIncome)}/mes</strong></span>
            {dti !== null && <span>DTI cuota: <strong>{dti.toFixed(1)}%</strong></span>}
          </div>
        </div>
      )}

      {dti !== null && (
        <div>
          <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, color: "#64748b", marginBottom: 4 }}>
            <span>Participación en capacidad de pago</span>
            <span style={{ fontWeight: 600, color: dti < 30 ? "#22c55e" : dti < 60 ? "#f59e0b" : "#ef4444" }}>{dti.toFixed(1)}% / 60%</span>
          </div>
          <div style={{ height: 8, background: "#e2e8f0", borderRadius: 4, overflow: "hidden" }}>
            <div style={{ height: "100%", width: `${Math.min(dti / 60 * 100, 100)}%`, background: dti < 30 ? "#22c55e" : dti < 60 ? "#f59e0b" : "#ef4444", borderRadius: 4 }} />
          </div>
        </div>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {[
          ["Condiciones", `${(credit.interestRate * 100).toFixed(2)}% E.A. · ${credit.termMonths} meses`],
          ["Decisión",    credit.decision ?? "—"],
          ["Razón",       credit.reason   ?? "—"],
          ["Creado",      new Date(credit.createdAt).toLocaleString("es-CO")],
          ["Actualizado", new Date(credit.updatedAt).toLocaleString("es-CO")],
          ["ID",          credit.id],
        ].map(([k, v]) => (
          <div key={k} style={{ display: "flex", justifyContent: "space-between", borderBottom: "1px solid #f1f5f9", paddingBottom: 6 }}>
            <span style={{ color: "#64748b", fontSize: 13 }}>{k}</span>
            <span style={{ fontWeight: 500, fontSize: 12, maxWidth: 300, textAlign: "right", wordBreak: "break-all" }}>{v}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function SummaryCard({ label, value, sub, color }: { label: string; value: string; sub?: string; color: string }) {
  return (
    <div style={{ background: color + "10", border: `1px solid ${color}30`, borderRadius: 8, padding: "10px 12px", textAlign: "center" }}>
      <div style={{ fontSize: 11, color: "#64748b", fontWeight: 600, textTransform: "uppercase", letterSpacing: 0.5, marginBottom: 4 }}>{label}</div>
      <div style={{ fontWeight: 700, fontSize: 14, color }}>{value}</div>
      {sub && <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>{sub}</div>}
    </div>
  );
}

// ─── Create credit modal (with client combobox) ───────────────────────────────

function CreateCreditModal({ onClose, onSaved }: { onClose: () => void; onSaved: (creditId: string) => void }) {
  const [clientId,      setClientId]      = useState("");
  const [selectedClient,setSelectedClient]= useState<Client | null>(null);
  const [amount,        setAmount]        = useState("");
  const [interestRate,  setInterestRate]  = useState("0.18");
  const [termMonths,    setTermMonths]    = useState("36");
  const [loading,       setLoading]       = useState(false);
  const [error,         setError]         = useState("");
  const [summary,       setSummary]       = useState<ClientFinancialSummary | null>(null);

  useEffect(() => {
    if (!clientId) { setSummary(null); return; }
    api.getClientFinancialSummary(clientId).then(setSummary).catch(() => setSummary(null));
  }, [clientId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!clientId) { setError("Selecciona un cliente"); return; }
    setLoading(true); setError("");
    try {
      const credit = await api.createCredit({
        clientId, amount: Number(amount),
        interestRate: Number(interestRate), termMonths: Number(termMonths),
      });
      onSaved(credit.id);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al crear crédito");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Modal title="Nueva Solicitud de Crédito" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Field label="Cliente">
          <ClientCombobox value={clientId} onChange={(id, c) => { setClientId(id); setSelectedClient(c ?? null); }} />
        </Field>

        {selectedClient && (
          <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 8, padding: "10px 14px", fontSize: 13, color: "#166534" }}>
            Ingresos: <strong>{fmt(selectedClient.monthlyIncome)}</strong> · Score: <strong>{selectedClient.creditScore}</strong>
            {summary && summary.activeCreditsCount > 0 && (
              <> · Deuda actual: <strong>{fmt(summary.existingMonthlyDebt)}/mes</strong> ({summary.activeCreditsCount} crédito{summary.activeCreditsCount !== 1 ? "s" : ""})</>
            )}
          </div>
        )}

        <Field label="Monto solicitado (COP)">
          <input type="number" min={100000} value={amount} onChange={e => setAmount(e.target.value)} required placeholder="ej: 10000000" />
        </Field>
        <Field label="Tasa de interés anual (decimal)">
          <input type="number" step="0.01" min={0.01} max={1} value={interestRate} onChange={e => setInterestRate(e.target.value)} required />
          <span style={{ fontSize: 12, color: "#94a3b8" }}>{(Number(interestRate) * 100).toFixed(1)}% anual</span>
        </Field>
        <Field label="Plazo (meses)">
          <input type="number" min={6} max={120} step={6} value={termMonths} onChange={e => setTermMonths(e.target.value)} required />
          <span style={{ fontSize: 12, color: "#94a3b8" }}>Entre 6 y 120 meses</span>
        </Field>

        {amount && selectedClient && (
          <DTIPreview
            amount={Number(amount)} rate={Number(interestRate)} term={Number(termMonths)}
            income={selectedClient.monthlyIncome}
            existingMonthlyDebt={summary?.existingMonthlyDebt ?? 0}
          />
        )}

        {error && <p style={{ color: "#ef4444", fontSize: 13 }}>{error}</p>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <button type="button" style={s.btnSm} onClick={onClose}>Cancelar</button>
          <button type="submit" style={s.btnPrimary} disabled={loading || !clientId}>
            {loading ? "Enviando..." : "Solicitar Crédito"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

// ─── Client combobox (search-as-you-type, server-side) ────────────────────────

function ClientCombobox({ value, onChange }: {
  value: string;
  onChange: (id: string, client?: Client) => void;
}) {
  const [inputValue,  setInputValue]  = useState("");
  const [results,     setResults]     = useState<Client[]>([]);
  const [open,        setOpen]        = useState(false);
  const [searching,   setSearching]   = useState(false);
  const debounceRef   = useRef<ReturnType<typeof setTimeout> | null>(null);
  const containerRef  = useRef<HTMLDivElement>(null);

  const doSearch = useCallback(async (q: string) => {
    if (q.length < 3) { setResults([]); setOpen(false); return; }
    setSearching(true);
    try {
      const data = await api.searchClients(q);
      setResults(data.items);
      setOpen(true);
    } catch { /* ignore */ }
    finally { setSearching(false); }
  }, []);

  function handleInput(e: React.ChangeEvent<HTMLInputElement>) {
    const q = e.target.value;
    setInputValue(q);
    if (value) onChange("");
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => doSearch(q), 300);
  }

  function select(client: Client) {
    setInputValue(client.fullName);
    setOpen(false);
    setResults([]);
    onChange(client.id, client);
  }

  function handleBlur(e: React.FocusEvent) {
    if (!containerRef.current?.contains(e.relatedTarget as Node)) {
      setTimeout(() => setOpen(false), 150);
    }
  }

  return (
    <div ref={containerRef} style={{ position: "relative" }} onBlur={handleBlur}>
      <div style={{ position: "relative" }}>
        <input
          value={inputValue}
          onChange={handleInput}
          onFocus={() => inputValue.length >= 3 && results.length > 0 && setOpen(true)}
          placeholder="Escribe al menos 3 letras del nombre..."
          autoComplete="off"
          style={{
            boxShadow: value ? "0 0 0 2px #bbf7d0" : undefined,
            borderColor: value ? "#22c55e" : undefined,
            outline: "none",
          }}
        />
        <span style={{ position: "absolute", right: 10, top: "50%", transform: "translateY(-50%)", fontSize: 13 }}>
          {searching ? <span style={{ color: "#6366f1" }}>...</span> : value ? <span style={{ color: "#22c55e" }}>✓</span> : null}
        </span>
      </div>

      {inputValue.length > 0 && inputValue.length < 3 && !value && (
        <p style={{ fontSize: 11, color: "#94a3b8", margin: "3px 0 0" }}>
          Escribe {3 - inputValue.length} letra{3 - inputValue.length !== 1 ? "s" : ""} más
        </p>
      )}

      {open && (
        <div style={s.dropdown}>
          {results.length === 0 ? (
            <div style={{ padding: "10px 14px", fontSize: 13, color: "#94a3b8" }}>Sin resultados para "{inputValue}"</div>
          ) : results.map(c => (
            <div key={c.id} onMouseDown={() => select(c)} style={s.dropdownItem}>
              <div style={{ fontWeight: 600, fontSize: 13 }}>{c.fullName}</div>
              <div style={{ fontSize: 12, color: "#64748b" }}>
                Score: <strong>{c.creditScore}</strong> · {fmtShort(c.monthlyIncome)}/mes · {c.documentNumber}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── DTI Preview ──────────────────────────────────────────────────────────────

function DTIPreview({ amount, rate, term, income, existingMonthlyDebt }: {
  amount: number; rate: number; term: number; income: number; existingMonthlyDebt: number;
}) {
  const r = rate / 12;
  const n = term || 36;
  const newCuota  = r === 0 ? amount / n : amount * r / (1 - Math.pow(1 + r, -n));
  const totalDebt = existingMonthlyDebt + newCuota;
  const totalDti  = (totalDebt / income) * 100;
  const newDti    = (newCuota  / income) * 100;
  const color     = totalDti < 30 ? "#22c55e" : totalDti < 60 ? "#f59e0b" : "#ef4444";
  const decision  = totalDti > 60 ? "Probable: Rechazado (supera 60% DTI)"
                  : newDti   < 30 ? "Probable: Aprobado"
                  :                  "Probable: En Revisión";
  return (
    <div style={{ background: color + "15", border: `1px solid ${color}40`, borderRadius: 8, padding: "10px 14px", fontSize: 13, display: "flex", flexDirection: "column", gap: 4 }}>
      <div>Cuota nueva: <strong>{fmt(newCuota)}/mes</strong></div>
      {existingMonthlyDebt > 0 && (
        <div style={{ color: "#64748b" }}>Deuda existente: {fmt(existingMonthlyDebt)}/mes · Total: {fmt(totalDebt)}/mes</div>
      )}
      <div>DTI total: <strong style={{ color }}>{totalDti.toFixed(1)}%</strong> · {decision}</div>
    </div>
  );
}

// ─── Update status modal ──────────────────────────────────────────────────────

function UpdateStatusModal({ credit, onClose, onSaved }: { credit: Credit; onClose: () => void; onSaved: () => void }) {
  const OPTS = [
    { value: 0, label: "Pendiente" },
    { value: 1, label: "En Revisión" },
    { value: 2, label: "Aprobado (Active)" },
    { value: 3, label: "Rechazado" },
    { value: 4, label: "Cerrado" },
    { value: 5, label: "En Mora" },
  ];
  const [newStatus, setNewStatus] = useState(0);
  const [reason,    setReason]    = useState("");
  const [loading,   setLoading]   = useState(false);
  const [error,     setError]     = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true); setError("");
    try {
      await api.updateCreditStatus(credit.id, { newStatus, reason });
      onSaved();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al actualizar estado");
    } finally { setLoading(false); }
  }

  return (
    <Modal title="Cambiar Estado del Crédito" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <p style={{ fontSize: 13, color: "#64748b" }}>Estado actual: <StatusBadge status={credit.status} /></p>
        <Field label="Nuevo estado">
          <select value={newStatus} onChange={e => setNewStatus(Number(e.target.value))}>
            {OPTS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </Field>
        <Field label="Razón">
          <input value={reason} onChange={e => setReason(e.target.value)} required placeholder="Motivo del cambio" />
        </Field>
        {error && <p style={{ color: "#ef4444", fontSize: 13 }}>{error}</p>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <button type="button" style={s.btnSm} onClick={onClose}>Cancelar</button>
          <button type="submit" style={s.btnPrimary} disabled={loading}>
            {loading ? "Guardando..." : "Actualizar"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

// ─── Shared helpers ───────────────────────────────────────────────────────────

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
      <label style={{ fontSize: 13, fontWeight: 500, color: "#374151" }}>{label}</label>
      {children}
    </div>
  );
}

function fmt(n: number) {
  return new Intl.NumberFormat("es-CO", { style: "currency", currency: "COP", maximumFractionDigits: 0 }).format(n);
}
function fmtShort(n: number) {
  if (n >= 1_000_000) return `$${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000)     return `$${(n / 1_000).toFixed(0)}k`;
  return `$${n}`;
}

const s: Record<string, React.CSSProperties> = {
  header:       { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 },
  title:        { fontSize: 24, fontWeight: 800, color: "#0f172a", letterSpacing: "-0.5px" },
  card:         { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, overflow: "hidden", boxShadow: "0 1px 3px rgba(0,0,0,0.06)" },
  filterBar:    { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: "16px 18px", marginBottom: 16, boxShadow: "0 1px 3px rgba(0,0,0,0.06)" },
  filterGrid:   { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(155px, 1fr))", gap: 10 },
  filterSummary:{ fontSize: 12, color: "#6366f1", marginTop: 10, fontWeight: 500 },
  btnPrimary:   { background: "linear-gradient(135deg, #6366f1, #8b5cf6)", color: "#fff", fontWeight: 700, padding: "9px 18px", border: "none", borderRadius: 8, cursor: "pointer", boxShadow: "0 2px 8px rgba(99,102,241,0.3)", fontSize: 13 },
  btnSm:        { background: "#f8fafc", color: "#374151", padding: "7px 14px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 7, cursor: "pointer" },
  btnClear:     { background: "#fff", color: "#ef4444", fontWeight: 700, padding: "7px 12px", fontSize: 12, border: "1px solid #fecaca", borderRadius: 7, cursor: "pointer", width: "100%" },
  muted:        { fontSize: 12, color: "#94a3b8" },
  error:        { color: "#ef4444", fontSize: 13, marginBottom: 12, background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, padding: "10px 14px" },
  empty:        { padding: 48, textAlign: "center", color: "#94a3b8", fontSize: 14 },
  link:         { color: "#6366f1", cursor: "pointer", fontWeight: 600 },
  pager:        { display: "flex", gap: 12, alignItems: "center", justifyContent: "flex-end", marginTop: 14 },
  pollingBadge: { fontSize: 12, color: "#6366f1", background: "#eef2ff", padding: "5px 12px", borderRadius: 20, border: "1px solid #c7d2fe", fontWeight: 600 },
  dropdown:     { position: "absolute", top: "calc(100% + 4px)", left: 0, right: 0, background: "#fff", border: "1px solid #e2e8f0", borderRadius: 10, boxShadow: "0 8px 24px rgba(0,0,0,0.12)", zIndex: 200, maxHeight: 240, overflowY: "auto" },
  dropdownItem: { padding: "10px 14px", cursor: "pointer", borderBottom: "1px solid #f8fafc", fontSize: 13 },
};
