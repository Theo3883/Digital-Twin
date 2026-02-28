// Extend NextAuth types to include the .NET API token and registration state.

import "next-auth";

declare module "next-auth" {
  interface Session {
    apiToken?: string;
    registrationRequired?: boolean;
    googleIdToken?: string;
    pendingEmail?: string;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    apiToken?: string;
    apiTokenExpires?: string;
    registrationRequired?: boolean;
    googleIdToken?: string;
    pendingEmail?: string;
  }
}
