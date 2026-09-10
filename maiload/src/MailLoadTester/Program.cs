// =============================================================================
// MailLoadTester – vstupní bod aplikace (GUI / CLI)
//
// Jak sestavit EXE a autoinstalátor pro Windows (krok za krokem, česky):
//   viz soubor ve kořeni projektu:  JAK-SESTAVIT-A-INSTALOVAT.txt
// Skript instalátoru (Inno Setup):
//   installer\MailLoadTester.iss
//
// Stručně:
//   1) nainstalovat .NET 8 SDK
//   2) dotnet publish src\MailLoadTester -c Release -r win-x64 --self-contained true
//      -p:PublishSingleFile=true -o publish
//   3) v Inno Setup otevřít installer\MailLoadTester.iss → Build → Compile
// =============================================================================

namespace MailLoadTester;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--cli" || args[0] == "-c"))
        {
            try
            {
                var options = ParseCli(args);
                Validation.Validate(options);
                Console.WriteLine($"Starting CLI load test: {options.MessageCount} messages → {options.SmtpHost}:{options.Port}" +
                                  (options.DryRun ? " [DRY-RUN]" : ""));
                var progress = new Progress<ProgressUpdate>(p =>
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {p.Status}");
                    if (!string.IsNullOrEmpty(p.CurrentStep))
                        Console.WriteLine($"         → {p.CurrentStep} | dál: {p.NextStep}");
                });
                var result = new SmtpTestRunner().RunAsync(options, progress, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"Done. Sent={result.Sent} Failed={result.Failed} Elapsed={result.Elapsed} " +
                                  $"Avg={result.AvgLatencyMs:F1}ms p50={result.P50LatencyMs:F1} p95={result.P95LatencyMs:F1} p99={result.P99LatencyMs:F1} " +
                                  $"Throughput={result.ThroughputPerSec:F2}/s");
                Environment.Exit(result.Failed > 0 ? 1 : 0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                Environment.Exit(2);
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    static MailTestOptions ParseCli(string[] args)
    {
        string Get(string name, string def = "")
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return def;
        }
        bool Has(string name) => args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        var security = Get("--security", "starttls").ToLowerInvariant() switch
        {
            "none" => SmtpSecurity.None,
            "implicit" or "implicitls" or "ssl" => SmtpSecurity.ImplicitTls,
            _ => SmtpSecurity.StartTls
        };

        var recipients = Get("--to").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var attachments = Get("--attach").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new MailTestOptions(
            From: Get("--from"),
            Recipients: recipients,
            SmtpHost: Get("--host"),
            Port: int.TryParse(Get("--port", "587"), out var p) ? p : 587,
            Security: security,
            UseAuthentication: Has("--auth"),
            Username: Get("--user"),
            // Heslo: --pass, nebo proměnná prostředí MAILLOADTESTER_PASSWORD (bezpečnější)
            Password: !string.IsNullOrEmpty(Get("--pass"))
                ? Get("--pass")
                : (Environment.GetEnvironmentVariable("MAILLOADTESTER_PASSWORD") ?? ""),
            MessageCount: int.TryParse(Get("--count", "1"), out var c) ? c : 1,
            IntervalMs: int.TryParse(Get("--interval", "0"), out var iv) ? iv : 0,
            BatchMode: Has("--batch"),
            BatchSize: int.TryParse(Get("--batchsize", "10"), out var bs) ? bs : 10,
            BatchPauseSeconds: int.TryParse(Get("--batchpause", "30"), out var bp) ? bp : 30,
            MaxConcurrency: int.TryParse(Get("--parallel", "1"), out var par) ? par : 1,
            Subject: Get("--subject", "CLI SMTP test"),
            Body: Get("--body", "Automated CLI test message."),
            DisplayName: Get("--name", "MailLoadTester CLI"),
            RandomTestData: Has("--random"),
            TestMode: !Has("--no-testmode"),
            AllowedDomains: Get("--domains"),
            HtmlBody: Has("--html"),
            Attachments: attachments,
            CustomHeaders: Validation.ParseHeaders(Get("--headers")),
            IgnoreCertificateErrors: Has("--ignore-cert"),
            MaxRetries: int.TryParse(Get("--retries", "1"), out var r) ? r : 1,
            DryRun: Has("--dry-run"));
    }
}
