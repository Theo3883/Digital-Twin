using System.Text.Json.Serialization;
using DigitalTwin.Mobile.OCR.Models;

namespace DigitalTwin.Mobile.OCR;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
[JsonSerializable(typeof(EncryptedDocumentDescriptor))]
internal partial class OcrJsonContext : JsonSerializerContext;
