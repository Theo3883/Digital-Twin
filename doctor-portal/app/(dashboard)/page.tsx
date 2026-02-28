"use client";

import { useEffect, useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Users, Activity, Moon } from "lucide-react";

interface Dashboard {
  totalAssignedPatients: number;
  doctorName: string;
  doctorEmail: string;
}

export default function DashboardPage() {
  const { api, ready } = useApi();
  const [data, setData] = useState<Dashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    console.log("[Dashboard] effect fired | ready:", ready);
    if (!ready) return;
    console.log("[Dashboard] calling getDashboard()");
    api
      .getDashboard()
      .then((d) => { console.log("[Dashboard] success:", d); setData(d); })
      .catch((e) => console.error("[Dashboard] error:", e))
      .finally(() => setLoading(false));
  }, [api, ready]);

  if (loading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <div className="grid gap-4 md:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-32" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">
          Welcome, Dr. {data?.doctorName || "Doctor"}
        </h1>
        <p className="text-muted-foreground">{data?.doctorEmail}</p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">
              Assigned Patients
            </CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {data?.totalAssignedPatients ?? 0}
            </div>
            <p className="text-xs text-muted-foreground">
              patients under your care
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Live Vitals</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">Real-time</div>
            <p className="text-xs text-muted-foreground">
              monitoring via HealthKit sync
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Sleep Tracking</CardTitle>
            <Moon className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">Active</div>
            <p className="text-xs text-muted-foreground">
              automatic sleep session collection
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
