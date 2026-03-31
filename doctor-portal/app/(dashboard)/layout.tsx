import { Providers } from "@/components/providers";
import { Sidebar } from "@/components/sidebar";

export default function DashboardLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <Providers>
      <div
        className="flex h-screen bg-cover bg-center overflow-hidden relative"
        style={{
          backgroundImage:
            'url("https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?q=80&w=2564&auto=format&fit=crop")',
        }}
      >
        {/* Dark overlay for readability */}
        <div className="absolute inset-0 bg-black/65 pointer-events-none z-0" />

        <Sidebar />

        <main className="flex-1 overflow-y-auto p-10 relative z-10">
          {children}
        </main>
      </div>
    </Providers>
  );
}
