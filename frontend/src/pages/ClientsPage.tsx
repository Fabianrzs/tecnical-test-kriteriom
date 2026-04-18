import { useState, useEffect, useCallback } from "react";
import { api, EMPLOYMENT_OPTIONS, CREDIT_STATUS, STATUS_COLORS } from "../api";
import type { Client, Credit, PagedResult, ClientFinancialSummary } from "../api";
import { Modal } from "../components/Modal";
import { useToast } from "../components/Toast";

export function ClientsPage() {
  const [data, setData] = useState<PagedResult<Client> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<Client | null>(null);
  const [detail, setDetail] = useState<Client | null>(null);
  const { toast } = useToast();

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await api.getClients(page, 10);
      setData(result);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al cargar clientes");
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => { load(); }, [load]);

  return (
    <div>
      <div style={s.header}>
        <h1 style={s.title}>Clientes</h1>
        <div style={{ display: "flex", gap: 8 }}>
          <button style={s.btnSm} onClick={load} disabled={loading}>{loading ? "Actualizando..." : "Actualizar"}</button>
          <button style={s.btnPrimary} onClick={() => setShowCreate(true)}>+ Nuevo Cliente</button>
        </div>
      </div>

      {error && <p style={s.error}>{error}</p>}

      <div style={s.card}>
        {loading ? (
          <p style={s.empty}>Cargando...</p>
        ) : data?.items.length === 0 ? (
          <p style={s.empty}>No hay clientes. Crea uno para comenzar.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Documento</th>
                <th>Ingresos / mes</th>
                <th>Credit Score</th>
                <th>Empleo</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {data?.items.map(c => (
                <tr key={c.id}>
                  <td>
                    <span style={s.link} onClick={() => setDetail(c)}>{c.fullName}</span>
                    <br /><span style={s.muted}>{c.email}</span>
                  </td>
                  <td>{c.documentNumber}</td>
                  <td>{fmt(c.monthlyIncome)}</td>
                  <td><ScoreBadge score={c.creditScore} /></td>
                  <td>{c.employmentStatus}</td>
                  <td>
                    <button style={s.btnSm} onClick={() => setEditing(c)}>Editar</button>
                  </td>
                </tr>
              ))}
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
        <ClientForm
          title="Nuevo Cliente"
          onClose={() => setShowCreate(false)}
          onSaved={() => { setShowCreate(false); load(); toast("Cliente creado", "success"); }}
        />
      )}

      {editing && (
        <ClientForm
          title="Editar Cliente"
          client={editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); load(); toast("Cliente actualizado", "success"); }}
        />
      )}

      {detail && (
        <ClientDetailModal client={detail} onClose={() => setDetail(null)} />
      )}
    </div>
  );
}

// ─── Client Detail Modal with Tabs ────────────────────────────────────────────

type TabKey = "perfil" | "creditos" | "pagos";

