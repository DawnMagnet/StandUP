using System.Text.Json.Serialization;

namespace StandUP.Services;

/// <summary>Compile-time JSON metadata, so persistence works without reflection (required for Native AOT).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(DailyStats))]
internal sealed partial class StandUpJson : JsonSerializerContext;
