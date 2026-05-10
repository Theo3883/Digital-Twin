import { env } from "./env";

/** Typed API client that calls the .NET Web API backend. */
export class ApiClient {
  private readonly baseUrl: string;
  private token: string | null = null;

  constructor() {
    this.baseUrl = env.API_URL;
  }

  setToken(token: string) {
    this.token = token;
  }

  private async request<T>(path: string, options?: RequestInit): Promise<T> {
    const headers: HeadersInit = {
      "Content-Type": "application/json",
      ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
      ...options?.headers,
    };

    const res = await fetch(`${this.baseUrl}${path}`, {
      ...options,
      headers,
    });

    if (!res.ok) {
      const bodyText = await res.text().catch(() => "");

      // Prefer a safe, user-facing error message; don't leak unknown server internals.
      let message = "";
      try {
        const parsed = bodyText ? (JSON.parse(bodyText) as { error?: string }) : null;
        message = parsed?.error ? String(parsed.error) : "";
      } catch {
        // ignore JSON parse failures
      }

      if (!message) {
        if (res.status >= 500) message = "Server error. Please try again.";
        else if (res.status === 401) message = "Unauthorized.";
        else if (res.status === 403) message = "Forbidden.";
        else if (res.status === 404) message = "Not found.";
        else message = "Request failed.";
      }

      throw new Error(`API ${res.status}: ${message}`);
    }

    if (res.status === 204) return undefined as T;
    return res.json();
  }

  // ── Auth ──────────────────────────────────────────────────────────────────

  async loginWithGoogle(idToken: string) {
    return this.request<LoginResponse>("/api/auth/google", {
      method: "POST",
      body: JSON.stringify({ idToken }),
    });
  }

  async registerDoctor(idToken: string, doctorSecret: string) {
    return this.request<LoginResponse>("/api/auth/register", {
      method: "POST",
      body: JSON.stringify({ idToken, doctorSecret }),
    });
  }

  // ── Dashboard ─────────────────────────────────────────────────────────────

  async getDashboard() {
    return this.request<{
      totalAssignedPatients: number;
      doctorName: string;
      doctorEmail: string;
    }>("/api/dashboard");
  }

  // ── Patients ──────────────────────────────────────────────────────────────

  async getMyPatients() {
    return this.request<PatientSummary[]>("/api/patients");
  }

  async getPatientDetail(id: string) {
    return this.request<PatientDetail>(`/api/patients/${id}`);
  }

  async getPatientVitals(id: string, params?: VitalsParams) {
    const qs = new URLSearchParams();
    if (params?.type) qs.set("type", params.type);
    if (params?.from) qs.set("from", params.from);
    if (params?.to) qs.set("to", params.to);
    const query = qs.toString();
    const vitalsPath = `/api/patients/${id}/vitals`;
    const vitalsUrl = query ? `${vitalsPath}?${query}` : vitalsPath;
    return this.request<VitalSign[]>(vitalsUrl);
  }

  async getPatientSleep(id: string, params?: { from?: string; to?: string }) {
    const qs = new URLSearchParams();
    if (params?.from) qs.set("from", params.from);
    if (params?.to) qs.set("to", params.to);
    const query = qs.toString();
    const sleepPath = `/api/patients/${id}/sleep`;
    const sleepUrl = query ? `${sleepPath}?${query}` : sleepPath;
    return this.request<SleepSession[]>(sleepUrl);
  }

  async assignPatient(email: string, notes?: string) {
    return this.request<PatientSummary>("/api/patients/assign", {
      method: "POST",
      body: JSON.stringify({ patientEmail: email, notes }),
    });
  }

  async unassignPatient(id: string) {
    return this.request<void>(`/api/patients/${id}/unassign`, {
      method: "DELETE",
    });
  }

  // ── Medications ───────────────────────────────────────────────────────────

  async getPatientMedications(id: string) {
    return this.request<Medication[]>(`/api/patients/${id}/medications`);
  }

  async getPatientMedicalHistory(id: string, limit = 50) {
    return this.request<MedicalHistoryEntry[]>(
      `/api/patients/${id}/medical-history?limit=${encodeURIComponent(String(limit))}`
    );
  }

  async getPatientMedicationInteractions(id: string) {
    return this.request<MedicationInteraction[]>(
      `/api/patients/${id}/medications/interactions`
    );
  }

  async addPatientMedication(id: string, dto: AddMedicationRequest) {
    return this.request<Medication>(`/api/patients/${id}/medications`, {
      method: "POST",
      body: JSON.stringify(dto),
    });
  }

  async deletePatientMedication(id: string, medId: string) {
    return this.request<void>(`/api/patients/${id}/medications/${medId}`, {
      method: "DELETE",
    });
  }

