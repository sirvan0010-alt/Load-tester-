using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MailLoadTester;

public static class WebhookNotifier
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task NotifyAsync(string url, MailTestResult result, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new
        {
            app = "MailLoadTester",
            version = AppVersion.Current,
            timestamp = DateTime.UtcNow,
            result.Requested,
            result.Sent,
            result.Failed,
            result.Cancelled,
            result.Elapsed,
            result.ThroughputPerSec,
            result.AvgLatencyMs,
            result.P95LatencyMs,
            result.Retries,
            result.Smtp4xx,
            result.Smtp5xx,
            result.LastError
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var response = await Client.PostAsync(url, content, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Best effort — don't fail the test if webhook fails */ }
    }
}
