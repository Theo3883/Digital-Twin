"use client";

import React, { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useApi } from "@/hooks/use-api";
import type {
  PatientDetail,
  VitalSign,
  SleepSession,
  Medication,
  AddMedicationRequest,
  MedicationInteraction,
  DrugSearchResult,
  MedicalHistoryEntry,
} from "@/lib/api";
import { MedicationStatusLabel, MedicationRouteLabel } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import { ArrowLeft, Heart, Moon, TrendingUp, Pill, Trash2, StopCircle, RotateCw } from "lucide-react";
import { format } from "date-fns";

/* ── Helpers ─────────────────────────────────────── */

const vitalTypes = ["HeartRate", "SpO2", "Steps", "Calories", "ActiveEnergy", "ExerciseMinutes", "StandHours"];

const vitalMeta: Record<string, { color: string; label: string }> = {
  HeartRate:       { color: "#FF3B30", label: "Heart Rate" },
  SpO2:            { color: "#007AFF", label: "SpO2" },
  Steps:           { color: "#34C759", label: "Steps" },
  Calories:        { color: "#FF9500", label: "Calories" },
  ActiveEnergy:    { color: "#5856D6", label: "Active Energy" },
  ExerciseMinutes: { color: "#FF2D55", label: "Exercise" },
  StandHours:      { color: "#00D4C8", label: "Stand Hours" },
};

function glassInput(extra = "") {
  return `flex h-10 w-full rounded-xl border border-white/20 bg-white/5 px-3 py-2 text-sm text-white placeholder:text-white/40 focus:outline-none focus:ring-2 focus:ring-white/30 ${extra}`;
}

function glassSelect(extra = "") {
  return `rounded-xl border border-white/20 bg-white/5 px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-white/30 ${extra}`;
}

function severityLabel(sev: number) {
  if (sev === 3) return "High Risk";
  if (sev === 2) return "Medium Risk";
  return "Low Risk";
}

function severityBadgeClass(sev: number) {
  if (sev === 3) return "bg-red-500/20 text-red-300 border-red-500/40";
  if (sev === 2) return "bg-amber-500/20 text-amber-300 border-amber-500/40";
  return "bg-yellow-500/20 text-yellow-300 border-yellow-500/40";
}

function qualityClass(score: number) {
  if (score >= 80) return "bg-green-500/20 text-green-300";
  if (score >= 50) return "bg-amber-500/20 text-amber-300";
  return "bg-red-500/20 text-red-300";
}

function medStatusClass(status: number) {
  if (status === 0) return "bg-green-500/20 text-green-300";
  if (status === 1) return "bg-red-500/20 text-red-300";
  return "bg-white/10 text-white/60";
}

function InfoCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="glass-panel p-5">
      <p className="text-xs text-white/50 uppercase tracking-wider font-medium mb-1">{label}</p>
      <p className="text-xl font-bold text-white">{value}</p>
    </div>
  );
}

/* ── Page ─────────────────────────────────────────── */

