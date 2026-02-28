"use client";

import { useSession } from "next-auth/react";
import { useMemo } from "react";
import { ApiClient } from "@/lib/api";

type UseApiReturn = {
  api: ApiClient;
  /** true only once the session has loaded AND the API token is present */
  ready: boolean;
};

export function useApi(): UseApiReturn {
  const { data: session, status } = useSession();
  const token = (session as any)?.apiToken as string | undefined;

  console.log("[useApi] status:", status, "| token present:", !!token, "| token preview:", token?.slice(0, 20));

  const api = useMemo(() => {
    const client = new ApiClient();
    if (token) client.setToken(token);
    return client;
  }, [token]);

  return { api, ready: status === "authenticated" && !!token };
}
