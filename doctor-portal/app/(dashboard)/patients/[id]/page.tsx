"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useApi } from "@/hooks/use-api";
import type { PatientDetail, VitalSign, SleepSession } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
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
} from "@/components/ui/chart";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  BarChart,
  Bar,
} from "recharts";
import { ArrowLeft, Heart, Activity, Footprints, Moon } from "lucide-react";
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

const chartConfig: Record<string, { label: string; color: string }> = {
  HeartRate: { label: "Heart Rate", color: "hsl(var(--chart-1))" },
  SpO2: { label: "SpO2", color: "hsl(var(--chart-2))" },
  Steps: { label: "Steps", color: "hsl(var(--chart-3))" },
  Calories: { label: "Calories", color: "hsl(var(--chart-4))" },
  ActiveEnergy: { label: "Active Energy", color: "hsl(var(--chart-5))" },
  ExerciseMinutes: { label: "Exercise", color: "hsl(var(--chart-1))" },
  StandHours: { label: "Stand Hours", color: "hsl(var(--chart-2))" },
  sleep: { label: "Duration (min)", color: "hsl(var(--chart-3))" },
};

export default function PatientDetailPage() {
  const params = useParams();
  const router = useRouter();
  const api = useApi();
  const patientId = Number(params.id);

  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [vitals, setVitals] = useState<VitalSign[]>([]);
  const [sleep, setSleep] = useState<SleepSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [vitalType, setVitalType] = useState("HeartRate");

  useEffect(() => {
    if (!patientId) return;
    setLoading(true);

    Promise.all([
      api.getPatientDetail(patientId),
      api.getPatientVitals(patientId, { type: "HeartRate" }),
      api.getPatientSleep(patientId),
    ])
      .then(([p, v, s]) => {
        setPatient(p);
        setVitals(v);
        setSleep(s);
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [api, patientId]);

  const loadVitals = (type: string) => {
    setVitalType(type);
    api.getPatientVitals(patientId, { type }).then(setVitals).catch(console.error);
  };

  const handleUnassign = async () => {
    if (!confirm("Remove this patient from your care?")) return;
    await api.unassignPatient(patientId);
    router.push("/patients");
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

  const vitalChartData = vitals
    .sort(
      (a, b) =>
        new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
    )
    .map((v) => ({
      time: format(new Date(v.timestamp), "HH:mm"),
      value: v.value,
    }));

  const sleepChartData = sleep
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
              <CardContent className="pt-6">
                <ChartContainer config={{ [vitalType]: chartConfig[vitalType] ?? { label: vitalType, color: "hsl(var(--chart-1))" } }} className="h-[300px] w-full">
                  <LineChart data={vitalChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="time" fontSize={12} />
                    <YAxis fontSize={12} />
                    <ChartTooltip content={<ChartTooltipContent />} />
                    <Line
                      type="monotone"
                      dataKey="value"
                      stroke={chartConfig[vitalType]?.color ?? "hsl(var(--chart-1))"}
                      strokeWidth={2}
                      dot={false}
                    />
                  </LineChart>
                </ChartContainer>
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                No {vitalType} data available.
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
                  <CardTitle className="text-sm">Sleep Duration</CardTitle>
                </CardHeader>
                <CardContent>
                  <ChartContainer config={{ sleep: chartConfig.sleep }} className="h-[250px] w-full">
                    <BarChart data={sleepChartData}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="date" fontSize={12} />
                      <YAxis fontSize={12} />
                      <ChartTooltip content={<ChartTooltipContent />} />
                      <Bar
                        dataKey="duration"
                        fill="hsl(var(--chart-3))"
                        radius={[4, 4, 0, 0]}
                      />
                    </BarChart>
                  </ChartContainer>
                </CardContent>
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
                      {sleep.map((s, i) => (
                        <TableRow key={i}>
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
                            <Badge
                              variant={
                                s.qualityScore >= 80
                                  ? "default"
                                  : s.qualityScore >= 50
                                  ? "secondary"
                                  : "destructive"
                              }
                            >
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

function InfoCard({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <CardContent className="pt-6">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="text-lg font-semibold">{value}</p>
      </CardContent>
    </Card>
  );
}
