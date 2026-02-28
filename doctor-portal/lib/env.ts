// Environment variables for the doctor portal
export const env = {
  API_URL: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5100",
  GOOGLE_CLIENT_ID: process.env.GOOGLE_CLIENT_ID ?? "",
  GOOGLE_CLIENT_SECRET: process.env.GOOGLE_CLIENT_SECRET ?? "",
  AUTH_SECRET: process.env.AUTH_SECRET ?? "",
} as const;
