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
} from "@/lib/api";
import { MedicationStatusLabel, MedicationRouteLabel } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
} from "recharts";
import { ArrowLeft, Heart, Moon, TrendingUp, Pill, Trash2, StopCircle } from "lucide-react";
import { format } from "date-fns";

const vitalTypes = [
  "HeartRate",
  "SpO2",
  "Steps",
  "Calories",
  "ActiveEnergy",
  "ExerciseMinutes",
  "StandHours",
];

const chartConfig: ChartConfig = {
  HeartRate: { label: "Heart Rate", color: "var(--chart-1)" },
  SpO2: { label: "SpO2", color: "var(--chart-2)" },
  Steps: { label: "Steps", color: "var(--chart-3)" },
  Calories: { label: "Calories", color: "var(--chart-4)" },
  ActiveEnergy: { label: "Active Energy", color: "var(--chart-5)" },
  ExerciseMinutes: { label: "Exercise", color: "var(--chart-1)" },
  StandHours: { label: "Stand Hours", color: "var(--chart-2)" },
  sleep: { label: "Duration (min)", color: "var(--chart-3)" },
} satisfies ChartConfig;

function getQualityVariant(score: number): "default" | "secondary" | "destructive" {
  if (score >= 80) return "default";
  if (score >= 50) return "secondary";
  return "destructive";
}

function getMedStatusVariant(status: number): "default" | "destructive" | "secondary" {
  if (status === 0) return "default";
  if (status === 1) return "destructive";
  return "secondary";
}

