"use client";

import { Suspense, useState } from "react";
import { signIn, useSession } from "next-auth/react";
import { useSearchParams, useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";

export default function LoginPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center">
          <Skeleton className="h-64 w-96" />
        </div>
      }
    >
      <LoginContent />
    </Suspense>
  );
}

function LoginContent() {
  const { data: session, update } = useSession();
  const searchParams = useSearchParams();
  const router = useRouter();

  const needsRegistration =
    searchParams.get("register") === "true" || session?.registrationRequired;
  const googleIdToken = session?.googleIdToken;
  const pendingEmail = session?.pendingEmail;

  const [secret, setSecret] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  // If fully authenticated (has API token), go to dashboard.
  if (session?.apiToken) {
    router.replace("/");
    return null;
  }

  const handleRegister = async () => {
    if (!googleIdToken) {
      setError("Google sign-in expired. Please sign in again.");
      return;
    }
    if (!secret.trim()) {
      setError("Please enter the registration secret.");
      return;
    }

    setLoading(true);
    setError("");

    try {
      const result = await api.registerDoctor(googleIdToken, secret.trim());
      // Force NextAuth to re-run the jwt callback with the new token.
      // We store it and trigger a session update.
      await update({
        apiToken: result.token,
        apiTokenExpires: result.expiresAt,
        registrationRequired: false,
      });
      // Redirect to dashboard.
      globalThis.window.location.href = "/";
    } catch (e: any) {
      const msg = e?.message ?? "";
      if (msg.includes("401")) {
        setError("Invalid registration secret. Please try again.");
      } else {
        setError(msg || "Registration failed.");
      }
    } finally {
      setLoading(false);
    }
  };

  // ── Registration step ──────────────────────────────────────────────────────

  if (needsRegistration) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <Card className="w-full max-w-sm">
          <CardHeader className="text-center">
            <CardTitle className="text-2xl font-bold">
              Doctor Registration
            </CardTitle>
            <p className="text-sm text-muted-foreground">
              Welcome{pendingEmail ? `, ${pendingEmail}` : ""}! Enter the
              registration secret to create your doctor account.
            </p>
          </CardHeader>
          <CardContent className="space-y-4">
            <Input
              type="password"
              placeholder="Registration secret"
              value={secret}
              onChange={(e) => setSecret(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleRegister()}
              autoFocus
            />
            {error && (
              <Badge variant="destructive" className="w-full justify-center">
                {error}
              </Badge>
            )}
            <Button
              className="w-full"
              size="lg"
              onClick={handleRegister}
              disabled={loading}
            >
              {loading ? "Registering…" : "Complete Registration"}
            </Button>
            <Button
              variant="ghost"
              className="w-full"
              onClick={() => signIn("google", { callbackUrl: "/" })}
            >
              Use a different Google account
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  // ── Initial Google sign-in step ────────────────────────────────────────────

  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl font-bold">Doctor Portal</CardTitle>
          <p className="text-sm text-muted-foreground">
            Sign in to access your patient dashboard
          </p>
        </CardHeader>
        <CardContent>
          <Button
            className="w-full"
            size="lg"
            onClick={() => signIn("google", { callbackUrl: "/" })}
          >
            <svg className="mr-2 h-5 w-5" viewBox="0 0 24 24">
              <path
                d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z"
                fill="#4285F4"
              />
              <path
                d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
                fill="#34A853"
              />
              <path
                d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
                fill="#FBBC05"
              />
              <path
                d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
                fill="#EA4335"
              />
            </svg>
            Sign in with Google
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
