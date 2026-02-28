"use client";

import { useSession } from "next-auth/react";
import { useEffect, useMemo } from "react";
import { api } from "@/lib/api";

/**
 * Hook that returns the API client with the session JWT applied.
 * Must be used within a SessionProvider context.
 */
export function useApi() {
  const { data: session } = useSession();

  useEffect(() => {
    if (session?.apiToken) {
      api.setToken(session.apiToken);
    }
  }, [session?.apiToken]);

  return useMemo(() => api, []);
}
