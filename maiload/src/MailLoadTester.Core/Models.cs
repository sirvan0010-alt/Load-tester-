namespace MailLoadTester;

public sealed record MailTestOptions(
    string From,
    IReadOnlyList<string> Recipients,
    string SmtpHost,
    int Port,
    SmtpSecurity Security,
    bool UseAuthentication,
    string Username,
    string Password,
    int MessageCount,
    int IntervalMs,
    bool BatchMode,
    int BatchSize,
    int BatchPauseSeconds,
    int MaxConcurrency,
    string Subject,
    string Body,
    string DisplayName,
    bool RandomTestData,
    bool TestMode,
    string AllowedDomains,
    bool HtmlBody,
    IReadOnlyList<string> Attachments,
    IReadOnlyDictionary<string, string> CustomHeaders,
    bool IgnoreCertificateErrors,
    int MaxRetries,
    bool DryRun);

public enum SmtpSecurity { None, StartTls, ImplicitTls }

public sealed record MailTestResult(
    int Requested,
    int Sent,
    int Failed,
    TimeSpan Elapsed,
    string LastError,
    double AvgLatencyMs,
    double MinLatencyMs,
    double MaxLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double ThroughputPerSec,
    bool Cancelled = false,
    double ActiveThroughputPerSec = 0,
    int Retries = 0,
    int Smtp4xx = 0,
    int Smtp5xx = 0,
    int Timeouts = 0,
    int PoolConnections = 0);

