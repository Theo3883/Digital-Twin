import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import { env } from "@/lib/env";
import { api } from "@/lib/api";

export const { handlers, signIn, signOut, auth } = NextAuth({
  providers: [
    Google({
      clientId: env.GOOGLE_CLIENT_ID,
      clientSecret: env.GOOGLE_CLIENT_SECRET,
    }),
  ],
  callbacks: {
    async jwt({ token, account, trigger, session }) {
      // Handle client-side session.update() calls (e.g. after registration).
      if (trigger === "update") {
        const incoming = session as any;
        console.log("[NextAuth jwt] update trigger | incoming apiToken present:", !!incoming?.apiToken);
        if (incoming?.apiToken) {
          token.apiToken = incoming.apiToken;
          token.apiTokenExpires = incoming.apiTokenExpires;
          token.registrationRequired = false;
          token.googleIdToken = undefined;
        }
        return token;
      }

      // On first sign-in, exchange the Google id_token for a .NET API JWT.
      if (account?.id_token) {
        try {
          const result = await api.loginWithGoogle(account.id_token);

          if (result.registrationRequired) {
            // Doctor not yet registered — store the Google id_token so the
            // login page can call /api/auth/register with the secret.
            token.registrationRequired = true;
            token.googleIdToken = account.id_token;
            token.pendingEmail = result.email;
          } else {
            token.apiToken = result.token;
            token.apiTokenExpires = result.expiresAt;
            token.registrationRequired = false;
            token.googleIdToken = undefined;
          }
        } catch (e) {
          console.error("[NextAuth] Failed to exchange Google token:", e);
        }
      }
      return token;
    },
    async session({ session, token }) {
      // Expose the API JWT and registration state to the client.
      (session as any).apiToken = token.apiToken;
      (session as any).registrationRequired = token.registrationRequired ?? false;
      (session as any).googleIdToken = token.googleIdToken;
      (session as any).pendingEmail = token.pendingEmail;
      console.log("[NextAuth session] apiToken present:", !!(token as any).apiToken, "| registrationRequired:", token.registrationRequired);
      return session;
    },
  },
  pages: {
    signIn: "/login",
  },
  session: {
    strategy: "jwt",
  },
  secret: env.AUTH_SECRET,
});
