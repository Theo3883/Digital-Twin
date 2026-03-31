"use client";

import { useEffect, useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Users, Activity, Moon, TrendingUp } from "lucide-react";
import {
  AreaChart,
  Area,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from "recharts";

const weeklyChartData = [
  { day: "Mon", heartRate: 72, steps: 6200 },
  { day: "Tue", heartRate: 68, steps: 8100 },
  { day: "Wed", heartRate: 75, steps: 5400 },
  { day: "Thu", heartRate: 70, steps: 9300 },
  { day: "Fri", heartRate: 74, steps: 7800 },
  { day: "Sat", heartRate: 66, steps: 11200 },
  { day: "Sun", heartRate: 69, steps: 4900 },
];

interface Dashboard {
  totalAssignedPatients: number;
  doctorName: string;
  doctorEmail: string;
}

const statCards = [
  {
    id: "patients",
    title: "Assigned Patients",
    icon: Users,
    getValue: (d: Dashboard) => d.totalAssignedPatients,
    sub: "patients under your care",
  },
  {
    id: "vitals",
    title: "Live Vitals",
    icon: Activity,
    getValue: () => "Real-time",
    sub: "monitoring via HealthKit sync",
  },
  {
    id: "sleep",
    title: "Sleep Tracking",
    icon: Moon,
    getValue: () => "Active",
    sub: "automatic sleep session collection",
  },
];

function SkeletonCard() {
  return (
    <div className="glass-panel p-6 animate-pulse">
      <div className="h-3 w-24 bg-white/20 rounded-full mb-4" />
      <div className="h-8 w-16 bg-white/20 rounded-full mb-2" />
      <div className="h-3 w-36 bg-white/10 rounded-full" />
    </div>
  );
}

export default function DashboardPage() {
  const { api, ready } = useApi();
  const [data, setData] = useState<Dashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!ready) return;
    api
      .getDashboard()
      .then(setData)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [api, ready]);

  if (loading) {
    return (
      <div className="max-w-6xl mx-auto space-y-8">
        <div className="h-8 w-64 bg-white/20 rounded-full animate-pulse" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {[1, 2, 3].map((i) => <SkeletonCard key={i} />)}
        </div>
        <div className="glass-panel h-80 animate-pulse" />
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-white">
          Welcome, Dr. {data?.doctorName || "Doctor"}
        </h1>
        <p className="text-white/60 mt-1">{data?.doctorEmail}</p>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {statCards.map((card) => {
          const Icon = card.icon;
          return (
            <div key={card.id} className="glass-panel p-6">
              <div className="flex items-center justify-between mb-3">
                <span className="text-sm font-medium text-white/80">
                  {card.title}
                </span>
                <Icon className="w-4 h-4 text-white/50" />
              </div>
              <div className="text-3xl font-bold text-white">
                {data ? String(card.getValue(data)) : "—"}
              </div>
              <p className="text-xs text-white/50 mt-1">{card.sub}</p>
            </div>
          );
        })}
      </div>

      {/* Weekly activity chart */}
      <div className="glass-panel p-6">
        <h2 className="text-lg font-semibold text-white mb-1">
          Weekly Patient Activity
        </h2>
        <p className="text-sm text-white/50 mb-6">
          Average heart rate &amp; steps across your patients
        </p>

        <div className="h-[320px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart
              data={weeklyChartData}
              margin={{ top: 10, right: 10, left: -20, bottom: 0 }}
            >
              <defs>
                <linearGradient id="gradSteps" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#00D4C8" stopOpacity={0.6} />
                  <stop offset="95%" stopColor="#00D4C8" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="gradHR" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#007AFF" stopOpacity={0.6} />
                  <stop offset="95%" stopColor="#007AFF" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid
                strokeDasharray="3 3"
                vertical={false}
                stroke="rgba(255,255,255,0.08)"
              />
              <XAxis
                dataKey="day"
                axisLine={false}
                tickLine={false}
                tick={{ fill: "rgba(255,255,255,0.5)", fontSize: 12 }}
              />
              <YAxis
                axisLine={false}
                tickLine={false}
                tick={{ fill: "rgba(255,255,255,0.5)", fontSize: 12 }}
              />
              <Tooltip
                contentStyle={{
                  borderRadius: "14px",
                  border: "1px solid rgba(255,255,255,0.15)",
                  boxShadow: "0 4px 24px rgba(0,0,0,0.5)",
                  backgroundColor: "rgba(10,20,35,0.85)",
                  color: "#ffffff",
                  backdropFilter: "blur(12px)",
                }}
              />
              <Area
                type="monotone"
                dataKey="steps"
                stroke="#00D4C8"
                strokeWidth={2}
                fillOpacity={1}
                fill="url(#gradSteps)"
              />
              <Area
                type="monotone"
                dataKey="heartRate"
                stroke="#007AFF"
                strokeWidth={2}
                fillOpacity={1}
                fill="url(#gradHR)"
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className="flex items-center gap-2 mt-6">
          <span className="text-sm font-medium text-white">
            Trending up this week
          </span>
          <TrendingUp className="w-4 h-4 text-[#00D4C8]" />
        </div>
        <p className="text-xs text-white/50 mt-1">
          Showing avg daily metrics across all assigned patients
        </p>
      </div>
    </div>
  );
}
