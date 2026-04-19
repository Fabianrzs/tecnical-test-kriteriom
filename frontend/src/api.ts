export const BASE = "http://localhost:8080";

function authHeader(): Record<string, string> {
  const token = localStorage.getItem("token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  return requestWithHeaders(method, path, body, {});
}

async function requestWithHeaders<T>(
  method: string,
  path: string,
  body: unknown | undefined,
  extra: Record<string, string>
): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: { "Content-Type": "application/json", ...authHeader(), ...extra },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    const text = await res.text();
    let message = `HTTP ${res.status}`;
    try {
      const json = JSON.parse(text);
      if (Array.isArray(json.errors)) {
        message = json.errors.map((e: { errorMessage: string }) => e.errorMessage).join(". ");
      } else if (json.detail) {
        message = json.detail;
      } else if (json.title) {
        message = json.title;
      }
    } catch {
      message = text || message;
    }
    throw new Error(message);
  }
  const text = await res.text();
  return (text ? JSON.parse(text) : null) as T;
}

// ─── Filter types ─────────────────────────────────────────────────────────────

export interface CreditsFilter {
  status?: string;
  clientId?: string;
  clientName?: string;
  amountMin?: number;
  amountMax?: number;
  dateFrom?: string;
  dateTo?: string;
  riskLevel?: string;
}

export interface ClientsFilter {
  search?: string;
  employmentStatus?: string;
  scoreTier?: string;
  incomeMin?: number;
  incomeMax?: number;
}

export interface AuditFilter {
  eventType?: string;
  dateFrom?: string;
  dateTo?: string;
  entityId?: string;
}

function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== "") p.set(k, String(v));
  }
  const s = p.toString();
  return s ? `?${s}` : "";
}

// ─── API client ───────────────────────────────────────────────────────────────

export const api = {
  login: async (username: string, password: string) => {
    const res = await request<{ accessToken: string; refreshToken: string; tokenType: string; expiresIn: number }>(
      "POST", "/auth/token", { username, password }
    );
    return { token: res.accessToken };
  },

  getClients: (page = 1, pageSize = 20, filter?: ClientsFilter) =>
    request<PagedResult<Client>>("GET", `/api/clients${buildQuery({
      page, pageSize,
      search:           filter?.search,
      employmentStatus: filter?.employmentStatus,
      scoreTier:        filter?.scoreTier,
      incomeMin:        filter?.incomeMin,
      incomeMax:        filter?.incomeMax,
    })}`),

  getClient: (id: string) =>
    request<Client>("GET", `/api/clients/${id}`),
  getClientFinancialSummary: (id: string) =>
    request<ClientFinancialSummary>("GET", `/api/clients/${id}/financial-summary`),
  getCreditsByClient: (id: string, page = 1, pageSize = 50) =>
    request<PagedResult<Credit>>("GET", `/api/clients/${id}/credits?page=${page}&pageSize=${pageSize}`),
  createClient: (data: CreateClientRequest) =>
    request<Client>("POST", "/api/clients", data),
  updateClient: (id: string, data: UpdateClientRequest) =>
    request<Client>("PUT", `/api/clients/${id}`, data),

  searchClients: (query: string, pageSize = 50) =>
    request<PagedResult<Client>>("GET", `/api/clients${buildQuery({
      page: 1, pageSize, search: query,
    })}`),

  getCredits: (page = 1, pageSize = 20, filter?: CreditsFilter) =>
    request<PagedResult<Credit>>("GET", `/api/credits${buildQuery({
      page, pageSize,
      status:     filter?.status,
      clientId:   filter?.clientId,
      clientName: filter?.clientName,
      amountMin:  filter?.amountMin,
      amountMax:  filter?.amountMax,
      dateFrom:   filter?.dateFrom,
      dateTo:     filter?.dateTo,
      riskLevel:  filter?.riskLevel,
    })}`),

  getCredit: (id: string) =>
    request<Credit>("GET", `/api/credits/${id}`),
  createCredit: (data: CreateCreditRequest) =>
    requestWithHeaders<Credit>("POST", "/api/credits", data, { "Idempotency-Key": crypto.randomUUID() }),
  getCreditStats: () =>
    request<CreditStats>("GET", "/api/credits/stats"),
  updateCreditStatus: (id: string, data: UpdateStatusRequest) =>
    request<void>("PUT", `/api/credits/${id}/status`, data),

  getAuditRecent: (page = 1, pageSize = 50, filter?: AuditFilter) =>
    request<AuditPagedResult>("GET", `/api/audit${buildQuery({
      page, pageSize,
      eventType: filter?.eventType,
      dateFrom:  filter?.dateFrom,
      dateTo:    filter?.dateTo,
      entityId:  filter?.entityId,
    })}`),

  getAuditByCredit: (creditId: string) =>
    request<AuditRecord[]>("GET", `/api/audit/credit/${creditId}`),

  triggerBatch: () =>
    request<BatchJobResponse>("POST", "/api/batch/recalculate"),
  getBatchStatus: () =>
    request<BatchCheckpoint[]>("GET", "/api/batch/status"),
};

