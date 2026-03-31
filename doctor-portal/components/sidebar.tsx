"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { signOut, useSession } from "next-auth/react";
import { LayoutDashboard, Users, LogOut } from "lucide-react";
import { cn } from "@/lib/utils";

const navItems = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/patients", label: "My Patients", icon: Users },
];

export function Sidebar() {
  const pathname = usePathname();
  const { data: session } = useSession();

  const initials =
    session?.user?.name
      ?.split(" ")
      .map((n) => n[0])
      .join("")
      .substring(0, 2)
      .toUpperCase() ?? "D";

  return (
    <aside className="flex h-screen w-64 flex-col border-r border-white/10 bg-white/10 backdrop-blur-2xl p-6 z-20 shadow-2xl relative">
      {/* Brand */}
      <div className="flex items-center gap-3 mb-10">
        <div className="w-10 h-10 rounded-xl bg-[#007AFF] text-white flex items-center justify-center font-bold text-xl shadow-md select-none">
          DT
        </div>
        <span className="font-bold text-lg tracking-tight text-white">
          Digital Twin
        </span>
      </div>

      {/* Nav */}
      <nav className="flex-1 space-y-2">
        {navItems.map((item) => {
          const isActive =
            item.href === "/"
              ? pathname === "/"
              : pathname.startsWith(item.href);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-colors",
                isActive
                  ? "bg-[#007AFF] text-white shadow-md"
                  : "text-white/70 hover:bg-white/10 hover:text-white"
              )}
            >
              <item.icon className="h-5 w-5 shrink-0" />
              {item.label}
            </Link>
          );
        })}
      </nav>

      {/* User footer */}
      <button
        onClick={() => signOut({ callbackUrl: "/login" })}
        className="flex items-center gap-3 p-3 text-white/90 hover:bg-white/10 rounded-xl transition-colors mt-auto text-left w-full group"
      >
        <div className="w-10 h-10 rounded-full bg-[#007AFF]/70 text-white flex items-center justify-center font-medium text-base shrink-0 select-none">
          {initials}
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-medium text-white truncate">
            {session?.user?.name ?? "Doctor"}
          </p>
          <p className="text-xs text-white/50 truncate">
            {session?.user?.email}
          </p>
        </div>
        <LogOut className="h-4 w-4 shrink-0 text-white/50 group-hover:text-white transition-colors" />
      </button>
    </aside>
  );
}
