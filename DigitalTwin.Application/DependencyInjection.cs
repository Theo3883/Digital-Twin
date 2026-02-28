// ──────────────────────────────────────────────────────────────────────────────
// All DI registrations are now handled centrally by DigitalTwin.Composition.
// See Composition/DependencyInjection.cs → AddDigitalTwinForWebApi / AddDigitalTwinForMaui.
//
// This file is intentionally left empty. Do NOT add service registrations here;
// add them in the appropriate Composition method instead.
// ──────────────────────────────────────────────────────────────────────────────

namespace DigitalTwin.Application;

/// <summary>
/// Kept for assembly marker / future per-layer helpers.
/// All DI wiring lives in <c>DigitalTwin.Composition.DependencyInjection</c>.
/// </summary>
public static class DependencyInjection { }
