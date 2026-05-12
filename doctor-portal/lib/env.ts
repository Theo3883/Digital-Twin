// Resolve API URL from runtime environment variables.
// Prefer explicit API_URL or API_BASE_URL, then NEXT_PUBLIC_API_URL, then localhost fallback.

function normalizeApiUrl(u?: string | null) {
  if (!u) return undefined;
  try {
    const s = String(u).trim();
    return s.replace(/\/(?:api\/?$)?$/, "");
  } catch {
    return u;
  }
}

const resolvedApi =
  normalizeApiUrl(process.env.API_URL ?? process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_URL) ??
  "http://localhost:5003";

export const env = {
  API_URL: resolvedApi,
  GOOGLE_CLIENT_ID: process.env.GOOGLE_CLIENT_ID ?? "",
  GOOGLE_CLIENT_SECRET: process.env.GOOGLE_CLIENT_SECRET ?? "",
  AUTH_SECRET: process.env.AUTH_SECRET ?? "",
} as const;
