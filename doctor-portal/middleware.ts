import { auth } from "@/lib/auth";
import { NextResponse } from "next/server";

export default auth((req) => {
  const { nextUrl } = req;
  const isLoginPage = nextUrl.pathname === "/login";
  const isAuthApi = nextUrl.pathname.startsWith("/api/auth");
  const isPublic =
    isLoginPage ||
    isAuthApi ||
    nextUrl.pathname.startsWith("/_next") ||
    nextUrl.pathname === "/favicon.ico";

  if (isPublic) return NextResponse.next();

  // Not signed in at all → redirect to login.
  if (!req.auth) {
    return NextResponse.redirect(new URL("/login", nextUrl));
  }

  // Signed in but registration required → redirect to login with flag.
  if ((req.auth as any)?.registrationRequired) {
    const url = new URL("/login", nextUrl);
    url.searchParams.set("register", "true");
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
});

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
