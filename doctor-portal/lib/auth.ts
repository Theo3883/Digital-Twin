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
    async jwt({ token, account }) {
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