function ClientDetailModal({ client, onClose }: { client: Client; onClose: () => void }) {
  const [tab, setTab] = useState<TabKey>("perfil");
  const [credits, setCredits] = useState<Credit[]>([]);
  const [summary, setSummary] = useState<ClientFinancialSummary | null>(null);
  const [loadingCredits, setLoadingCredits] = useState(false);

  useEffect(() => {
    setLoadingCredits(true);
    Promise.all([
      api.getCreditsByClient(client.id, 1, 50),
      api.getClientFinancialSummary(client.id),
    ])
      .then(([cr, sm]) => { setCredits(cr.items); setSummary(sm); })
      .catch(() => {})
      .finally(() => setLoadingCredits(false));
  }, [client.id]);

  const activeCredits = credits.filter(c => c.status === "Active");

  return (
    <Modal title={client.fullName} onClose={onClose} maxWidth={640}>
      {/* Tab bar */}
      <div style={{ display: "flex", gap: 2, marginBottom: 20, borderBottom: "2px solid #e2e8f0" }}>
        {([
          ["perfil", "Perfil"],
          ["creditos", `Créditos (${credits.length})`],
          ["pagos", `Pago del Mes (${activeCredits.length})`],
        ] as [TabKey, string][]).map(([key, label]) => (
          <button
            key={key}
            onClick={() => setTab(key)}
            style={{
              padding: "8px 16px", fontSize: 13, fontWeight: 600, border: "none",
              borderBottom: tab === key ? "2px solid #6366f1" : "2px solid transparent",
              color: tab === key ? "#6366f1" : "#64748b",
              background: "none", cursor: "pointer", marginBottom: -2,
            }}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "perfil" && <PerfilTab client={client} summary={summary} />}
      {tab === "creditos" && <CreditosTab credits={credits} income={client.monthlyIncome} loading={loadingCredits} />}
      {tab === "pagos" && <PagosTab credits={activeCredits} loading={loadingCredits} />}
    </Modal>
  );
}

// ─── Tab: Perfil ──────────────────────────────────────────────────────────────

function PerfilTab({ client, summary }: { client: Client; summary: ClientFinancialSummary | null }) {
  const scoreColor = client.creditScore >= 700 ? "#22c55e" : client.creditScore >= 550 ? "#f59e0b" : "#ef4444";
  const scoreLabel = client.creditScore >= 700 ? "Bueno" : client.creditScore >= 550 ? "Regular" : "Bajo";
  const scorePct = ((client.creditScore - 300) / 550) * 100;

  const currentDti = summary && client.monthlyIncome > 0
    ? (summary.existingMonthlyDebt / client.monthlyIncome) * 100
    : 0;
  const dtiColor = currentDti < 30 ? "#22c55e" : currentDti < 60 ? "#f59e0b" : "#ef4444";
  const capacityUsed = Math.min(currentDti / 60 * 100, 100);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {/* Score visual */}
      <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: 10, padding: "14px 16px" }}>
        <div style={{ fontSize: 11, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: 1, marginBottom: 8 }}>
          Credit Score
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 8 }}>
          <span style={{ fontSize: 28, fontWeight: 700, color: scoreColor }}>{client.creditScore}</span>
          <span style={{ background: scoreColor + "20", color: scoreColor, fontSize: 12, fontWeight: 600, padding: "3px 10px", borderRadius: 12 }}>
            {scoreLabel}
          </span>
          <span style={{ fontSize: 12, color: "#94a3b8", marginLeft: "auto" }}>máx. 850</span>
        </div>
        <ProgressBar pct={scorePct} color={scoreColor} />
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#94a3b8", marginTop: 4 }}>
          <span>300 — Muy bajo</span><span>575 — Regular</span><span>850 — Excelente</span>
        </div>
      </div>

      {/* Capacity gauge */}
      <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: 10, padding: "14px 16px" }}>
        <div style={{ fontSize: 11, fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: 1, marginBottom: 8 }}>
          Capacidad de Endeudamiento (DTI)
        </div>
        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
          <span style={{ fontSize: 13 }}>
            Deuda actual: <strong style={{ color: dtiColor }}>{currentDti.toFixed(1)}%</strong> de ingresos
          </span>
          <span style={{ fontSize: 12, color: "#64748b" }}>
            {summary ? fmt(summary.existingMonthlyDebt) + "/mes" : "—"}
          </span>
        </div>
        <div style={{ position: "relative", height: 10, background: "#e2e8f0", borderRadius: 5, overflow: "hidden" }}>
          <div style={{ position: "absolute", left: 0, top: 0, height: "100%", width: `${capacityUsed}%`, background: dtiColor, borderRadius: 5, transition: "width 0.4s" }} />
          {/* 60% limit marker */}
          <div style={{ position: "absolute", left: "100%", top: -2, height: 14, width: 2, background: "#ef4444", opacity: 0.6 }} />
        </div>
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#94a3b8", marginTop: 4 }}>
          <span>0%</span>
          <span style={{ color: "#ef4444" }}>Límite 60%</span>
        </div>
        {summary && (
          <div style={{ marginTop: 8, fontSize: 12, color: "#64748b" }}>
            Capacidad disponible:{" "}
            <strong style={{ color: "#22c55e" }}>
              {fmt(Math.max(0, client.monthlyIncome * 0.6 - summary.existingMonthlyDebt))}/mes
            </strong>
            {" "}· {summary.activeCreditsCount} crédito{summary.activeCreditsCount !== 1 ? "s" : ""} activo{summary.activeCreditsCount !== 1 ? "s" : ""}
          </div>
        )}
      </div>

      {/* Fields */}
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {[
          ["Email", client.email],
          ["Documento", client.documentNumber],
          ["Ingresos mensuales", fmt(client.monthlyIncome)],
          ["Situación laboral", client.employmentStatus],
          ["Registrado", new Date(client.createdAt).toLocaleDateString("es-CO")],
          ["Actualizado", new Date(client.updatedAt).toLocaleDateString("es-CO")],
        ].map(([k, v]) => (
          <div key={k} style={{ display: "flex", justifyContent: "space-between", borderBottom: "1px solid #f1f5f9", paddingBottom: 6 }}>
            <span style={{ color: "#64748b", fontSize: 13 }}>{k}</span>
            <span style={{ fontWeight: 500, fontSize: 13 }}>{v}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Tab: Créditos ────────────────────────────────────────────────────────────

function CreditosTab({ credits, income, loading }: { credits: Credit[]; income: number; loading: boolean }) {
  if (loading) return <p style={{ textAlign: "center", color: "#94a3b8", padding: 32 }}>Cargando...</p>;
  if (credits.length === 0) return <p style={{ textAlign: "center", color: "#94a3b8", padding: 32 }}>Sin créditos registrados.</p>;

  const activeStatuses = ["Active"];
  const totalDebt = credits
    .filter(c => activeStatuses.includes(c.status))
    .reduce((sum, c) => sum + monthlyPayment(c), 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      {income > 0 && (
        <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 8, padding: "10px 14px", fontSize: 13, color: "#166534", display: "flex", justifyContent: "space-between" }}>
          <span>Carga mensual total activa</span>
          <strong>{fmt(totalDebt)}/mes · DTI {((totalDebt / income) * 100).toFixed(1)}%</strong>
        </div>
      )}

      {credits.map(c => {
        const cuota = monthlyPayment(c);
        const dti = income > 0 ? (cuota / income) * 100 : 0;
        const statusColor = STATUS_COLORS[c.status] ?? "#6b7280";
        const isActive = c.status === "Active";

        return (
          <div
            key={c.id}
            style={{
              border: `1px solid ${isActive ? "#bbf7d0" : "#e2e8f0"}`,
              borderLeft: `3px solid ${statusColor}`,
              borderRadius: 8, padding: "12px 14px",
              background: isActive ? "#f0fdf4" : "#fafafa",
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 8 }}>
              <div>
                <span style={{ fontWeight: 700, fontSize: 15, color: "#1e293b" }}>{fmt(c.amount)}</span>
                <span style={{ fontSize: 12, color: "#64748b", marginLeft: 8 }}>{c.termMonths} meses · {(c.interestRate * 100).toFixed(1)}% E.A.</span>
              </div>
              <span style={{ background: statusColor + "20", color: statusColor, fontSize: 11, fontWeight: 600, padding: "3px 8px", borderRadius: 10 }}>
                {CREDIT_STATUS[c.status] ?? c.status}
              </span>
            </div>

            {isActive && (
              <>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, color: "#475569", marginBottom: 6 }}>
                  <span>Cuota: <strong>{fmt(cuota)}/mes</strong></span>
                  {c.riskScore != null && <span>Risk Score: <strong>{c.riskScore.toFixed(1)}</strong></span>}
                  <span>DTI aporte: <strong style={{ color: dti < 30 ? "#22c55e" : dti < 60 ? "#f59e0b" : "#ef4444" }}>{dti.toFixed(1)}%</strong></span>
                </div>
                <ProgressBar pct={Math.min(dti / 60 * 100, 100)} color={dti < 30 ? "#22c55e" : dti < 60 ? "#f59e0b" : "#ef4444"} height={6} />
              </>
            )}

            {c.reason && (
              <p style={{ fontSize: 11, color: "#64748b", marginTop: 6, fontStyle: "italic" }}>{c.reason}</p>
            )}

            <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 6 }}>
              {new Date(c.createdAt).toLocaleDateString("es-CO")}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ─── Tab: Pago del Mes ────────────────────────────────────────────────────────

function PagosTab({ credits, loading }: { credits: Credit[]; loading: boolean }) {
  if (loading) return <p style={{ textAlign: "center", color: "#94a3b8", padding: 32 }}>Cargando...</p>;
  if (credits.length === 0) return (
    <div style={{ textAlign: "center", padding: 32, color: "#94a3b8" }}>
      <div style={{ fontSize: 15, marginBottom: 8 }}>Sin créditos activos</div>
      <div style={{ fontSize: 13 }}>El cliente no tiene créditos en estado Aprobado.</div>
    </div>
  );

  const now = new Date();
  const monthName = now.toLocaleString("es-CO", { month: "long", year: "numeric" });
  const totalMes = credits.reduce((sum, c) => sum + monthlyPayment(c), 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h3 style={{ fontSize: 14, fontWeight: 600, color: "#1e293b", textTransform: "capitalize" }}>
          Detalle de pagos — {monthName}
        </h3>
        <span style={{ fontSize: 13, fontWeight: 700, color: "#6366f1" }}>Total: {fmt(totalMes)}</span>
      </div>

      {credits.map(c => {
        const amort = computeCurrentAmortization(c);
        const paidPct = Math.min(100, Math.max(0, ((c.amount - amort.balance) / c.amount) * 100));

        return (
          <div key={c.id} style={{ border: "1px solid #e2e8f0", borderRadius: 10, padding: "14px 16px", background: "#fff" }}>
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
              <span style={{ fontWeight: 600, fontSize: 14, color: "#1e293b" }}>{fmt(c.amount)}</span>
              <span style={{ fontSize: 12, color: "#6366f1", fontWeight: 600 }}>
                Cuota {amort.paymentNum} de {c.termMonths}
              </span>
            </div>

            {/* Payment breakdown */}
            <div style={{ display: "flex", flexDirection: "column", gap: 8, marginBottom: 12 }}>
              <AmortRow label="Cuota total" value={fmt(amort.payment)} color="#1e293b" bold />
              <div style={{ height: 1, background: "#f1f5f9" }} />
              <AmortRow label="Interés del mes" value={fmt(amort.interest)} color="#ef4444"
                pct={amort.payment > 0 ? amort.interest / amort.payment * 100 : 0} barColor="#ef4444" />
              <AmortRow label="Abono a capital" value={fmt(amort.principal)} color="#22c55e"
                pct={amort.payment > 0 ? amort.principal / amort.payment * 100 : 0} barColor="#22c55e" />
              <div style={{ height: 1, background: "#f1f5f9" }} />
              <AmortRow label="Saldo restante" value={fmt(amort.balance)} color="#64748b" />
            </div>

            {/* Progress: paid off */}
            <div style={{ fontSize: 12, color: "#64748b", marginBottom: 4, display: "flex", justifyContent: "space-between" }}>
              <span>Capital amortizado</span>
              <span style={{ fontWeight: 600, color: "#22c55e" }}>{paidPct.toFixed(1)}%</span>
            </div>
            <div style={{ position: "relative", height: 8, background: "#e2e8f0", borderRadius: 4, overflow: "hidden" }}>
              <div style={{ position: "absolute", left: 0, top: 0, height: "100%", width: `${paidPct}%`, background: "linear-gradient(90deg, #22c55e, #16a34a)", borderRadius: 4 }} />
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#94a3b8", marginTop: 4 }}>
              <span>{fmt(c.amount - amort.balance)} amortizado</span>
              <span>{fmt(amort.balance)} pendiente</span>
            </div>

            <div style={{ marginTop: 8, fontSize: 11, color: "#94a3b8" }}>
              {(c.interestRate * 100).toFixed(2)}% E.A. · {c.termMonths - amort.paymentNum} cuotas restantes
            </div>
          </div>
        );
      })}
    </div>
  );
}

function AmortRow({ label, value, color, bold, pct, barColor }: {
  label: string; value: string; color: string; bold?: boolean; pct?: number; barColor?: string;
}) {
  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13 }}>
        <span style={{ color: "#64748b" }}>{label}</span>
        <span style={{ fontWeight: bold ? 700 : 600, color }}>{value}</span>
      </div>
      {pct !== undefined && barColor && (
        <div style={{ marginTop: 3, height: 4, background: "#f1f5f9", borderRadius: 2, overflow: "hidden" }}>
          <div style={{ height: "100%", width: `${pct}%`, background: barColor, borderRadius: 2 }} />
        </div>
      )}
    </div>
  );
}

