import { env } from "./env";

/** Typed API client that calls the .NET Web API backend. */
export class ApiClient {
  private baseUrl: string;
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
      const body = await res.text().catch(() => "");
      throw new Error(`API ${res.status}: ${body}`);
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
    return this.request<VitalSign[]>(
      `/api/patients/${id}/vitals${query ? `?${query}` : ""}`
    );
  }

  async getPatientSleep(id: string, params?: { from?: string; to?: string }) {
    const qs = new URLSearchParams();
    if (params?.from) qs.set("from", params.from);
    if (params?.to) qs.set("to", params.to);
    const query = qs.toString();
    return this.request<SleepSession[]>(
      `/api/patients/${id}/sleep${query ? `?${query}` : ""}`
    );
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
  createdAt: string;
  updatedAt: string;
}

export interface VitalSign {
  type: string;
  value: number;
  unit: string;
  timestamp: string;
  trend: number;
}

export interface SleepSession {
  startTime: string;
  endTime: string;
  durationMinutes: number;
  qualityScore: number;
}

export interface VitalsParams {
  type?: string;
  from?: string;
  to?: string;
}

export const api = new ApiClient();
