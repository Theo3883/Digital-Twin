"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useApi } from "@/hooks/use-api";
import type { PatientSummary } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { UserPlus, ArrowRight } from "lucide-react";
import { format } from "date-fns";

export default function PatientsPage() {
  const api = useApi();
  const [patients, setPatients] = useState<PatientSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [assignEmail, setAssignEmail] = useState("");
  const [assignNotes, setAssignNotes] = useState("");
  const [assigning, setAssigning] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [error, setError] = useState("");

  const loadPatients = () => {
    setLoading(true);
    api
      .getMyPatients()
      .then(setPatients)
      .catch(console.error)
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadPatients();
  }, [api]);

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
    } catch (e: any) {
      setError(e.message || "Failed to assign patient.");
    } finally {
      setAssigning(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-16" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">My Patients</h1>
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <UserPlus className="mr-2 h-4 w-4" />
              Assign Patient
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Assign a Patient</DialogTitle>
            </DialogHeader>
            <div className="space-y-4 pt-2">
              <div>
                <label className="text-sm font-medium">Patient Email</label>
                <Input
                  placeholder="patient@example.com"
                  value={assignEmail}
                  onChange={(e) => setAssignEmail(e.target.value)}
                />
              </div>
              <div>
                <label className="text-sm font-medium">
                  Notes{" "}
                  <span className="text-muted-foreground">(optional)</span>
                </label>
                <Input
                  placeholder="e.g. Referred by Dr. Smith"
                  value={assignNotes}
                  onChange={(e) => setAssignNotes(e.target.value)}
                />
              </div>
              {error && (
                <p className="text-sm text-destructive">{error}</p>
              )}
              <Button
                className="w-full"
                onClick={handleAssign}
                disabled={assigning || !assignEmail.trim()}
              >
                {assigning ? "Assigning..." : "Assign Patient"}
              </Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {patients.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center">
            <p className="text-muted-foreground">
              No patients assigned yet. Click &quot;Assign Patient&quot; to add
              one by email.
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>
              {patients.length} Patient{patients.length !== 1 ? "s" : ""}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Blood Type</TableHead>
                  <TableHead>Assigned</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {patients.map((p) => (
                  <TableRow key={p.patientId}>
                    <TableCell className="font-medium">
                      {p.fullName || "—"}
                    </TableCell>
                    <TableCell>{p.email}</TableCell>
                    <TableCell>
                      {p.bloodType ? (
                        <Badge variant="outline">{p.bloodType}</Badge>
                      ) : (
                        "—"
                      )}
                    </TableCell>
                    <TableCell>
                      {format(new Date(p.assignedAt), "MMM d, yyyy")}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/patients/${p.patientId}`}>
                          View
                          <ArrowRight className="ml-1 h-4 w-4" />
                        </Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