// ─── Shared utilities & components ───────────────────────────────────────────

function ProgressBar({ pct, color, height = 8 }: { pct: number; color: string; height?: number }) {
  return (
    <div style={{ height, background: "#e2e8f0", borderRadius: height / 2, overflow: "hidden" }}>
      <div style={{ height: "100%", width: `${Math.min(100, Math.max(0, pct))}%`, background: color, borderRadius: height / 2, transition: "width 0.4s" }} />
    </div>
  );
}

function monthlyPayment(c: Credit): number {
  const r = c.interestRate / 12;
  const n = c.termMonths;
  if (r === 0) return c.amount / n;
  return c.amount * r / (1 - Math.pow(1 + r, -n));
}

function computeCurrentAmortization(c: Credit) {
  const created = new Date(c.createdAt);
  const now = new Date();
  const elapsed = (now.getFullYear() - created.getFullYear()) * 12 + (now.getMonth() - created.getMonth());
  const paymentNum = Math.max(1, Math.min(elapsed + 1, c.termMonths));

  const r = c.interestRate / 12;
  const n = c.termMonths;
  const payment = r === 0 ? c.amount / n : c.amount * r / (1 - Math.pow(1 + r, -n));

  let balance = c.amount;
  let interest = 0;
  let principal = 0;
  for (let i = 1; i <= paymentNum; i++) {
    interest = balance * r;
    principal = payment - interest;
    balance = Math.max(0, balance - principal);
  }

  return { paymentNum, payment, interest, principal, balance };
}