export default function PatientDetailPage() {
  const params = useParams();
  const router = useRouter();
  const { api, ready } = useApi();
  const patientId = params.id as string;

  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [vitals, setVitals] = useState<VitalSign[]>([]);
  const [sleep, setSleep] = useState<SleepSession[]>([]);
  const [medications, setMedications] = useState<Medication[]>([]);
  const [loading, setLoading] = useState(true);
  const [vitalType, setVitalType] = useState("HeartRate");

  // Prescribe form state
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

  // Interaction checker dialog state
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

  const [endMedDialog, setEndMedDialog] = useState<{ medId: string; medName: string; isDiscontinued: boolean } | null>(null);
  const [endMedReason, setEndMedReason] = useState("");
  const [endingMed, setEndingMed] = useState(false);

  useEffect(() => {
    console.log("[PatientDetail] effect fired | ready:", ready, "| patientId:", patientId);
    if (!patientId || !ready) return;
    setLoading(true);

    Promise.all([
      api.getPatientDetail(patientId),
      api.getPatientVitals(patientId, { type: "HeartRate" }),
      api.getPatientSleep(patientId),
      api.getPatientMedications(patientId),
    ])
      .then(([p, v, s, m]) => { setPatient(p); setVitals(v); setSleep(s); setMedications(m); })
      .catch((e) => console.error("[PatientDetail] error:", e))
      .finally(() => setLoading(false));
  }, [api, patientId, ready]);

  const loadVitals = (type: string) => {
    setVitalType(type);
    api.getPatientVitals(patientId, { type }).then(setVitals).catch(console.error);
  };

  const handleUnassign = async () => {
    if (!confirm("Remove this patient from your care?")) return;
    await api.unassignPatient(patientId);
    router.push("/patients");
  };

  const handlePrescribe = async () => {
    if (!prescribeName.trim() || !prescribeDosageAmount.trim()) return;
    setPrescribing(true);
    setPrescribeError(null);
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
      setShowPrescribeForm(false);
      setPrescribeName(""); setPrescribeDosageAmount(""); setPrescribeDosageUnit("mg"); setPrescribeFrequency("");
      setPrescribeRoute(0); setPrescribeReason("");
    } catch (e) {
      console.error("[Prescribe] error:", e);
      setPrescribeError(parseApiError(e));
    } finally {
      setPrescribing(false);
    }
  };

  const parseApiError = (err: unknown) => {
    const raw = err instanceof Error ? err.message : String(err ?? "Unknown error");
    const prefix = /^API\s+\d+:\s*/i;
    const body = raw.replace(prefix, "").trim();
    try {
      const parsed = JSON.parse(body) as { error?: string };
      if (parsed?.error) return parsed.error;
    } catch {
      // ignore parse failures
    }
    return body || "Request failed.";
  };

  const normalizeForMatch = (value: string) =>
    value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9\s]/g, " ")
      .replace(/\s+/g, " ");

  const getMatchScore = (input: string, candidate: string) => {
    if (!input || !candidate) return 0;
    if (candidate === input) return 100;
    if (candidate.startsWith(`${input} `)) return 90;
    if (candidate.includes(` ${input} `) || candidate.endsWith(` ${input}`)) return 80;
    if (candidate.includes(input)) return 60;
    const tokens = input.split(" ").filter(Boolean);
    const overlap = tokens.filter((t) => candidate.includes(t)).length;
    return overlap * 10;
  };

  const resolveBestDrugMatch = async (typedName: string): Promise<DrugSearchResult | null> => {
    const input = typedName.trim();
    if (!input) return null;

    const normalizedInput = normalizeForMatch(input);
    const matches = await api.searchDrugs(input, 20);
    if (!matches.length) return null;

    const best = [...matches]
      .sort((a, b) => {
        const aScore = getMatchScore(normalizedInput, normalizeForMatch(a.name));
        const bScore = getMatchScore(normalizedInput, normalizeForMatch(b.name));
        if (aScore !== bScore) return bScore - aScore;
        return a.name.length - b.name.length;
      })[0];

    return best ?? null;
  };

  const tryExtractNamesFromDescription = (description?: string) => {
    if (!description) return { a: null as string | null, b: null as string | null };
    const match = description.match(/between\s+([a-z0-9\-\s]+)\s+and\s+([a-z0-9\-\s]+)\.?/i);
    if (!match) return { a: null as string | null, b: null as string | null };
    const a = match[1]?.trim() || null;
    const b = match[2]?.trim() || null;
    return { a, b };
  };

  const resolveDisplayName = (
    rxCui: string,
    descriptionName: string | null,
    typedFallback: string,
  ) => {
    if (rxCuiNameMap[rxCui]) return rxCuiNameMap[rxCui];
    if (descriptionName) return descriptionName;
    if (typedFallback?.trim()) return typedFallback.trim();
    return `Medication (${rxCui})`;
  };

  const getInteractionPairDisplay = (interaction: MedicationInteraction) => {
    const parsed = tryExtractNamesFromDescription(interaction.description);
    const a = resolveDisplayName(interaction.drugARxCui, parsed.a, typedMedication1ForCheck);
    const b = resolveDisplayName(interaction.drugBRxCui, parsed.b, typedMedication2ForCheck);
    return `${a} + ${b}`;
  };

  const severityLabel = (sev: number) => {
    if (sev === 3) return "High Risk";
    if (sev === 2) return "Medium Risk";
    if (sev === 1) return "Low Risk";
    return "Unknown";
  };

  const severityBadgeClass = (sev: number) => {
    if (sev === 3) return "bg-red-500/20 text-red-300 border-red-500/40";
    if (sev === 2) return "bg-amber-500/20 text-amber-300 border-amber-500/40";
    return "bg-yellow-500/20 text-yellow-300 border-yellow-500/40";
  };

  const openInteractionDialog = () => {
    setCheckMedication1("");
    setCheckMedication2("");
    setIncludeActiveMeds(false);
    setInteractionError(null);
    setCheckedInteractions([]);
    setHasCheckedInteractions(false);
    setRxCuiNameMap({});
    setTypedMedication1ForCheck("");
    setTypedMedication2ForCheck("");
    setShowInteractionDialog(true);
  };

  const handleCheckInteractions = async () => {
    if (!checkMedication1.trim()) return;
    if (!includeActiveMeds && !checkMedication2.trim()) return;

    setCheckingInteractions(true);
    setInteractionError(null);
    setCheckedInteractions([]);
    setHasCheckedInteractions(false);
    setTypedMedication1ForCheck(checkMedication1.trim());
    setTypedMedication2ForCheck(checkMedication2.trim());

    try {
      const rxCuis: string[] = [];
      const nameMap: Record<string, string> = {};

      const med1 = await resolveBestDrugMatch(checkMedication1);
      if (!med1) {
        setInteractionError(`Could not find medication: "${checkMedication1}".`);
        return;
      }
      rxCuis.push(med1.rxCui);
      nameMap[med1.rxCui] = med1.name;

      if (checkMedication2.trim()) {
        const med2 = await resolveBestDrugMatch(checkMedication2);
        if (!med2) {
          setInteractionError(`Could not find medication: "${checkMedication2}".`);
          return;
        }
        rxCuis.push(med2.rxCui);
        nameMap[med2.rxCui] = med2.name;
      }

      if (includeActiveMeds) {
        medications
          .filter((m) => m.status === 0 && !!m.rxCui)
          .forEach((m) => {
            if (!m.rxCui) return;
            rxCuis.push(m.rxCui);
            nameMap[m.rxCui] = m.name;
          });
      }

      const uniqueRxCuis = Array.from(new Set(rxCuis));
      if (uniqueRxCuis.length < 2) {
        setInteractionError("Need at least two medications to check interactions.");
        return;
      }

      const interactions = await api.checkDrugInteractions(uniqueRxCuis);
      setRxCuiNameMap(nameMap);
      setCheckedInteractions(interactions);
      setHasCheckedInteractions(true);
    } catch (e) {
      console.error("[CheckInteractions] error:", e);
      setInteractionError(parseApiError(e));
    } finally {
      setCheckingInteractions(false);
    }
  };

  const handleEndMedication = (med: Medication) => {
    setEndMedDialog({ medId: med.id, medName: med.name, isDiscontinued: med.status === 1 });
    setEndMedReason("");
  };

  const handleConfirmEndMedication = async () => {
    if (!endMedDialog) return;
    if (!endMedReason.trim()) return;
    setEndingMed(true);
    try {
      if (endMedDialog.isDiscontinued) {
        await api.deletePatientMedication(patientId, endMedDialog.medId);
        setMedications((prev) => prev.filter((m) => m.id !== endMedDialog.medId));
      } else {
        await api.discontinuePatientMedication(patientId, endMedDialog.medId, endMedReason.trim());
        setMedications((prev) =>
          prev.map((m) =>
            m.id === endMedDialog.medId
              ? { ...m, status: 1 as const, endDate: new Date().toISOString(), discontinuedReason: endMedReason.trim() }
              : m
          )
        );
      }
      setEndMedDialog(null);
    } catch (e) {
      console.error("[EndMed] error:", e);
    } finally {
      setEndingMed(false);
    }
  };


  if (loading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-48" />
        <Skeleton className="h-64" />
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="space-y-4">
        <Button variant="ghost" onClick={() => router.push("/patients")}>
          <ArrowLeft className="mr-2 h-4 w-4" /> Back
        </Button>
        <p className="text-muted-foreground">
          Patient not found or you do not have access.
        </p>
      </div>
    );
  }

  const vitalChartData = [...vitals]
    .sort(
      (a, b) =>
        new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
    )
    .map((v) => ({
      time: format(new Date(v.timestamp), "HH:mm"),
      value: v.value,
    }));

  const sleepChartData = [...sleep]
    .sort(
      (a, b) =>
        new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
    )
    .map((s) => ({
      date: format(new Date(s.startTime), "MMM d"),
      duration: s.durationMinutes,
      quality: s.qualityScore,
    }));

  return (
    <div className="space-y-6">
      <Dialog open={showInteractionDialog} onOpenChange={setShowInteractionDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Check Drug Interactions</DialogTitle>
            <DialogDescription>
              Enter one or two medications. Optionally include active medications.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="space-y-1">
              <label htmlFor="check-med-1" className="text-xs text-muted-foreground font-medium">Medication 1 *</label>
              <input
                id="check-med-1"
                className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                placeholder="e.g. Ibuprofen"
                value={checkMedication1}
                onChange={(e) => setCheckMedication1(e.target.value)}
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={includeActiveMeds}
                onChange={(e) => setIncludeActiveMeds(e.target.checked)}
              />
              Include active medications
            </label>

            <div className="space-y-1">
              <label htmlFor="check-med-2" className="text-xs text-muted-foreground font-medium">
                Medication 2 {includeActiveMeds ? "(optional)" : "*"}
              </label>
              <input
                id="check-med-2"
                className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                placeholder={includeActiveMeds ? "Optional when active medications are included" : "e.g. Warfarin"}
                value={checkMedication2}
                onChange={(e) => setCheckMedication2(e.target.value)}
              />
            </div>

            {interactionError && (
              <div className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-sm text-red-300">
                {interactionError}
              </div>
            )}

            {!interactionError && hasCheckedInteractions && checkedInteractions.length === 0 && !checkingInteractions && (
              <div className="rounded-md border border-green-500/40 bg-green-500/10 px-3 py-2 text-sm text-green-300">
                No interactions found.
              </div>
            )}

            {checkedInteractions.length > 0 && (
              <div className="space-y-2 max-h-64 overflow-auto pr-1">
                <div className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-300">
                  Found {checkedInteractions.length} interaction{checkedInteractions.length === 1 ? "" : "s"}.
                </div>
                {checkedInteractions.map((interaction, idx) => (
                  <div key={`${interaction.drugARxCui}-${interaction.drugBRxCui}-${idx}`} className="rounded-md border p-3 bg-card/60">
                    <div className="flex items-center justify-between gap-2 mb-1">
                      <div className="text-sm font-medium">{getInteractionPairDisplay(interaction)}</div>
                      <span className={`text-xs border rounded-full px-2 py-0.5 ${severityBadgeClass(interaction.severity)}`}>
                        {severityLabel(interaction.severity)}
                      </span>
                    </div>
                    <div className="text-xs text-muted-foreground">{interaction.description}</div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowInteractionDialog(false)}>Close</Button>
            <Button
              onClick={handleCheckInteractions}
              disabled={checkingInteractions || !checkMedication1.trim() || (!includeActiveMeds && !checkMedication2.trim())}
            >
              {checkingInteractions ? "Checking..." : "Check"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!endMedDialog} onOpenChange={(open) => !open && setEndMedDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {endMedDialog?.isDiscontinued ? "Remove medication" : "End medication"}
            </DialogTitle>
            <DialogDescription>
              {endMedDialog?.isDiscontinued
                ? "This will permanently remove this medication from the record."
                : `End "${endMedDialog?.medName}" and record the reason (required).`}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <label htmlFor="end-med-reason" className="text-sm font-medium">Reason *</label>
            <input
              id="end-med-reason"
              className="w-full border rounded-md px-3 py-2 text-sm bg-background"
              placeholder="e.g. Treatment completed, side effects, etc."
              value={endMedReason}
              onChange={(e) => setEndMedReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEndMedDialog(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={!endMedReason.trim() || endingMed}
              onClick={handleConfirmEndMedication}
            >
              {(() => {
                if (endingMed) return "...";
                return endMedDialog?.isDiscontinued ? "Remove" : "End";
              })()}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.push("/patients")}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <Avatar className="h-12 w-12">
          <AvatarImage src={patient.photoUrl ?? ""} />
          <AvatarFallback>
            {patient.fullName[0]?.toUpperCase() ?? "P"}
          </AvatarFallback>
        </Avatar>
        <div className="flex-1">
          <h1 className="text-2xl font-bold">{patient.fullName}</h1>
          <p className="text-sm text-muted-foreground">{patient.email}</p>
        </div>
        <Button variant="destructive" size="sm" onClick={handleUnassign}>
          Unassign
        </Button>
      </div>

      {/* Info cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <InfoCard label="Blood Type" value={patient.bloodType ?? "—"} />
        <InfoCard label="Allergies" value={patient.allergies ?? "None"} />
        <InfoCard
          label="Member Since"
          value={format(new Date(patient.createdAt), "MMM d, yyyy")}
        />
        <InfoCard
          label="Last Updated"
          value={format(new Date(patient.updatedAt), "MMM d, yyyy")}
        />
      </div>

      {/* Medical Notes */}
      {patient.medicalHistoryNotes && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Medical History Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground whitespace-pre-wrap">
              {patient.medicalHistoryNotes}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Tabs: Vitals / Sleep */}
      <Tabs defaultValue="vitals">
        <TabsList>
          <TabsTrigger value="vitals">
            <Heart className="mr-1 h-4 w-4" /> Vitals
          </TabsTrigger>
          <TabsTrigger value="sleep">
            <Moon className="mr-1 h-4 w-4" /> Sleep
          </TabsTrigger>
          <TabsTrigger value="medications">
            <Pill className="mr-1 h-4 w-4" /> Medications
          </TabsTrigger>
        </TabsList>

        {/* Vitals Tab */}
        <TabsContent value="vitals" className="space-y-4">
          <div className="flex flex-wrap gap-2">
            {vitalTypes.map((t) => (
              <Badge
                key={t}
                variant={t === vitalType ? "default" : "outline"}
                className="cursor-pointer"
                onClick={() => loadVitals(t)}
              >
                {t}
              </Badge>
            ))}
          </div>

          {vitalChartData.length > 0 ? (
            <Card>
              <CardHeader>
                <CardTitle>{chartConfig[vitalType]?.label as string ?? vitalType}</CardTitle>
                <CardDescription>
                  {vitals.length} readings — latest {vitals.at(-1) ? format(new Date(vitals.at(-1)!.timestamp), "MMM d, HH:mm") : ""}
                </CardDescription>
              </CardHeader>
              <CardContent>
                <ChartContainer
                  config={{ value: chartConfig[vitalType] ?? { label: vitalType, color: "var(--chart-1)" } }}
                  className="h-[300px] w-full"
                >
                  <AreaChart
                    accessibilityLayer
                    data={vitalChartData}
                    margin={{ left: -20, right: 12 }}
                  >
                    <CartesianGrid vertical={false} />
                    <XAxis
                      dataKey="time"
                      tickLine={false}
                      axisLine={false}
                      tickMargin={8}
                    />
                    <YAxis
                      tickLine={false}
                      axisLine={false}
                      tickMargin={8}
                      tickCount={4}
                    />
                    <ChartTooltip cursor={false} content={<ChartTooltipContent />} />
                    <Area
                      dataKey="value"
                      type="natural"
                      fill={chartConfig[vitalType]?.color as string ?? "var(--chart-1)"}
                      fillOpacity={0.4}
                      stroke={chartConfig[vitalType]?.color as string ?? "var(--chart-1)"}
                    />
                  </AreaChart>
                </ChartContainer>
              </CardContent>
              <CardFooter>
                <div className="flex w-full items-start gap-2 text-sm">
                  <div className="flex items-center gap-2 font-medium leading-none">
                    {chartConfig[vitalType]?.label as string ?? vitalType} trend
                    <TrendingUp className="h-4 w-4" />
                  </div>
                </div>
              </CardFooter>
            </Card>
          ) : (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                No {vitalType} data available.
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Medications Tab */}
        <TabsContent value="medications" className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              {medications.length} medication{medications.length === 1 ? "" : "s"}
            </p>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" onClick={openInteractionDialog}>
                Check interactions
              </Button>
              <Button size="sm" onClick={() => setShowPrescribeForm((v) => !v)}>
                <Pill className="mr-1 h-4 w-4" />
                {showPrescribeForm ? "Cancel" : "Prescribe"}
              </Button>
            </div>
          </div>

          {showPrescribeForm && (
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Prescribe Medication</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <label htmlFor="prescribe-name" className="text-xs text-muted-foreground font-medium">Name *</label>
                    <input
                      id="prescribe-name"
                      className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                      placeholder="e.g. Nurofen, Ibuprofen, Metoprolol…"
                      value={prescribeName}
                      onChange={(e) => setPrescribeName(e.target.value)}
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-xs text-muted-foreground font-medium">Dosage *</label>
                    <div className="flex gap-2">
                      <input
                        id="prescribe-dosage-amount"
                        type="number"
                        min="0"
                        step="any"
                        className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                        placeholder="e.g. 50"
                        value={prescribeDosageAmount}
                        onChange={(e) => setPrescribeDosageAmount(e.target.value)}
                      />
                      <select
                        className="border rounded-md px-2 py-2 text-sm bg-background shrink-0"
                        value={prescribeDosageUnit}
                        onChange={(e) => setPrescribeDosageUnit(e.target.value as DosageUnit)}
                      >
                        {DOSAGE_UNITS.map((u) => (
                          <option key={u} value={u}>{u}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                  <div className="space-y-1">
                    <label htmlFor="prescribe-frequency" className="text-xs text-muted-foreground font-medium">Frequency</label>
                    <input
                      id="prescribe-frequency"
                      className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                      placeholder="e.g. Twice daily"
                      value={prescribeFrequency}
                      onChange={(e) => setPrescribeFrequency(e.target.value)}
                    />
                  </div>
                  <div className="space-y-1">
                    <label htmlFor="prescribe-route" className="text-xs text-muted-foreground font-medium">Route</label>
                    <select
                      id="prescribe-route"
                      className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                      value={prescribeRoute}
                      onChange={(e) => setPrescribeRoute(Number(e.target.value) as 0 | 1 | 2 | 3 | 4)}
                    >
                      {Object.entries(MedicationRouteLabel).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1">
                    <label htmlFor="prescribe-reason" className="text-xs text-muted-foreground font-medium">Reason (optional)</label>
                    <input
                      id="prescribe-reason"
                      className="w-full border rounded-md px-3 py-2 text-sm bg-background"
                      placeholder="e.g. Blood pressure control"
                      value={prescribeReason}
                      onChange={(e) => setPrescribeReason(e.target.value)}
                    />
                  </div>
                </div>
                {prescribeError && (
                  <div className="rounded-md border border-red-500/40 bg-red-500/10 px-3 py-2 text-sm text-red-300">
                    {prescribeError}
                  </div>
                )}
                <Button
                  size="sm"
                  disabled={prescribing || !prescribeName.trim() || !prescribeDosageAmount.trim()}
                  onClick={handlePrescribe}
                >
                  {prescribing ? "Saving..." : "Save Prescription"}
                </Button>
              </CardContent>
            </Card>
          )}

          {medications.length > 0 ? (
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Medication List</CardTitle>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Dosage</TableHead>
                      <TableHead>Frequency</TableHead>
                      <TableHead>Route</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Added By</TableHead>
                      <TableHead>Since</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {medications.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell className="font-medium">{m.name}</TableCell>
                        <TableCell>{m.dosage}</TableCell>
                        <TableCell className="text-muted-foreground">{m.frequency ?? "—"}</TableCell>
                        <TableCell>{MedicationRouteLabel[m.route]}</TableCell>
                        <TableCell>
                          <Badge variant={getMedStatusVariant(m.status)}>
                            {MedicationStatusLabel[m.status]}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Badge variant={m.addedByRole === 1 ? "default" : "outline"}>
                            {m.addedByRole === 1 ? "Doctor" : "Patient"}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-muted-foreground text-xs">
                          {m.createdAt ? format(new Date(m.createdAt), "MMM d, yyyy") : "—"}
                        </TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon"
                            title={m.status === 0 ? "End medication" : "Remove"}
                            onClick={() => handleEndMedication(m)}
                          >
                            {m.status === 0 ? (
                              <StopCircle className="h-4 w-4 text-destructive" />
                            ) : (
                              <Trash2 className="h-4 w-4 text-destructive" />
                            )}
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                No medications on record.
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Sleep Tab */}
        <TabsContent value="sleep" className="space-y-4">
          {sleepChartData.length > 0 ? (
            <>
              <Card>
                <CardHeader>
                  <CardTitle>Sleep Duration</CardTitle>
                  <CardDescription>Minutes per night</CardDescription>
                </CardHeader>
                <CardContent>
                  <ChartContainer config={{ duration: chartConfig.sleep }} className="h-[250px] w-full">
                    <AreaChart
                      accessibilityLayer
                      data={sleepChartData}
                      margin={{ left: -20, right: 12 }}
                    >
                      <CartesianGrid vertical={false} />
                      <XAxis
                        dataKey="date"
                        tickLine={false}
                        axisLine={false}
                        tickMargin={8}
                      />
                      <YAxis
                        tickLine={false}
                        axisLine={false}
                        tickMargin={8}
                        tickCount={3}
                      />
                      <ChartTooltip cursor={false} content={<ChartTooltipContent />} />
                      <Area
                        dataKey="duration"
                        type="natural"
                        fill="var(--chart-3)"
                        fillOpacity={0.4}
                        stroke="var(--chart-3)"
                      />
                    </AreaChart>
                  </ChartContainer>
                </CardContent>
                <CardFooter>
                  <div className="flex w-full items-start gap-2 text-sm">
                    <div className="flex items-center gap-2 font-medium leading-none">
                      Sleep duration trend <TrendingUp className="h-4 w-4" />
                    </div>
                  </div>
                </CardFooter>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="text-sm">Sleep Sessions</CardTitle>
                </CardHeader>
                <CardContent>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Date</TableHead>
                        <TableHead>Start</TableHead>
                        <TableHead>End</TableHead>
                        <TableHead>Duration</TableHead>
                        <TableHead>Quality</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {sleep.map((s) => (
                        <TableRow key={s.startTime}>
                          <TableCell>
                            {format(new Date(s.startTime), "MMM d, yyyy")}
                          </TableCell>
                          <TableCell>
                            {format(new Date(s.startTime), "HH:mm")}
                          </TableCell>
                          <TableCell>
                            {format(new Date(s.endTime), "HH:mm")}
                          </TableCell>
                          <TableCell>
                            {Math.floor(s.durationMinutes / 60)}h{" "}
                            {s.durationMinutes % 60}m
                          </TableCell>
                          <TableCell>
                            <Badge variant={getQualityVariant(s.qualityScore)}>
                              {s.qualityScore.toFixed(0)}%
                            </Badge>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>
            </>
          ) : (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                No sleep data available.
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function InfoCard({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <Card>
      <CardContent className="pt-6">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="text-lg font-semibold">{value}</p>
      </CardContent>
    </Card>
  );
}
