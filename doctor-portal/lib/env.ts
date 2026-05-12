// Environment variables for the doctor portal
function normalizeApiUrl(u?: string | null) {
  if (!u) return undefined;
  // prefer https, and strip trailing / if present
  try {
    const s = String(u).trim();
    return s.replace(/\/(?:api\/?$)?$/, "");
  } catch {
    return u;
  }
}

// Prefer explicit runtime vars first (API_BASE_URL / API_URL), then NEXT_PUBLIC_API_URL.
const resolvedApi = normalizeApiUrl(process.env.API_BASE_URL ?? process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL) ?? "http://localhost:5100";

export const env = {
  API_URL: resolvedApi,
  GOOGLE_CLIENT_ID: process.env.GOOGLE_CLIENT_ID ?? "",
  GOOGLE_CLIENT_SECRET: process.env.GOOGLE_CLIENT_SECRET ?? "",
  AUTH_SECRET: process.env.AUTH_SECRET ?? "",
} as const;