public static class Validation
{
    public static bool IsValidEmail(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var addr = new System.Net.Mail.MailAddress(value);
            return addr.Address == value.Trim();
        }
        catch { return false; }
    }

    public static string[] ParseDomains(string raw) => raw
        .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.Trim().TrimStart('@').ToLowerInvariant())
        .Where(x => x.Length > 0)
        .Distinct()
        .ToArray();

    public static IReadOnlyDictionary<string, string> ParseHeaders(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return dict;
        foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            if (key.Length > 0) dict[key] = val;
        }
        return dict;
    }

    public static void Validate(MailTestOptions o)
    {
        if (!IsValidEmail(o.From))
            throw new ArgumentException("From musí být platný e-mailový formát.");
        if (o.Recipients.Count == 0 || o.Recipients.Any(x => !IsValidEmail(x)))
            throw new ArgumentException("Seznam příjemců obsahuje neplatný e-mail.");
        if (string.IsNullOrWhiteSpace(o.SmtpHost))
            throw new ArgumentException("SMTP server je povinný.");
        if (o.Port is < 1 or > 65535)
            throw new ArgumentException("Port musí být 1–65535.");
        if (o.MessageCount is < 1 or > 10000)
            throw new ArgumentException("Počet zpráv musí být 1–10000.");
        if (o.IntervalMs is < 0 or > 3_600_000)
            throw new ArgumentException("Interval je mimo povolený rozsah (0–3600000 ms).");
        if (o.UseAuthentication && string.IsNullOrWhiteSpace(o.Username))
            throw new ArgumentException("U SMTP autentizace je povinné uživatelské jméno.");
        if (o.UseAuthentication && string.IsNullOrEmpty(o.Password))
            throw new ArgumentException("U SMTP autentizace je povinné heslo.");
        if (o.MaxConcurrency is < 1 or > 20)
            throw new ArgumentException("Paralelismus musí být 1–20.");
        if (o.MaxRetries is < 0 or > 5)
            throw new ArgumentException("Počet opakování musí být 0–5.");
        if (o.Security == SmtpSecurity.ImplicitTls && o.Port != 465)
            throw new ArgumentException("Implicit TLS je podporováno na portu 465.");
        if (o.Security == SmtpSecurity.StartTls && o.Port == 465)
            throw new ArgumentException("Port 465 používá implicit TLS, nikoli STARTTLS.");
        foreach (var header in o.CustomHeaders)
        {
            if (!IsSafeCustomHeaderName(header.Key))
                throw new ArgumentException($"Nepovolená vlastní hlavička: {header.Key}");
            if (header.Value.Contains('\r') || header.Value.Contains('\n'))
                throw new ArgumentException($"Hodnota hlavičky {header.Key} obsahuje zakázaný nový řádek.");
        }
        if (o.BatchMode)
        {
            if (o.BatchSize is < 1 or > 1000)
                throw new ArgumentException("Velikost dávky musí být 1–1000.");
            if (o.BatchPauseSeconds is < 1 or > 86_400)
                throw new ArgumentException("Pauza mezi dávkami musí být 1–86400 sekund.");
        }
        if (o.TestMode)
        {
            var allowed = ParseDomains(o.AllowedDomains);
            if (allowed.Length == 0)
                throw new ArgumentException("V Test mode zadejte alespoň jednu povolenou testovací doménu.");
            if (o.Recipients.Any(r =>
            {
                var at = r.LastIndexOf('@');
                if (at < 0 || at >= r.Length - 1) return true;
                return !allowed.Contains(r[(at + 1)..].ToLowerInvariant());
            }))
                throw new ArgumentException("Test mode: některý příjemce není v Allowed domains.");
        }
        foreach (var path in o.Attachments)
        {
            if (!File.Exists(path))
                throw new ArgumentException($"Příloha neexistuje: {path}");
        }
        if (o.Subject.Length > 998)
            throw new ArgumentException("Předmět je příliš dlouhý (max 998 znaků).");
        if (o.Body.Length > 10_000_000)
            throw new ArgumentException("Tělo zprávy je příliš velké (max 10 MB).");
        if (o.DisplayName.Length > 200)
            throw new ArgumentException("Display name je příliš dlouhý (max 200 znaků).");
    }


    static bool IsSafeCustomHeaderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("X-", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var ch in name)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'))
                return false;
        }
        return true;
    }

    /// <summary>Mapuje běžné SMTP kódy na srozumitelný popis.</summary>
    public static string ExplainSmtpError(Exception ex)
    {
        if (ex is MailKit.Net.Smtp.SmtpCommandException sce)
        {
            var code = (int)sce.StatusCode;
            var hint = code switch
            {
                421 => "Služba dočasně nedostupná – zkuste později nebo snižte paralelismus.",
                450 => "Schránka dočasně nedostupná.",
                451 => "Chyba při zpracování – dočasná.",
                452 => "Nedostatek místa na serveru.",
                454 => "Dočasné selhání autentizace.",
                500 => "Syntaktická chyba příkazu.",
                501 => "Syntaktická chyba parametrů.",
                502 => "Příkaz není implementován.",
                503 => "Špatné pořadí příkazů.",
                504 => "Parametr není implementován.",
                535 => "Autentizace selhala (špatné jméno/heslo nebo metoda).",
                550 => "Schránka neexistuje nebo je odmítnuta.",
                551 => "Uživatel není lokální.",
                552 => "Překročena kvóta schránky.",
                553 => "Adresa příjemce je neplatná.",
                554 => "Transakce selhala (často policy/spam).",
                _ when code >= 400 && code < 500 => "Dočasná chyba (4xx) – lze opakovat.",
                _ when code >= 500 => "Trvalá chyba (5xx) – opakování nepomůže.",
                _ => ""
            };
            return string.IsNullOrEmpty(hint)
                ? $"SMTP {code}: {sce.Message}"
                : $"SMTP {code}: {sce.Message} — {hint}";
        }
        if (ex is MailKit.Security.AuthenticationException)
            return "Autentizace selhala: " + ex.Message;
        if (ex is MailKit.Net.Smtp.SmtpProtocolException)
            return "Chyba SMTP protokolu: " + ex.Message;
        if (ex is TimeoutException)
            return "Vypršel časový limit spojení se SMTP serverem.";
        if (ex is IOException)
            return "Síťová chyba: " + ex.Message;
        return ex.Message;
    }
}
