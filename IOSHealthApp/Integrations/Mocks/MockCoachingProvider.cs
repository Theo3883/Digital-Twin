using IOSHealthApp.Domain.Interfaces;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Integrations.Mocks;

public class MockCoachingProvider : ICoachingProvider
{
    private static readonly string[] AdvicePool =
    [
        "Your heart rate has been stable today. Keep up the good work! A short evening walk could help maintain this trend.",
        "You've been less active than usual. Try a 15-minute walk after lunch to boost circulation.",
        "Great step count today! Remember to stay hydrated, especially with the current temperature.",
        "Your resting heart rate is slightly elevated. Consider a relaxation exercise before bed tonight.",
        "Excellent recovery pattern detected. Your cardiovascular fitness is improving week over week.",
        "Air quality is moderate outside. Consider indoor exercises today and keep windows closed.",
        "Your sleep-to-activity ratio looks balanced. Maintain this routine for optimal heart health."
    ];

    private readonly Random _random = new();

    public Task<string> GetAdviceAsync(PatientProfile profile)
    {
        var latestHr = profile.RecentVitals
            .Where(v => v.Type == Domain.Enums.VitalSignType.HeartRate)
            .OrderByDescending(v => v.Timestamp)
            .FirstOrDefault();

        var index = latestHr switch
        {
            { Value: > 90 } => 3,
            { Value: < 65 } => 4,
            _ => _random.Next(AdvicePool.Length)
        };

        return Task.FromResult(AdvicePool[index]);
    }
}
