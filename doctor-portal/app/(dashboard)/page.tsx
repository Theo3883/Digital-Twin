"use client";

import { useEffect, useState } from "react";
import { useApi } from "@/hooks/use-api";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Users, Activity, Moon, TrendingUp } from "lucide-react";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import { AreaChart, Area, CartesianGrid, XAxis, YAxis } from "recharts";

const weeklyChartData = [
  { day: "Mon", heartRate: 72, steps: 6200 },
  { day: "Tue", heartRate: 68, steps: 8100 },
  { day: "Wed", heartRate: 75, steps: 5400 },
  { day: "Thu", heartRate: 70, steps: 9300 },
  { day: "Fri", heartRate: 74, steps: 7800 },
  { day: "Sat", heartRate: 66, steps: 11200 },
  { day: "Sun", heartRate: 69, steps: 4900 },
];

const weeklyChartConfig = {
  heartRate: {
    label: "Heart Rate",
    color: "var(--chart-1)",
  },
  steps: {
    label: "Steps",
    color: "var(--chart-2)",
  },
} satisfies ChartConfig;

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

      {/* Weekly activity area chart */}
      <Card>
        <CardHeader>
          <CardTitle>Weekly Patient Activity</CardTitle>
          <CardDescription>Average heart rate &amp; steps across your patients</CardDescription>
        </CardHeader>
        <CardContent>
          <ChartContainer config={weeklyChartConfig} className="h-[280px] w-full">
            <AreaChart
              accessibilityLayer
              data={weeklyChartData}
              margin={{ left: -20, right: 12 }}
            >
              <CartesianGrid vertical={false} />
              <XAxis
                dataKey="day"
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
                dataKey="steps"
                type="natural"
                fill="var(--color-steps)"
                fillOpacity={0.4}
                stroke="var(--color-steps)"
                stackId="a"
              />
              <Area
                dataKey="heartRate"
                type="natural"
                fill="var(--color-heartRate)"
                fillOpacity={0.4}
                stroke="var(--color-heartRate)"
                stackId="a"
              />
            </AreaChart>
          </ChartContainer>
        </CardContent>
        <CardFooter>
          <div className="flex w-full items-start gap-2 text-sm">
            <div className="grid gap-2">
              <div className="flex items-center gap-2 font-medium leading-none">
                Trending up this week <TrendingUp className="h-4 w-4" />
              </div>
              <div className="text-muted-foreground flex items-center gap-2 leading-none">
                Showing avg daily metrics across all assigned patients
              </div>
            </div>
          </div>
        </CardFooter>
      </Card>
    </div>
  );
}