function ScoreBadge({ score }: { score: number }) {
  const color = score >= 700 ? "#22c55e" : score >= 550 ? "#f59e0b" : "#ef4444";
  const label = score >= 700 ? "Bueno" : score >= 550 ? "Regular" : "Bajo";
  return (
    <span style={{ background: color + "20", color, padding: "3px 8px", borderRadius: 12, fontSize: 12, fontWeight: 600 }}>
      {score} · {label}
    </span>
  );
}

// ─── Form ─────────────────────────────────────────────────────────────────────

interface FormProps { title: string; client?: Client; onClose: () => void; onSaved: () => void; }

function ClientForm({ title, client, onClose, onSaved }: FormProps) {
  const [form, setForm] = useState({
    fullName: client?.fullName ?? "",
    email: client?.email ?? "",
    documentNumber: client?.documentNumber ?? "",
    monthlyIncome: client?.monthlyIncome ?? 0,
    employmentStatus: EMPLOYMENT_OPTIONS.find(o => o.label === client?.employmentStatus)?.value ?? 0,
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  function set(field: string, value: string | number) {
    setForm(f => ({ ...f, [field]: value }));
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");
    try {
      if (client) {
        await api.updateClient(client.id, {
          fullName: form.fullName,
          monthlyIncome: Number(form.monthlyIncome),
          employmentStatus: Number(form.employmentStatus),
        });
      } else {
        await api.createClient({
          fullName: form.fullName,
          email: form.email,
          documentNumber: form.documentNumber,
          monthlyIncome: Number(form.monthlyIncome),
          employmentStatus: Number(form.employmentStatus),
        });
      }
      onSaved();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Error al guardar");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Modal title={title} onClose={onClose}>
      <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <Field label="Nombre completo">
          <input value={form.fullName} onChange={e => set("fullName", e.target.value)} required />
        </Field>
        {!client && (
          <>
            <Field label="Email">
              <input type="email" value={form.email} onChange={e => set("email", e.target.value)} required />
            </Field>
            <Field label="Número de documento">
              <input value={form.documentNumber} onChange={e => set("documentNumber", e.target.value)} required />
            </Field>
          </>
        )}
        <Field label="Ingresos mensuales (COP)">
          <input type="number" min={0} value={form.monthlyIncome} onChange={e => set("monthlyIncome", e.target.value)} required />
        </Field>
        <Field label="Situación laboral">
          <select value={form.employmentStatus} onChange={e => set("employmentStatus", e.target.value)}>
            {EMPLOYMENT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </Field>
        <p style={{ fontSize: 12, color: "#6366f1", background: "#eef2ff", padding: "8px 12px", borderRadius: 6 }}>
          El Credit Score se calcula automáticamente según ingresos y situación laboral.
        </p>
        {error && <p style={{ color: "#ef4444", fontSize: 13 }}>{error}</p>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <button type="button" style={s.btnSm} onClick={onClose}>Cancelar</button>
          <button type="submit" style={s.btnPrimary} disabled={loading}>
            {loading ? "Guardando..." : "Guardar"}
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
