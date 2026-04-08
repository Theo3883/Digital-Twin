namespace DigitalTwin.Components.Shared;

// ── FormDialog models ─────────────────────────────────────────────────────────

public enum FormFieldType { Text, TextArea, Select }

/// <summary>
/// Describes one field rendered by <see cref="FormDialog"/>.
/// </summary>
public record FormFieldDef(
    string Key,
    string Label,
    string Placeholder = "",
    FormFieldType Type = FormFieldType.Text,
    string? InitialValue = null,
    bool Required = false,
    IReadOnlyList<string>? Options = null
);

// ── ActionMenuDialog models ───────────────────────────────────────────────────

/// <summary>
/// One action item inside <see cref="ActionMenuDialog"/>.
/// </summary>
public record ActionItem(
    string Key,
    string Label,
    string Icon,
    string TextColor = "var(--text-main)"
);