// ─── Types ────────────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface Client {
  id: string;
  fullName: string;
  email: string;
  documentNumber: string;
  monthlyIncome: number;
  creditScore: number;
  employmentStatus: string;
  createdAt: string;
  updatedAt: string;
}

export interface Credit {
  id: string;
  clientId: string;
  amount: number;
  interestRate: number;
  termMonths: number;
  status: string;
  riskScore?: number;
  decision?: string;
  reason?: string;
  createdAt: string;
  updatedAt: string;
}

export interface ClientFinancialSummary {
  existingMonthlyDebt: number;
  activeCreditsCount: number;
}

export interface CreateClientRequest {
  fullName: string;
  email: string;
  documentNumber: string;
  monthlyIncome: number;
  employmentStatus: number;
}

export interface UpdateClientRequest {
  fullName: string;
  monthlyIncome: number;
  employmentStatus: number;
}

export interface CreateCreditRequest {
  clientId: string;
  amount: number;
  interestRate: number;
  termMonths: number;
}

export interface UpdateStatusRequest {
  newStatus: number;
  reason: string;
}

export interface AuditRecord {
  id: string;
  eventType: string;
  eventId: string;
  correlationId: string;
  entityId: string | null;
  payload: string;
  occurredOn: string;
  recordedAt: string;
  serviceName: string;
}

export interface AuditPagedResult {
  items: AuditRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CreditStats {
  totalCredits: number;
  totalClients: number;
  approvalRate: number;
  byStatus: {
    pending: number;
    underReview: number;
    approved: number;
    rejected: number;
    closed: number;
    defaulted: number;
  };
}

export interface BatchJobResponse {
  jobId: string;
  jobName: string;
  status: string;
}

export interface BatchCheckpoint {
  id: string;
  jobName: string;
  status: string;
  processedRecords: number;
  totalRecords: number;
  lastProcessedOffset: number;
  startedAt: string;
  updatedAt: string;
  errorMessage: string | null;
}

export function decodeRole(token: string): string {
  try {
    const payload = JSON.parse(atob(token.split(".")[1]));
    return payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? "";
  } catch {
    return "";
  }
}

export const EMPLOYMENT_OPTIONS = [
  { value: 0, label: "Empleado" },
  { value: 1, label: "Independiente" },
  { value: 2, label: "Desempleado" },
  { value: 3, label: "Pensionado" },
];

export const CREDIT_STATUS: Record<string, string> = {
  Pending:     "Pendiente",
  UnderReview: "En Revisión",
  Active:      "Aprobado",
  Rejected:    "Rechazado",
  Closed:      "Cerrado",
  Defaulted:   "En Mora",
};

export const STATUS_COLORS: Record<string, string> = {
  Pending:     "#f59e0b",
  UnderReview: "#6366f1",
  Active:      "#22c55e",
  Rejected:    "#ef4444",
  Closed:      "#6b7280",
  Defaulted:   "#dc2626",
};
