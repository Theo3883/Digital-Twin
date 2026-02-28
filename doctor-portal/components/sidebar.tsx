"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { signOut, useSession } from "next-auth/react";
import {
  LayoutDashboard,
  Users,
  LogOut,
  Activity,
} from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";

const navItems = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/patients", label: "My Patients", icon: Users },
];

export function Sidebar() {
  const pathname = usePathname();
  const { data: session } = useSession();

  return (
    <aside className="flex h-screen w-64 flex-col border-r bg-card">
      {/* Brand */}
      <div className="flex items-center gap-2 px-6 py-5">
        <Activity className="h-6 w-6 text-primary" />
        <span className="text-lg font-bold">Digital Twin</span>
      </div>

      <Separator />

      {/* Nav */}
      <nav className="flex-1 space-y-1 px-3 py-4">
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
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "bg-primary/10 text-primary"
                  : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              )}
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </Link>
          );
        })}
      </nav>

      <Separator />

      {/* User */}
      <div className="flex items-center gap-3 px-4 py-4">
        <Avatar className="h-8 w-8">
          <AvatarImage src={session?.user?.image ?? ""} />
          <AvatarFallback>
            {session?.user?.name?.[0]?.toUpperCase() ?? "D"}
          </AvatarFallback>
        </Avatar>
        <div className="flex-1 truncate">
          <p className="text-sm font-medium truncate">
            {session?.user?.name ?? "Doctor"}
          </p>
          <p className="text-xs text-muted-foreground truncate">
            {session?.user?.email}
          </p>
        </div>
        <Button
          variant="ghost"
          size="icon"
          onClick={() => signOut({ callbackUrl: "/login" })}
        >
          <LogOut className="h-4 w-4" />
        </Button>
      </div>
    </aside>
  );
}
