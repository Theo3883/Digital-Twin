"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useApi } from "@/hooks/use-api";
import type { PatientSummary } from "@/lib/api";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { UserPlus, ArrowRight } from "lucide-react";
import { format } from "date-fns";

export default function PatientsPage() {
  const { api, ready } = useApi();
  const [patients, setPatients] = useState<PatientSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [assignEmail, setAssignEmail] = useState("");
  const [assignNotes, setAssignNotes] = useState("");
  const [assigning, setAssigning] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [error, setError] = useState("");

  const loadPatients = () => {
    if (!ready) return;
    setLoading(true);
    api
      .getMyPatients()
      .then(setPatients)
      .catch(console.error)
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadPatients();
  }, [ready]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleAssign = async () => {
    if (!assignEmail.trim()) return;
    setAssigning(true);
    setError("");
    try {
      await api.assignPatient(assignEmail.trim(), assignNotes.trim() || undefined);
      setAssignEmail("");
      setAssignNotes("");
      setDialogOpen(false);
      loadPatients();
    } catch (e: unknown) {
      setError((e as Error).message || "Failed to assign patient.");
    } finally {
      setAssigning(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-6xl mx-auto space-y-6">
        <div className="h-8 w-48 bg-white/20 rounded-full animate-pulse" />
        <div className="glass-panel p-6 space-y-4">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-14 bg-white/10 rounded-xl" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold tracking-tight text-white">
          My Patients
        </h1>

        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogTrigger asChild>
            <button className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-white text-black text-sm font-semibold hover:bg-white/90 transition-colors shadow-md">
              <UserPlus className="w-4 h-4" />
              Assign Patient
            </button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[425px] bg-black/50 backdrop-blur-2xl border-white/15 text-white">
            <DialogHeader>
              <DialogTitle className="text-white text-lg">
                Assign a Patient
              </DialogTitle>
            </DialogHeader>
            <div className="space-y-4 pt-2">
              <div className="grid gap-1.5">
                <label
                  htmlFor="assign-email"
                  className="text-sm font-medium text-white/90"
                >
                  Patient Email
                </label>
                <input
                  id="assign-email"
                  type="email"
                  placeholder="patient@example.com"
                  value={assignEmail}
                  onChange={(e) => setAssignEmail(e.target.value)}
                  className="flex h-10 w-full rounded-xl border border-white/20 bg-white/5 px-3 py-2 text-sm text-white placeholder:text-white/40 focus:outline-none focus:ring-2 focus:ring-white/30"
                />
              </div>
              <div className="grid gap-1.5">
                <label
                  htmlFor="assign-notes"
                  className="text-sm font-medium text-white/90"
                >
                  Notes{" "}
                  <span className="text-white/40 font-normal">(optional)</span>
                </label>
                <input
                  id="assign-notes"
                  placeholder="e.g. Referred by Dr. Smith"
                  value={assignNotes}
                  onChange={(e) => setAssignNotes(e.target.value)}
                  className="flex h-10 w-full rounded-xl border border-white/20 bg-white/5 px-3 py-2 text-sm text-white placeholder:text-white/40 focus:outline-none focus:ring-2 focus:ring-white/30"
                />
              </div>
              {error && (
                <p className="text-sm text-red-400">{error}</p>
              )}
              <button
                onClick={handleAssign}
                disabled={assigning || !assignEmail.trim()}
                className="w-full h-10 rounded-xl bg-white text-black text-sm font-semibold hover:bg-white/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {assigning ? "Assigning…" : "Assign Patient"}
              </button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {/* Patient list */}
      {patients.length === 0 ? (
        <div className="glass-panel p-12 text-center">
          <p className="text-white/60">
            No patients assigned yet. Click &quot;Assign Patient&quot; to add one by email.
          </p>
        </div>
      ) : (
        <div className="glass-panel overflow-hidden">
          <div className="px-6 py-5 border-b border-white/10">
            <h3 className="text-base font-semibold text-white">
              {patients.length} Patient{patients.length !== 1 ? "s" : ""}
            </h3>
          </div>
          <table className="w-full text-sm text-left">
            <thead className="text-xs text-white/50 uppercase bg-white/5">
              <tr>
                <th className="px-6 py-4 font-medium">Name</th>
                <th className="px-6 py-4 font-medium">Email</th>
                <th className="px-6 py-4 font-medium">Blood Type</th>
                <th className="px-6 py-4 font-medium">Assigned</th>
                <th className="px-6 py-4 font-medium text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {patients.map((p) => (
                <tr
                  key={p.patientId}
                  className="border-b border-white/10 hover:bg-white/5 transition-colors"
                >
                  <td className="px-6 py-4 font-medium text-white">
                    {p.fullName || "—"}
                  </td>
                  <td className="px-6 py-4 text-white/60">{p.email}</td>
                  <td className="px-6 py-4">
                    {p.bloodType ? (
                      <span className="px-3 py-1 rounded-full bg-white/10 border border-white/20 text-xs font-medium text-white">
                        {p.bloodType}
                      </span>
                    ) : (
                      <span className="text-white/40">—</span>
                    )}
                  </td>
                  <td className="px-6 py-4 text-white/60">
                    {format(new Date(p.assignedAt), "MMM d, yyyy")}
                  </td>
                  <td className="px-6 py-4 text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="gap-1 text-white/70 hover:text-white hover:bg-white/10"
                      asChild
                    >
                      <Link href={`/patients/${p.patientId}`}>
                        View
                        <ArrowRight className="w-4 h-4" />
                      </Link>
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