  async discontinuePatientMedication(id: string, medId: string, reason: string) {
    return this.request<void>(`/api/patients/${id}/medications/${medId}/discontinue`, {
      method: "PATCH",
      body: JSON.stringify({ reason }),
    });
  }

  async searchDrugs(query: string, max = 8) {
    const q = encodeURIComponent(query);
    return this.request<DrugSearchResult[]>(`/api/drugs/search?q=${q}&max=${max}`);
  }

  async checkDrugInteractions(rxCuis: string[]) {
    return this.request<MedicationInteraction[]>(`/api/drugs/interactions`, {
      method: "POST",
      body: JSON.stringify({ rxCuis }),
    });
  }

  // ── Notifications ────────────────────────────────────────────────────────

  async getNotifications(limit = 50, unreadOnly = false) {
    const qs = new URLSearchParams();
    qs.set("limit", String(limit));
    qs.set("unreadOnly", unreadOnly ? "true" : "false");
    return this.request<NotificationItem[]>(`/api/notifications?${qs.toString()}`);
  }

  async getUnreadNotificationsCount() {
    return this.request<{ count: number }>("/api/notifications/unread-count");
  }

  async markNotificationRead(id: string) {
    return this.request<void>(`/api/notifications/${id}/read`, { method: "POST" });
  }

  async markAllNotificationsRead() {
    return this.request<void>("/api/notifications/read-all", { method: "POST" });
  }

  async deleteNotification(id: string) {
    return this.request<void>(`/api/notifications/${id}`, { method: "DELETE" });
  }
}

// ── Types ─────────────────────────────────────────────────────────────────────

export interface LoginResponse {
  token: string;
  expiresAt: string;
  email: string;
  name: string | null;
  registrationRequired: boolean;
}

export interface PatientSummary {
  patientId: string;
  email: string;
  fullName: string;
  bloodType: string | null;
  assignedAt: string;
  patientCreatedAt: string;
}

export interface PatientDetail {
  patientId: string;
  userId: string;
  email: string;
  fullName: string;
  photoUrl: string | null;
  bloodType: string | null;
  allergies: string | null;
  medicalHistoryNotes: string | null;
  cnp: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface MedicalHistoryEntry {
  id: string;
  patientId: string;
  sourceDocumentId: string;
  title: string;
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string;
  notes: string;
  summary: string;
  confidence: number;
  eventDate: string;
  createdAt: string;
  updatedAt: string;
}

export interface VitalSign {
  type: string;
  value: number;
  unit: string;
  timestamp: string;
  source: string;
  trend: number;
}

export interface SleepSession {
  startTime: string;
  endTime: string;
  durationMinutes: number;
  qualityScore: number;
}

export interface NotificationItem {
  id: string;
  title: string;
  body: string;
  type: number;
  severity: number;
  patientId: string | null;
  actorUserId: string | null;
  actorName: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface VitalsParams {
  type?: string;
  from?: string;
  to?: string;
}

// 0=Active, 1=Discontinued, 2=Scheduled, 3=Completed
export type MedicationStatus = 0 | 1 | 2 | 3;
// 0=Oral, 1=IV, 2=Topical, 3=Subcutaneous, 4=Other
export type MedicationRoute = 0 | 1 | 2 | 3 | 4;
// 0=Patient, 1=Doctor, 2=OcrScan
export type AddedByRole = 0 | 1 | 2;

export const MedicationStatusLabel: Record<MedicationStatus, string> = {
  0: "Active",
  1: "Discontinued",
  2: "Scheduled",
  3: "Completed",
};

export const MedicationRouteLabel: Record<MedicationRoute, string> = {
  0: "Oral",
  1: "IV",
  2: "Topical",
  3: "Subcutaneous",
  4: "Other",
};

export interface Medication {
  id: string;
  name: string;
  dosage: string;
  frequency: string | null;
  route: MedicationRoute;
  status: MedicationStatus;
  rxCui: string | null;
  instructions: string | null;
  reason: string | null;
  prescribedByUserId: string | null;
  startDate: string | null;
  endDate: string | null;
  discontinuedReason: string | null;
  addedByRole: AddedByRole;
  createdAt: string;
}

export interface AddMedicationRequest {
  name: string;
  dosage: string;
  frequency?: string;
  route: MedicationRoute;
  rxCui?: string;
  instructions?: string;
  reason?: string;
  startDate?: string;
}

export interface DrugSearchResult {
  name: string;
  rxCui: string;
}

// 0=None, 1=Low, 2=Medium, 3=High
export type InteractionSeverity = 0 | 1 | 2 | 3;

export interface MedicationInteraction {
  drugARxCui: string;
  drugBRxCui: string;
  severity: InteractionSeverity;
  description: string;
}

export const api = new ApiClient();
