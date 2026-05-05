"use client";

import { useEffect, useMemo, useState } from "react";
import { useApi } from "@/hooks/use-api";
import type { NotificationItem } from "@/lib/api";
import { Bell, Check, Trash2 } from "lucide-react";
import { cn } from "@/lib/utils";

function severityClass(sev: number) {
  if (sev >= 4) return "bg-red-500/20 text-red-300 border-red-500/40";
  if (sev >= 3) return "bg-amber-500/20 text-amber-300 border-amber-500/40";
  if (sev >= 2) return "bg-yellow-500/20 text-yellow-300 border-yellow-500/40";
  return "bg-white/10 text-white/60 border-white/10";
}

function formatWhen(iso: string) {
  const date = new Date(iso);
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

export default function NotificationsPage() {
  const { api, ready } = useApi();
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(true);

  const unreadCount = useMemo(() => items.filter((n) => !n.readAt).length, [items]);

  async function refresh() {
    if (!ready) return;
    setLoading(true);
    try {
      const data = await api.getNotifications();
      setItems(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    refresh();
    if (!ready) return;
    const id = setInterval(refresh, 45000);
    return () => clearInterval(id);
  }, [ready]);

  async function markRead(id: string) {
    try {
      await api.markNotificationRead(id);
      setItems((prev) => prev.map((n) => (n.id === id ? { ...n, readAt: new Date().toISOString() } : n)));
    } catch (err) {
      console.error(err);
    }
  }

  async function deleteItem(id: string) {
    try {
      await api.deleteNotification(id);
      setItems((prev) => prev.filter((n) => n.id !== id));
    } catch (err) {
      console.error(err);
    }
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-white">Notifications</h1>
          <p className="text-white/60 mt-1">
            {unreadCount} unread
          </p>
        </div>
        <div className="glass-panel px-3 py-1.5 flex items-center gap-2 text-white/80">
          <Bell className="w-4 h-4" />
          <span className="text-sm">Inbox</span>
        </div>
      </div>

      {loading ? (
        <div className="glass-panel h-64 animate-pulse" />
      ) : items.length === 0 ? (
        <div className="glass-panel p-8 text-center text-white/60">
          No notifications yet.
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((item) => (
            <div
              key={item.id}
              className={cn(
                "glass-panel p-4 flex items-start gap-3",
                !item.readAt && "border border-white/20"
              )}
            >
              <div className={cn("text-xs border rounded-full px-2 py-0.5 shrink-0", severityClass(item.severity))}>
                {item.readAt ? "Read" : "New"}
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="text-white font-semibold truncate">{item.title}</h3>
                  <span className="text-xs text-white/50 shrink-0">{formatWhen(item.createdAt)}</span>
                </div>
                <p className="text-sm text-white/70 mt-1 line-clamp-2">{item.body}</p>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {!item.readAt && (
                  <button
                    onClick={() => markRead(item.id)}
                    className="p-2 rounded-lg hover:bg-white/10 text-white/70"
                    title="Mark read"
                  >
                    <Check className="w-4 h-4" />
                  </button>
                )}
                <button
                  onClick={() => deleteItem(item.id)}
                  className="p-2 rounded-lg hover:bg-white/10 text-white/50"
                  title="Delete"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
