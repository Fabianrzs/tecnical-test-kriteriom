import { useState, useEffect, useCallback, useRef } from "react";
import { api, CREDIT_STATUS, STATUS_COLORS } from "../api";
import type { Credit, Client, PagedResult, ClientFinancialSummary } from "../api";
import { Modal } from "../components/Modal";
import { useToast } from "../components/Toast";

type ClientMap = Record<string, Client>;

export function CreditsPage() {
  const [data, setData] = useState<PagedResult<Credit> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [detail, setDetail] = useState<Credit | null>(null);
  const [updating, setUpdating] = useState<Credit | null>(null);
  const [pollingId, setPollingId] = useState<string | null>(null);
  const [clientsMap, setClientsMap] = useState<ClientMap>({});
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const { toast } = useToast();

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await api.getCredits(page, 10);
      setData(result);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al cargar créditos");
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    api.getClients(1, 100).then(r => {
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
            credit.status === "Active"
              ? "Crédito aprobado"
              : credit.status === "Rejected"
              ? "Crédito rechazado por el motor de riesgo"
              : "Evaluación completada. Estado: En revisión",
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

  useEffect(() => () => { if (pollRef.current) clearInterval(pollRef.current); }, []);

  return (
    <div>
      <div style={s.header}>
        <h1 style={s.title}>Créditos</h1>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          {pollingId && (
            <span style={{ fontSize: 12, color: "#6366f1", background: "#eef2ff", padding: "5px 10px", borderRadius: 6 }}>
              Evaluando riesgo...
            </span>
          )}
          <button style={s.btnSm} onClick={load} disabled={loading}>{loading ? "Actualizando..." : "Actualizar"}</button>
          <button style={s.btnPrimary} onClick={() => setShowCreate(true)}>+ Nuevo Crédito</button>
        </div>
      </div>

      {error && <p style={s.error}>{error}</p>}

      <div style={s.card}>
        {loading ? (
          <p style={s.empty}>Cargando...</p>
        ) : data?.items.length === 0 ? (
          <p style={s.empty}>No hay créditos. Crea un cliente primero, luego solicita un crédito.</p>
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
                const cuota = computeMonthlyPayment(c.amount, c.interestRate, c.termMonths);
                const dti = client && client.monthlyIncome > 0 ? (cuota / client.monthlyIncome) * 100 : null;
                return (
                  <tr key={c.id}>
                    <td>
                      <span style={s.link} onClick={() => setDetail(c)}>
                        {client ? client.fullName : c.clientId.slice(0, 8) + "…"}
                      </span>
                      {client && (
                        <>
                          <br />
                          <span style={s.muted}>Score: {client.creditScore}</span>
                        </>
                      )}
                    </td>
                    <td>
                      <span style={{ fontWeight: 600 }}>{fmt(c.amount)}</span>
                      <br />
                      <span style={s.muted}>{fmt(cuota)}/mes</span>
                    </td>
                    <td>
                      <span>{(c.interestRate * 100).toFixed(1)}% E.A.</span>
                      <br />
                      <span style={s.muted}>{c.termMonths} meses</span>
                    </td>
                    <td>
                      <StatusBadge status={c.status} />
                      {dti !== null && c.status === "Active" && (
                        <>
                          <br />
                          <span style={{ fontSize: 11, color: dti < 30 ? "#22c55e" : dti < 60 ? "#f59e0b" : "#ef4444" }}>
                            DTI {dti.toFixed(1)}%
                          </span>
                        </>
                      )}
                    </td>
                    <td>
                      {c.riskScore != null ? (
                        <RiskScoreIndicator score={c.riskScore} />
                      ) : "—"}
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

      {data && data.totalPages > 1 && (
        <div style={s.pager}>
          <button style={s.btnSm} disabled={!data.hasPreviousPage} onClick={() => setPage(p => p - 1)}>← Anterior</button>
          <span style={s.muted}>Página {page} de {data.totalPages}</span>
          <button style={s.btnSm} disabled={!data.hasNextPage} onClick={() => setPage(p => p + 1)}>Siguiente →</button>
        </div>
      )}

      {showCreate && (
        <CreateCreditModal
          onClose={() => setShowCreate(false)}
          onSaved={(creditId) => {
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

function CreditDetail({ credit, client }: { credit: Credit; client?: Client }) {
  const cuota = computeMonthlyPayment(credit.amount, credit.interestRate, credit.termMonths);
  const dti = client && client.monthlyIncome > 0 ? (cuota / client.monthlyIncome) * 100 : null;
  const statusColor = STATUS_COLORS[credit.status] ?? "#6b7280";
  const riskColor = credit.riskScore != null
    ? (credit.riskScore < 30 ? "#22c55e" : credit.riskScore < 60 ? "#f59e0b" : "#ef4444")
    : "#6b7280";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      {/* Summary cards */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 10 }}>
        <SummaryCard label="Monto" value={fmt(credit.amount)} sub={`${fmt(cuota)}/mes`} color="#6366f1" />
        <SummaryCard label="Estado" value={CREDIT_STATUS[credit.status] ?? credit.status} color={statusColor} />
        {credit.riskScore != null
          ? <SummaryCard label="Risk Score" value={credit.riskScore.toFixed(1)} sub={credit.riskScore < 30 ? "Bajo riesgo" : credit.riskScore < 60 ? "Riesgo medio" : "Alto riesgo"} color={riskColor} />
          : <SummaryCard label="Risk Score" value="Pendiente" color="#94a3b8" />
        }
      </div>

      {/* Client info */}
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

      {/* DTI bar */}
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

      {/* Fields */}
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {[
          ["Condiciones", `${(credit.interestRate * 100).toFixed(2)}% E.A. · ${credit.termMonths} meses`],
          ["Decisión", credit.decision ?? "—"],
          ["Razón", credit.reason ?? "—"],
          ["Creado", new Date(credit.createdAt).toLocaleString("es-CO")],
          ["Actualizado", new Date(credit.updatedAt).toLocaleString("es-CO")],
          ["ID", credit.id],
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

function CreateCreditModal({ onClose, onSaved }: { onClose: () => void; onSaved: (creditId: string) => void }) {
  const [clients, setClients] = useState<Client[]>([]);
  const [clientId, setClientId] = useState("");
  const [amount, setAmount] = useState("");
  const [interestRate, setInterestRate] = useState("0.18");
  const [termMonths, setTermMonths] = useState("36");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [financialSummary, setFinancialSummary] = useState<ClientFinancialSummary | null>(null);

  useEffect(() => {
    api.getClients(1, 100)
      .then(r => {
        setClients(r.items);
        if (r.items.length > 0) setClientId(r.items[0].id);
      })
      .catch(() => setError("No se pudo cargar la lista de clientes"));
  }, []);

  useEffect(() => {
    if (!clientId) { setFinancialSummary(null); return; }
    api.getClientFinancialSummary(clientId)
      .then(setFinancialSummary)
      .catch(() => setFinancialSummary(null));
  }, [clientId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");
    try {
      const credit = await api.createCredit({
        clientId,
        amount: Number(amount),
        interestRate: Number(interestRate),
        termMonths: Number(termMonths),
      });
      onSaved(credit.id);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al crear crédito");
    } finally {
      setLoading(false);
    }
  }

  const selectedClient = clients.find(c => c.id === clientId);

  return (
    <Modal title="Nueva Solicitud de Crédito" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Field label="Cliente">
          {clients.length === 0 ? (
            <p style={{ color: "#ef4444", fontSize: 13 }}>No hay clientes registrados. Crea un cliente primero.</p>
          ) : (
            <select value={clientId} onChange={e => setClientId(e.target.value)}>
              {clients.map(c => (
                <option key={c.id} value={c.id}>
                  {c.fullName} — Score: {c.creditScore} — {fmtShort(c.monthlyIncome)}/mes
                </option>
              ))}
            </select>
          )}
        </Field>

        {selectedClient && (
          <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 8, padding: "10px 14px", fontSize: 13, color: "#166534" }}>
            Ingresos: <strong>{fmt(selectedClient.monthlyIncome)}</strong> · Score: <strong>{selectedClient.creditScore}</strong>
            {financialSummary && financialSummary.activeCreditsCount > 0 && (
              <> · Deuda actual: <strong>{fmt(financialSummary.existingMonthlyDebt)}/mes</strong> ({financialSummary.activeCreditsCount} crédito{financialSummary.activeCreditsCount !== 1 ? "s" : ""})</>
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
            amount={Number(amount)}
            rate={Number(interestRate)}
            term={Number(termMonths)}
            income={selectedClient.monthlyIncome}
            existingMonthlyDebt={financialSummary?.existingMonthlyDebt ?? 0}
          />
        )}

        {error && <p style={{ color: "#ef4444", fontSize: 13 }}>{error}</p>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <button type="button" style={s.btnSm} onClick={onClose}>Cancelar</button>
          <button type="submit" style={s.btnPrimary} disabled={loading || clients.length === 0}>
            {loading ? "Enviando..." : "Solicitar Crédito"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function DTIPreview({ amount, rate, term, income, existingMonthlyDebt }: {
  amount: number; rate: number; term: number; income: number; existingMonthlyDebt: number;
}) {
  const r = rate / 12;
  const n = term || 36;
  const newCuota = r === 0 ? amount / n : amount * r / (1 - Math.pow(1 + r, -n));
  const totalDebt = existingMonthlyDebt + newCuota;
  const totalDti = (totalDebt / income) * 100;
  const newDti = (newCuota / income) * 100;
  const color = totalDti < 30 ? "#22c55e" : totalDti < 60 ? "#f59e0b" : "#ef4444";
  const decision = totalDti > 60
    ? "Probable: Rechazado (supera 60% DTI)"
    : newDti < 30
    ? "Probable: Aprobado"
    : "Probable: En Revisión";
  return (
    <div style={{ background: color + "15", border: `1px solid ${color}40`, borderRadius: 8, padding: "10px 14px", fontSize: 13, display: "flex", flexDirection: "column", gap: 4 }}>
      <div>Cuota nueva: <strong>{fmt(newCuota)}/mes</strong></div>
      {existingMonthlyDebt > 0 && (
        <div style={{ color: "#64748b" }}>Deuda existente: {fmt(existingMonthlyDebt)}/mes · Total: {fmt(totalDebt)}/mes</div>
      )}
      <div>DTI total proyectado: <strong style={{ color }}>{totalDti.toFixed(1)}%</strong> · {decision}</div>
    </div>
  );
}

function UpdateStatusModal({ credit, onClose, onSaved }: { credit: Credit; onClose: () => void; onSaved: () => void }) {
  const STATUS_OPTIONS = [
    { value: 0, label: "Pendiente" },
    { value: 1, label: "En Revisión" },
    { value: 2, label: "Aprobado (Active)" },
    { value: 3, label: "Rechazado" },
    { value: 4, label: "Cerrado" },
    { value: 5, label: "En Mora" },
  ];
  const [newStatus, setNewStatus] = useState(0);
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");
    try {
      await api.updateCreditStatus(credit.id, { newStatus, reason });
      onSaved();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al actualizar estado");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Modal title="Cambiar Estado del Crédito" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <p style={{ fontSize: 13, color: "#64748b" }}>
          Estado actual: <StatusBadge status={credit.status} />
        </p>
        <Field label="Nuevo estado">
          <select value={newStatus} onChange={e => setNewStatus(Number(e.target.value))}>
            {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
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
  if (n >= 1_000) return `$${(n / 1_000).toFixed(0)}k`;
  return `$${n}`;
}

const s: Record<string, React.CSSProperties> = {
  header: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 },
  title: { fontSize: 22, fontWeight: 700, color: "#1e293b" },
  card: { background: "#fff", border: "1px solid #e2e8f0", borderRadius: 10, overflow: "hidden" },
  btnPrimary: { background: "#6366f1", color: "#fff", fontWeight: 600, padding: "8px 16px" },
  btnSm: { background: "#f1f5f9", color: "#374151", padding: "6px 12px", fontSize: 13, border: "none", borderRadius: 6 },
  muted: { fontSize: 12, color: "#94a3b8" },
  error: { color: "#ef4444", fontSize: 13, marginBottom: 12 },
  empty: { padding: 32, textAlign: "center", color: "#94a3b8" },
  link: { color: "#6366f1", cursor: "pointer", fontWeight: 500 },
  pager: { display: "flex", gap: 12, alignItems: "center", justifyContent: "flex-end", marginTop: 12 },
};
