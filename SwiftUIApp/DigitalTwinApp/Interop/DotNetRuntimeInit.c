#include <stdint.h>
#include <stdbool.h>
// NativeAOT runtime initialization glue.
//
// When embedding a .NET NativeAOT static library into a native iOS app,
// the runtime must be initialized before calling any managed export
// (e.g., before Marshal.PtrToStringUTF8, async state machines, etc.).
//
// We do that here once, at dylib load time.

static bool s_dotnet_initialized = false;

static void dotnet_try_initialize(void) {
  if (s_dotnet_initialized) return;
  // With the Framework/XCFramework (shared library) approach, the runtime
  // startup is handled by the published dylib; no manual initialization needed.
  s_dotnet_initialized = true;
}

// Explicit entry point called from Swift before any managed export.
void mobile_engine_runtime_initialize(void) {
  dotnet_try_initialize();
}

