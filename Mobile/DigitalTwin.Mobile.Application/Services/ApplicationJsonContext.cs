using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Application.DTOs;

namespace DigitalTwin.Mobile.Application.Services;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
[JsonSerializable(typeof(CoachingAdviceDto))]
public partial class ApplicationJsonContext : JsonSerializerContext;
