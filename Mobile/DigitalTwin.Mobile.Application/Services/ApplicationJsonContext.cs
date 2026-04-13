using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Application.DTOs;

namespace DigitalTwin.Mobile.Application.Services;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
[JsonSerializable(typeof(CoachingAdviceDto))]
[JsonSerializable(typeof(CoachingSectionDto))]
[JsonSerializable(typeof(List<CoachingSectionDto>))]
[JsonSerializable(typeof(CoachingActionDto))]
[JsonSerializable(typeof(List<CoachingActionDto>))]
public partial class ApplicationJsonContext : JsonSerializerContext;