export default function PatientDetailPage() {
  const params = useParams();
  const router = useRouter();
  const { api, ready } = useApi();
  const patientId = params.id as string;

  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [vitals, setVitals] = useState<VitalSign[]>([]);
  const [sleep, setSleep] = useState<SleepSession[]>([]);
  const [medications, setMedications] = useState<Medication[]>([]);
  const [historyEntries, setHistoryEntries] = useState<MedicalHistoryEntry[]>([]);
  const [autoInteractions, setAutoInteractions] = useState<MedicationInteraction[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshingVitals, setRefreshingVitals] = useState(false);
  const [vitalType, setVitalType] = useState("HeartRate");
  const [activeTab, setActiveTab] = useState<"vitals" | "sleep" | "medications">("vitals");

  // Prescribe form
  const DOSAGE_UNITS = ["mg", "g", "mcg", "ml", "IU", "units", "%"] as const;
  type DosageUnit = typeof DOSAGE_UNITS[number];
  const [showPrescribeForm, setShowPrescribeForm] = useState(false);
  const [prescribeName, setPrescribeName] = useState("");
  const [prescribeDosageAmount, setPrescribeDosageAmount] = useState("");
  const [prescribeDosageUnit, setPrescribeDosageUnit] = useState<DosageUnit>("mg");
  const [prescribeFrequency, setPrescribeFrequency] = useState("");
  const [prescribeRoute, setPrescribeRoute] = useState<0 | 1 | 2 | 3 | 4>(0);
  const [prescribeReason, setPrescribeReason] = useState("");
  const [prescribing, setPrescribing] = useState(false);
  const [prescribeError, setPrescribeError] = useState<string | null>(null);

  // Interaction checker
  const [showInteractionDialog, setShowInteractionDialog] = useState(false);
  const [checkMedication1, setCheckMedication1] = useState("");
  const [checkMedication2, setCheckMedication2] = useState("");
  const [includeActiveMeds, setIncludeActiveMeds] = useState(false);
  const [checkingInteractions, setCheckingInteractions] = useState(false);
  const [hasCheckedInteractions, setHasCheckedInteractions] = useState(false);
  const [interactionError, setInteractionError] = useState<string | null>(null);
  const [checkedInteractions, setCheckedInteractions] = useState<MedicationInteraction[]>([]);
  const [rxCuiNameMap, setRxCuiNameMap] = useState<Record<string, string>>({});
  const [typedMedication1ForCheck, setTypedMedication1ForCheck] = useState("");
  const [typedMedication2ForCheck, setTypedMedication2ForCheck] = useState("");

  // End medication
  const [endMedDialog, setEndMedDialog] = useState<{ medId: string; medName: string; isDiscontinued: boolean } | null>(null);
  const [endMedReason, setEndMedReason] = useState("");
  const [endingMed, setEndingMed] = useState(false);

  useEffect(() => {
    if (!patientId || !ready) return;
    setLoading(true);
    Promise.all([
      api.getPatientDetail(patientId),
      api.getPatientVitals(patientId, { type: "HeartRate" }),
      api.getPatientSleep(patientId),
      api.getPatientMedications(patientId),
      api.getPatientMedicalHistory(patientId, 80),
      api.getPatientMedicationInteractions(patientId),
    ])
      .then(([p, v, s, m, h, interactions]) => {
        console.log(`[VitalsDebug] Initial load: ${v.length} vitals. Latest: ${v.at(-1)?.timestamp} Source: ${v.at(-1)?.source}. All sources:`, v.map(x => ({ timestamp: x.timestamp, source: x.source, type: x.type })));
        setPatient(p); setVitals(v); setSleep(s);
        setMedications(m); setHistoryEntries(h); setAutoInteractions(interactions);
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [api, patientId, ready]);

  const loadVitals = (type: string) => {
    setVitalType(type);
    setRefreshingVitals(true);
    api.getPatientVitals(patientId, { type }).then(v => {
      console.log(`[VitalsDebug] Loaded ${type}: ${v.length} vitals. Latest: ${v.at(-1)?.timestamp} Source: ${v.at(-1)?.source}`);
      setVitals(v);
    }).catch(console.error).finally(() => setRefreshingVitals(false));
  };

  const handleRefreshVitals = () => {
    loadVitals(vitalType);
  };

  const handleUnassign = async () => {
    if (!confirm("Remove this patient from your care?")) return;
    await api.unassignPatient(patientId);
    router.push("/patients");
  };

  const parseApiError = (err: unknown) => {
    const raw = err instanceof Error ? err.message : String(err ?? "Unknown error");
    const body = raw.replace(/^API\s+\d+:\s*/i, "").trim();
    try { const p = JSON.parse(body) as { error?: string }; if (p?.error) return p.error; } catch { /* ignore */ }
    return body || "Request failed.";
  };

  const handlePrescribe = async () => {
    if (!prescribeName.trim() || !prescribeDosageAmount.trim()) return;
    setPrescribing(true); setPrescribeError(null);
    try {
      const dto: AddMedicationRequest = {
        name: prescribeName.trim(),
        dosage: `${prescribeDosageAmount.trim()} ${prescribeDosageUnit}`,
        frequency: prescribeFrequency.trim() || undefined,
        route: prescribeRoute,
        reason: prescribeReason.trim() || undefined,
        startDate: new Date().toISOString(),
      };
      const added = await api.addPatientMedication(patientId, dto);
      setMedications((prev) => [added, ...prev]);
      try { setAutoInteractions(await api.getPatientMedicationInteractions(patientId)); } catch { /* ignore */ }
      setShowPrescribeForm(false);
      setPrescribeName(""); setPrescribeDosageAmount(""); setPrescribeDosageUnit("mg");
      setPrescribeFrequency(""); setPrescribeRoute(0); setPrescribeReason("");
    } catch (e) { setPrescribeError(parseApiError(e)); }
    finally { setPrescribing(false); }
  };

  const normalizeForMatch = (v: string) => v.trim().toLowerCase().replace(/[^a-z0-9\s]/g, " ").replace(/\s+/g, " ");
  const getMatchScore = (input: string, candidate: string) => {
    if (!input || !candidate) return 0;
    if (candidate === input) return 100;
    if (candidate.startsWith(`${input} `)) return 90;
    if (candidate.includes(` ${input} `) || candidate.endsWith(` ${input}`)) return 80;
    if (candidate.includes(input)) return 60;
    return input.split(" ").filter(Boolean).filter((t) => candidate.includes(t)).length * 10;
  };
  const resolveBestDrugMatch = async (typedName: string): Promise<DrugSearchResult | null> => {
    const input = typedName.trim();
    if (!input) return null;
    const normed = normalizeForMatch(input);
    const matches = await api.searchDrugs(input, 20);
    if (!matches.length) return null;
    return [...matches].sort((a, b) => {
      const diff = getMatchScore(normed, normalizeForMatch(b.name)) - getMatchScore(normed, normalizeForMatch(a.name));
      return diff !== 0 ? diff : a.name.length - b.name.length;
    })[0] ?? null;
  };

  const tryExtractNamesFromDescription = (desc?: string) => {
    if (!desc) return { a: null as string|null, b: null as string|null };
    const m = desc.match(/between\s+([a-z0-9\-\s]+)\s+and\s+([a-z0-9\-\s]+)\.?/i);
    return { a: m?.[1]?.trim() ?? null, b: m?.[2]?.trim() ?? null };
  };
  const resolveDisplayName = (rxCui: string, descName: string|null, typed: string) =>
    rxCuiNameMap[rxCui] ?? descName ?? (typed.trim() || `Medication (${rxCui})`);
  const getInteractionPairDisplay = (interaction: MedicationInteraction) => {
    const parsed = tryExtractNamesFromDescription(interaction.description);
    return `${resolveDisplayName(interaction.drugARxCui, parsed.a, typedMedication1ForCheck)} + ${resolveDisplayName(interaction.drugBRxCui, parsed.b, typedMedication2ForCheck)}`;
  };

  const openInteractionDialog = () => {
    setCheckMedication1(""); setCheckMedication2(""); setIncludeActiveMeds(false);
    setInteractionError(null); setCheckedInteractions([]); setHasCheckedInteractions(false);
    setRxCuiNameMap({}); setTypedMedication1ForCheck(""); setTypedMedication2ForCheck("");
    setShowInteractionDialog(true);
  };

  const handleCheckInteractions = async () => {
    if (!checkMedication1.trim()) return;
    if (!includeActiveMeds && !checkMedication2.trim()) return;
    setCheckingInteractions(true); setInteractionError(null); setCheckedInteractions([]); setHasCheckedInteractions(false);
    setTypedMedication1ForCheck(checkMedication1.trim()); setTypedMedication2ForCheck(checkMedication2.trim());
    try {
      const rxCuis: string[] = [];
      const nameMap: Record<string, string> = {};
      const med1 = await resolveBestDrugMatch(checkMedication1);
      if (!med1) { setInteractionError(`Could not find medication: "${checkMedication1}".`); return; }
      rxCuis.push(med1.rxCui); nameMap[med1.rxCui] = med1.name;
      if (checkMedication2.trim()) {
        const med2 = await resolveBestDrugMatch(checkMedication2);
        if (!med2) { setInteractionError(`Could not find medication: "${checkMedication2}".`); return; }
        rxCuis.push(med2.rxCui); nameMap[med2.rxCui] = med2.name;
      }
      if (includeActiveMeds) {
        medications.filter((m) => m.status === 0 && !!m.rxCui).forEach((m) => {
          if (!m.rxCui) return; rxCuis.push(m.rxCui); nameMap[m.rxCui] = m.name;
        });
      }
      const unique = Array.from(new Set(rxCuis));
      if (unique.length < 2) { setInteractionError("Need at least two medications to check interactions."); return; }
      const interactions = await api.checkDrugInteractions(unique);
      setRxCuiNameMap(nameMap); setCheckedInteractions(interactions); setHasCheckedInteractions(true);
    } catch (e) { setInteractionError(parseApiError(e)); }
    finally { setCheckingInteractions(false); }
  };

  const handleEndMedication = (med: Medication) => {
    setEndMedDialog({ medId: med.id, medName: med.name, isDiscontinued: med.status === 1 });
    setEndMedReason("");
  };
  const handleConfirmEndMedication = async () => {
    if (!endMedDialog || !endMedReason.trim()) return;
    setEndingMed(true);
    try {
      if (endMedDialog.isDiscontinued) {
        await api.deletePatientMedication(patientId, endMedDialog.medId);
        setMedications((prev) => prev.filter((m) => m.id !== endMedDialog.medId));
      } else {
        await api.discontinuePatientMedication(patientId, endMedDialog.medId, endMedReason.trim());
        setMedications((prev) => prev.map((m) =>
          m.id === endMedDialog.medId ? { ...m, status: 1 as const, endDate: new Date().toISOString(), discontinuedReason: endMedReason.trim() } : m
        ));
      }
      try { setAutoInteractions(await api.getPatientMedicationInteractions(patientId)); } catch { /* ignore */ }
      setEndMedDialog(null);
    } catch (e) { console.error("[EndMed] error:", e); }
    finally { setEndingMed(false); }
  };

  /* ── Chart data ─────────────────────────────────── */

  const vitalChartData = [...vitals]
    .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime())
    .map((v) => ({ time: format(new Date(v.timestamp), "HH:mm"), value: v.value, source: v.source }));

  const sleepChartData = [...sleep]
    .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
    .map((s) => ({ date: format(new Date(s.startTime), "MMM d"), duration: s.durationMinutes, quality: s.qualityScore }));

  const glassTooltipStyle = {
    borderRadius: "14px",
    border: "1px solid rgba(255,255,255,0.15)",
    boxShadow: "0 4px 24px rgba(0,0,0,0.5)",
    backgroundColor: "rgba(10,20,35,0.85)",
    color: "#ffffff",
    backdropFilter: "blur(12px)",
  };

  /* ── Loading / not-found ─────────────────────────── */

  if (loading) {
    return (
      <div className="max-w-6xl mx-auto space-y-6">
        <div className="h-12 w-72 bg-white/20 rounded-2xl animate-pulse" />
        <div className="grid gap-4 md:grid-cols-5">
          {[1,2,3,4,5].map((i) => <div key={i} className="glass-panel h-24 animate-pulse" />)}
        </div>
        <div className="glass-panel h-80 animate-pulse" />
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="space-y-4">
        <button
          onClick={() => router.push("/patients")}
          className="flex items-center gap-2 text-white/70 hover:text-white transition-colors"
        >
          <ArrowLeft className="h-4 w-4" /> Back
        </button>
        <p className="text-white/60">Patient not found or you do not have access.</p>
      </div>
    );
  }

  /* ── Render ───────────────────────────────────────── */

  const currentMeta = vitalMeta[vitalType] ?? { color: "#00D4C8", label: vitalType };
  const tabItems = [
    { key: "vitals", label: "Vitals", icon: Heart },
    { key: "sleep", label: "Sleep", icon: Moon },
    { key: "medications", label: "Medications", icon: Pill },
  ] as const;

  return (
    <div className="max-w-6xl mx-auto space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500">

      {/* ── Dialogs ───────────────────────────────────── */}

      {/* Check Interactions Dialog */}
      <Dialog open={showInteractionDialog} onOpenChange={setShowInteractionDialog}>
        <DialogContent className="bg-black/50 backdrop-blur-2xl border-white/15 text-white sm:max-w-lg">
          <DialogHeader>
            <DialogTitle className="text-white">Check Drug Interactions</DialogTitle>
            <DialogDescription className="text-white/60">
              Enter one or two medications. Optionally include active medications.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <label htmlFor="check-med-1" className="text-xs text-white/60 font-medium">Medication 1 *</label>
              <input id="check-med-1" className={glassInput()} placeholder="e.g. Ibuprofen"
                value={checkMedication1} onChange={(e) => setCheckMedication1(e.target.value)} />
            </div>
            <label className="flex items-center gap-2 text-sm text-white/80 cursor-pointer">
              <input type="checkbox" checked={includeActiveMeds} onChange={(e) => setIncludeActiveMeds(e.target.checked)} />
              Include active medications
            </label>
            <div className="space-y-1">
              <label htmlFor="check-med-2" className="text-xs text-white/60 font-medium">
                Medication 2 {includeActiveMeds ? "(optional)" : "*"}
              </label>
              <input id="check-med-2" className={glassInput()} placeholder={includeActiveMeds ? "Optional…" : "e.g. Warfarin"}
                value={checkMedication2} onChange={(e) => setCheckMedication2(e.target.value)} />
            </div>
            {interactionError && <div className="rounded-xl border border-red-500/40 bg-red-500/10 px-3 py-2 text-sm text-red-300">{interactionError}</div>}
            {!interactionError && hasCheckedInteractions && checkedInteractions.length === 0 && !checkingInteractions && (
              <div className="rounded-xl border border-green-500/40 bg-green-500/10 px-3 py-2 text-sm text-green-300">No interactions found.</div>
            )}
            {checkedInteractions.length > 0 && (
              <div className="space-y-2 max-h-64 overflow-auto pr-1">
                <div className="rounded-xl border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-300">
                  Found {checkedInteractions.length} interaction{checkedInteractions.length === 1 ? "" : "s"}.
                </div>
                {checkedInteractions.map((interaction, idx) => (
                  <div key={`${interaction.drugARxCui}-${interaction.drugBRxCui}-${idx}`} className="rounded-xl border border-white/10 bg-white/5 p-3">
                    <div className="flex items-center justify-between gap-2 mb-1">
                      <div className="text-sm font-medium text-white">{getInteractionPairDisplay(interaction)}</div>
                      <span className={`text-xs border rounded-full px-2 py-0.5 ${severityBadgeClass(interaction.severity)}`}>
                        {severityLabel(interaction.severity)}
                      </span>
                    </div>
                    <div className="text-xs text-white/50">{interaction.description}</div>
                  </div>
                ))}
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="ghost" className="text-white/70 hover:bg-white/10 hover:text-white" onClick={() => setShowInteractionDialog(false)}>Close</Button>
            <button
              onClick={handleCheckInteractions}
              disabled={checkingInteractions || !checkMedication1.trim() || (!includeActiveMeds && !checkMedication2.trim())}
              className="px-5 py-2 rounded-xl bg-white text-black text-sm font-semibold hover:bg-white/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {checkingInteractions ? "Checking…" : "Check"}
            </button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* End Medication Dialog */}
      <Dialog open={!!endMedDialog} onOpenChange={(open) => !open && setEndMedDialog(null)}>
        <DialogContent className="bg-black/50 backdrop-blur-2xl border-white/15 text-white">
          <DialogHeader>
            <DialogTitle className="text-white">{endMedDialog?.isDiscontinued ? "Remove medication" : "End medication"}</DialogTitle>
            <DialogDescription className="text-white/60">
              {endMedDialog?.isDiscontinued
                ? "This will permanently remove this medication from the record."
                : `End "${endMedDialog?.medName}" and record the reason (required).`}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <label htmlFor="end-med-reason" className="text-sm font-medium text-white/90">Reason *</label>
            <input id="end-med-reason" className={glassInput()} placeholder="e.g. Treatment completed, side effects, etc."
              value={endMedReason} onChange={(e) => setEndMedReason(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="ghost" className="text-white/70 hover:bg-white/10 hover:text-white" onClick={() => setEndMedDialog(null)}>Cancel</Button>
            <button
              disabled={!endMedReason.trim() || endingMed}
              onClick={handleConfirmEndMedication}
              className="px-5 py-2 rounded-xl bg-red-500 text-white text-sm font-semibold hover:bg-red-600 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {endingMed ? "…" : endMedDialog?.isDiscontinued ? "Remove" : "End"}
            </button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Header ────────────────────────────────────── */}

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <button
            onClick={() => router.push("/patients")}
            className="w-10 h-10 rounded-xl bg-white/10 hover:bg-white/20 flex items-center justify-center transition-colors"
          >
            <ArrowLeft className="h-5 w-5 text-white" />
          </button>
          <div className="w-12 h-12 rounded-full bg-[#007AFF]/70 text-white flex items-center justify-center font-bold text-xl uppercase select-none">
            {patient.fullName.substring(0, 2)}
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-white">{patient.fullName}</h1>
            <p className="text-white/60 text-sm">{patient.email}</p>
          </div>
        </div>
        <button
          onClick={handleUnassign}
          className="px-4 py-2 rounded-xl bg-red-500 text-white text-sm font-semibold hover:bg-red-600 transition-colors"
        >
          Unassign
        </button>
      </div>

      {/* ── Info cards ───────────────────────────────── */}

      <div className="grid gap-4 md:grid-cols-5">
        <InfoCard label="Blood Type" value={patient.bloodType ?? "—"} />
        <InfoCard label="Allergies" value={patient.allergies ?? "None"} />
        <InfoCard label="CNP" value={patient.cnp ?? "—"} />
        <InfoCard label="Member Since" value={format(new Date(patient.createdAt), "MMM d, yyyy")} />
        <InfoCard label="Last Updated" value={vitals.length > 0 ? (() => {
          const latestVital = vitals.reduce((max, v) => new Date(v.timestamp) > new Date(max.timestamp) ? v : max);
          return format(new Date(latestVital.timestamp), "MMM d, yyyy");
        })() : format(new Date(patient.updatedAt), "MMM d, yyyy")} />
      </div>

      {/* ── Medical Notes ────────────────────────────── */}

      {patient.medicalHistoryNotes && (
        <div className="glass-panel p-6">
          <h3 className="text-sm font-semibold text-white mb-3">Medical History Notes</h3>
          <p className="text-sm text-white/70 whitespace-pre-wrap">{patient.medicalHistoryNotes}</p>
        </div>
      )}

      {/* ── Structured History ───────────────────────── */}

      {historyEntries.length > 0 && (
        <div className="glass-panel p-6">
          <h3 className="text-sm font-semibold text-white mb-1">Medical History (Structured)</h3>
          <p className="text-xs text-white/50 mb-4">Extracted from uploaded medical documents</p>
          <div className={`space-y-3 ${historyEntries.length > 3 ? "max-h-64 overflow-auto pr-1" : ""}`}>
            {historyEntries.map((e) => (
              <div key={e.id} className="rounded-xl border border-white/10 bg-white/5 p-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="text-sm font-medium text-white">{e.summary || e.title}</div>
                    <div className="text-xs text-white/50">{format(new Date(e.eventDate), "MMM d, yyyy")} • {e.title}</div>
                  </div>
                  <span className="text-[11px] text-white/50 border border-white/20 rounded-full px-2 py-0.5 shrink-0">
                    {(e.confidence * 100).toFixed(0)}%
                  </span>
                </div>
                {(e.medicationName || e.dosage || e.frequency || e.duration) && (
                  <div className="mt-2 text-xs text-white/50">
                    {[e.medicationName, e.dosage, e.frequency, e.duration].filter(Boolean).join(" • ")}
                  </div>
                )}
                {e.notes && <div className="mt-2 text-xs text-white/50 whitespace-pre-wrap">{e.notes}</div>}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ── Tabs ─────────────────────────────────────── */}

      <div>
        {/* Tab bar */}
        <div className="glass-pill flex gap-1 p-1 mb-6 w-fit">
          {tabItems.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={`flex items-center gap-2 px-5 py-2 rounded-full text-sm font-medium transition-all ${
                activeTab === key
                  ? "bg-white/20 text-white shadow"
                  : "text-white/60 hover:text-white"
              }`}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        {/* ── Vitals Tab ──────────────────────────────── */}

        {activeTab === "vitals" && (
          <div className="space-y-6">
            {/* Metric pills + Refresh */}
            <div className="flex flex-wrap gap-2 items-center">
              {vitalTypes.map((t) => (
                <button
                  key={t}
                  onClick={() => loadVitals(t)}
                  className={`px-4 py-1.5 rounded-full text-xs font-medium whitespace-nowrap transition-all border ${
                    vitalType === t
                      ? "bg-white text-black border-transparent shadow-md"
                      : "bg-transparent border-white/20 text-white/60 hover:text-white hover:border-white/40"
                  }`}
                >
                  {vitalMeta[t]?.label ?? t}
                </button>
              ))}
              <button
                onClick={handleRefreshVitals}
                disabled={refreshingVitals}
                className="ml-auto p-2 rounded-full text-white/60 hover:text-white hover:bg-white/10 transition-all disabled:opacity-50"
                title="Refresh vitals"
              >
                <RotateCw className={`w-4 h-4 ${refreshingVitals ? 'animate-spin' : ''}`} />
              </button>
            </div>

            {vitalChartData.length > 0 ? (
              <div className="glass-panel p-6">
                <h3 className="text-lg font-semibold text-white mb-1">{currentMeta.label}</h3>
                <p className="text-sm text-white/50 mb-6">
                  {vitals.length} readings — latest{" "}
                  {vitals.length > 0 ? (() => {
                    const latestVital = vitals.reduce((max, v) => new Date(v.timestamp) > new Date(max.timestamp) ? v : max);
                    return format(new Date(latestVital.timestamp), "MMM d, HH:mm");
                  })() : ""}
                </p>
                <div className="h-[300px] w-full">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={vitalChartData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                      <defs>
                        <linearGradient id={`grad-${vitalType}`} x1="0" y1="0" x2="0" y2="1">
                          <stop offset="5%" stopColor={currentMeta.color} stopOpacity={0.6} />
                          <stop offset="95%" stopColor={currentMeta.color} stopOpacity={0} />
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.08)" />
                      <XAxis dataKey="time" axisLine={false} tickLine={false} tick={{ fill: "rgba(255,255,255,0.5)", fontSize: 11 }} interval={Math.floor(vitalChartData.length / 8)} />
                      <YAxis axisLine={false} tickLine={false} tick={{ fill: "rgba(255,255,255,0.5)", fontSize: 11 }} />
                      <Tooltip contentStyle={glassTooltipStyle} />
                      <Area type="monotone" dataKey="value" stroke={currentMeta.color} strokeWidth={2} fillOpacity={1} fill={`url(#grad-${vitalType})`} />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
                <div className="flex items-center gap-2 mt-5">
                  <span className="text-sm font-medium text-white">{currentMeta.label} trend</span>
                  <TrendingUp className="w-4 h-4 text-white/60" />
                </div>
              </div>
            ) : (
              <div className="glass-panel p-12 text-center">
                <p className="text-white/50">No {vitalMeta[vitalType]?.label ?? vitalType} data available.</p>
              </div>
            )}
          </div>
        )}

        {/* ── Sleep Tab ───────────────────────────────── */}

        {activeTab === "sleep" && (
          <div className="space-y-6">
            {sleepChartData.length > 0 ? (
              <>
                <div className="glass-panel p-6">
                  <h3 className="text-lg font-semibold text-white mb-1">Sleep Duration</h3>
                  <p className="text-sm text-white/50 mb-6">Minutes per night</p>
                  <div className="h-[250px] w-full">
                    <ResponsiveContainer width="100%" height="100%">
                      <AreaChart data={sleepChartData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                        <defs>
                          <linearGradient id="gradSleep" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#5856D6" stopOpacity={0.6} />
                            <stop offset="95%" stopColor="#5856D6" stopOpacity={0} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.08)" />
                        <XAxis dataKey="date" axisLine={false} tickLine={false} tick={{ fill: "rgba(255,255,255,0.5)", fontSize: 11 }} />
                        <YAxis axisLine={false} tickLine={false} tick={{ fill: "rgba(255,255,255,0.5)", fontSize: 11 }} tickCount={3} />
                        <Tooltip contentStyle={glassTooltipStyle} />
                        <Area type="monotone" dataKey="duration" stroke="#5856D6" strokeWidth={2} fillOpacity={1} fill="url(#gradSleep)" />
                      </AreaChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="flex items-center gap-2 mt-5">
                    <span className="text-sm font-medium text-white">Sleep duration trend</span>
                    <TrendingUp className="w-4 h-4 text-white/60" />
                  </div>
                </div>

                <div className="glass-panel overflow-hidden">
                  <div className="px-6 py-4 border-b border-white/10">
                    <h3 className="text-sm font-semibold text-white">Sleep Sessions</h3>
                  </div>
                  <table className="w-full text-sm text-left">
                    <thead className="text-xs text-white/50 uppercase bg-white/5">
                      <tr>
                        <th className="px-6 py-3 font-medium">Date</th>
                        <th className="px-6 py-3 font-medium">Start</th>
                        <th className="px-6 py-3 font-medium">End</th>
                        <th className="px-6 py-3 font-medium">Duration</th>
                        <th className="px-6 py-3 font-medium">Quality</th>
                      </tr>
                    </thead>
                    <tbody>
                      {sleep.map((s) => (
                        <tr key={s.startTime} className="border-b border-white/10 hover:bg-white/5 transition-colors">
                          <td className="px-6 py-3 text-white">{format(new Date(s.startTime), "MMM d, yyyy")}</td>
                          <td className="px-6 py-3 text-white/70">{format(new Date(s.startTime), "HH:mm")}</td>
                          <td className="px-6 py-3 text-white/70">{format(new Date(s.endTime), "HH:mm")}</td>
                          <td className="px-6 py-3 text-white/70">{Math.floor(s.durationMinutes / 60)}h {s.durationMinutes % 60}m</td>
                          <td className="px-6 py-3">
                            <span className={`px-3 py-1 rounded-full text-xs font-medium ${qualityClass(s.qualityScore)}`}>
                              {s.qualityScore.toFixed(0)}%
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            ) : (
              <div className="glass-panel p-12 text-center">
                <p className="text-white/50">No sleep data available.</p>
              </div>
            )}
          </div>
        )}

        {/* ── Medications Tab ─────────────────────────── */}

        {activeTab === "medications" && (
          <div className="space-y-6">
            {/* Interaction warning */}
            {autoInteractions.length > 0 && (
              <div className="glass-panel border border-amber-500/40 bg-amber-500/5 p-6">
                <h3 className="text-sm font-semibold text-amber-300 mb-1">⚠ Interaction Warning</h3>
                <p className="text-xs text-amber-300/70 mb-4">
                  Detected {autoInteractions.length} interaction{autoInteractions.length === 1 ? "" : "s"} among active medications.
                </p>
                <div className="space-y-2 max-h-48 overflow-auto pr-1">
                  {autoInteractions.map((interaction, idx) => (
                    <div key={`${interaction.drugARxCui}-${interaction.drugBRxCui}-${idx}`} className="rounded-xl border border-white/10 bg-white/5 p-3">
                      <div className="flex items-center justify-between gap-2 mb-1">
                        <div className="text-sm font-medium text-white">{interaction.drugARxCui} + {interaction.drugBRxCui}</div>
                        <span className={`text-xs border rounded-full px-2 py-0.5 ${severityBadgeClass(interaction.severity)}`}>
                          {severityLabel(interaction.severity)}
                        </span>
                      </div>
                      <div className="text-xs text-white/50">{interaction.description}</div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Toolbar */}
            <div className="flex items-center justify-between">
              <p className="text-sm text-white/60">{medications.length} medication{medications.length === 1 ? "" : "s"}</p>
              <div className="flex gap-2">
                <button
                  onClick={openInteractionDialog}
                  className="px-4 py-2 rounded-xl border border-white/20 bg-white/5 text-white/80 text-sm font-medium hover:bg-white/10 hover:text-white transition-colors"
                >
                  Check interactions
                </button>
                <button
                  onClick={() => setShowPrescribeForm((v) => !v)}
                  className="flex items-center gap-2 px-4 py-2 rounded-xl bg-white text-black text-sm font-semibold hover:bg-white/90 transition-colors"
                >
                  <Pill className="w-4 h-4" />
                  {showPrescribeForm ? "Cancel" : "Prescribe"}
                </button>
              </div>
            </div>

            {/* Prescribe form */}
            {showPrescribeForm && (
              <div className="glass-panel p-6">
                <h3 className="text-sm font-semibold text-white mb-4">Prescribe Medication</h3>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <label htmlFor="prescribe-name" className="text-xs text-white/60 font-medium">Name *</label>
                    <input id="prescribe-name" className={glassInput()} placeholder="e.g. Metoprolol…"
                      value={prescribeName} onChange={(e) => setPrescribeName(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <label className="text-xs text-white/60 font-medium">Dosage *</label>
                    <div className="flex gap-2">
                      <input id="prescribe-dosage-amount" type="number" min="0" step="any" className={glassInput("flex-1")} placeholder="50"
                        value={prescribeDosageAmount} onChange={(e) => setPrescribeDosageAmount(e.target.value)} />
                      <select className={glassSelect("shrink-0")} value={prescribeDosageUnit}
                        onChange={(e) => setPrescribeDosageUnit(e.target.value as DosageUnit)}>
                        {DOSAGE_UNITS.map((u) => <option key={u} value={u}>{u}</option>)}
                      </select>
                    </div>
                  </div>
                  <div className="space-y-1">
                    <label htmlFor="prescribe-frequency" className="text-xs text-white/60 font-medium">Frequency</label>
                    <input id="prescribe-frequency" className={glassInput()} placeholder="e.g. Twice daily"
                      value={prescribeFrequency} onChange={(e) => setPrescribeFrequency(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <label htmlFor="prescribe-route" className="text-xs text-white/60 font-medium">Route</label>
                    <select id="prescribe-route" className={glassSelect("w-full")} value={prescribeRoute}
                      onChange={(e) => setPrescribeRoute(Number(e.target.value) as 0|1|2|3|4)}>
                      {Object.entries(MedicationRouteLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                    </select>
                  </div>
                  <div className="space-y-1 col-span-2">
                    <label htmlFor="prescribe-reason" className="text-xs text-white/60 font-medium">Reason (optional)</label>
                    <input id="prescribe-reason" className={glassInput()} placeholder="e.g. Blood pressure control"
                      value={prescribeReason} onChange={(e) => setPrescribeReason(e.target.value)} />
                  </div>
                </div>
                {prescribeError && (
                  <div className="mt-3 rounded-xl border border-red-500/40 bg-red-500/10 px-3 py-2 text-sm text-red-300">{prescribeError}</div>
                )}
                <button
                  onClick={handlePrescribe}
                  disabled={prescribing || !prescribeName.trim() || !prescribeDosageAmount.trim()}
                  className="mt-4 px-5 py-2 rounded-xl bg-white text-black text-sm font-semibold hover:bg-white/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  {prescribing ? "Saving…" : "Save Prescription"}
                </button>
              </div>
            )}

            {/* Medication list */}
            {medications.length > 0 ? (
              <div className="glass-panel overflow-hidden">
                <div className="px-6 py-4 border-b border-white/10">
                  <h3 className="text-sm font-semibold text-white">Medication List</h3>
                </div>
                <table className="w-full text-sm text-left">
                  <thead className="text-xs text-white/50 uppercase bg-white/5">
                    <tr>
                      <th className="px-6 py-3 font-medium">Name</th>
                      <th className="px-6 py-3 font-medium">Dosage</th>
                      <th className="px-6 py-3 font-medium">Frequency</th>
                      <th className="px-6 py-3 font-medium">Route</th>
                      <th className="px-6 py-3 font-medium">Status</th>
                      <th className="px-6 py-3 font-medium">Added By</th>
                      <th className="px-6 py-3 font-medium">Since</th>
                      <th className="px-6 py-3" />
                    </tr>
                  </thead>
                  <tbody>
                    {medications.map((m) => (
                      <tr key={m.id} className="border-b border-white/10 hover:bg-white/5 transition-colors">
                        <td className="px-6 py-3 font-medium text-white">{m.name}</td>
                        <td className="px-6 py-3 text-white/70">{m.dosage}</td>
                        <td className="px-6 py-3 text-white/60">{m.frequency ?? "—"}</td>
                        <td className="px-6 py-3 text-white/70">{MedicationRouteLabel[m.route]}</td>
                        <td className="px-6 py-3">
                          <span className={`px-3 py-1 rounded-full text-xs font-medium ${medStatusClass(m.status)}`}>
                            {MedicationStatusLabel[m.status]}
                          </span>
                        </td>
                        <td className="px-6 py-3">
                          <span className={`px-3 py-1 rounded-full text-xs font-medium ${m.addedByRole === 1 ? "bg-blue-500/20 text-blue-300" : m.addedByRole === 2 ? "bg-orange-500/20 text-orange-300" : "bg-white/10 text-white/60"}`}>
                            {m.addedByRole === 1 ? "Doctor" : m.addedByRole === 2 ? "OCR Scan" : "Patient"}
                          </span>
                        </td>
                        <td className="px-6 py-3 text-white/50 text-xs">
                          {m.createdAt ? format(new Date(m.createdAt), "MMM d, yyyy") : "—"}
                        </td>
                        <td className="px-6 py-3">
                          <button
                            title={m.status === 0 ? "End medication" : "Remove"}
                            onClick={() => handleEndMedication(m)}
                            className="w-8 h-8 rounded-lg hover:bg-white/10 flex items-center justify-center transition-colors"
                          >
                            {m.status === 0
                              ? <StopCircle className="h-4 w-4 text-red-400" />
                              : <Trash2 className="h-4 w-4 text-red-400" />}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="glass-panel p-12 text-center">
                <p className="text-white/50">No medications on record.</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
