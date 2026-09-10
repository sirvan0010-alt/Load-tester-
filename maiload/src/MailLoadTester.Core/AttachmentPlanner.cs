using System.Diagnostics;

namespace MailLoadTester;

public sealed record AttachmentSource(string FileName, string FullPath, byte[]? PreloadedContent, long Length);

public sealed record AttachmentPlan(
    IReadOnlyList<AttachmentSource> Sources,
    long TotalBytes,
    long AvailableMemoryBytes,
    long PreloadBudgetBytes,
    bool Preloaded,
    string Description);

public static class AttachmentPlanner
{
    // Conservative policy: attachments may use at most 10% of memory reported as
    // available to the process, capped at 512 MiB. Keep a minimum 64 MiB budget so
    // small test files can still be preloaded on modest machines.
    private const long MaxPreloadBytes = 512L * 1024 * 1024;
    private const long MinPreloadBudget = 64L * 1024 * 1024;

    public static AttachmentPlan Prepare(IReadOnlyList<string> paths, int maxConcurrency = 1)
    {
        var files = new List<(string Name, string Path, long Length)>();
        long total = 0;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException($"Příloha neexistuje: {path}", path);
            if (info.Length < 0)
                throw new IOException($"Nelze zjistit velikost přílohy: {path}");

            checked { total += info.Length; }
            files.Add((info.Name, info.FullName, info.Length));
        }

        if (files.Count == 0)
            return new AttachmentPlan(Array.Empty<AttachmentSource>(), 0, GetAvailableMemory(), 0, true, "Bez příloh.");

        var available = GetAvailableMemory();
        var concurrency = Math.Clamp(maxConcurrency, 1, 20);
        var budget = Math.Min(MaxPreloadBytes, Math.Max(MinPreloadBudget, available / 10));

        // Preloaded byte[] data are shared between messages, but MIME encoding and
        // network buffers can multiply transient memory use with concurrency. Keep a
        // conservative headroom estimate instead of looking only at raw file size.
        var encodingFactor = 1.5; // MIME/base64 + temporary buffers (conservative estimate)
        var estimatedPeak = total * Math.Min(concurrency, 4) * encodingFactor;
        var preload = total <= budget && estimatedPeak <= available / 4;
        var sources = new List<AttachmentSource>(files.Count);

        if (preload)
        {
            foreach (var file in files)
            {
                sources.Add(new AttachmentSource(file.Name, file.Path, File.ReadAllBytes(file.Path), file.Length));
            }

            return new AttachmentPlan(sources, total, available, budget, true,
                $"Přílohy přednačteny do RAM: {FormatBytes(total)} / rozpočet {FormatBytes(budget)}, " +
                $"odhadovaný špičkový footprint {FormatBytes((long)Math.Min(long.MaxValue, estimatedPeak))}, dostupná RAM {FormatBytes(available)}.");
        }

        foreach (var file in files)
            sources.Add(new AttachmentSource(file.Name, file.Path, null, file.Length));

        return new AttachmentPlan(sources, total, available, budget, false,
            $"Přílohy budou čteny ze souborů průběžně: {FormatBytes(total)} celkem, " +
            $"odhadovaný špičkový footprint {FormatBytes((long)Math.Min(long.MaxValue, estimatedPeak))}, " +
            $"dostupná RAM {FormatBytes(available)}, rozpočet pro preload {FormatBytes(budget)}.");
    }

    private static long GetAvailableMemory()
    {
        try
        {
            var gc = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (gc > 0) return gc;
        }
        catch { }

        try
        {
            var process = Process.GetCurrentProcess();
            var fallback = Math.Max(128L * 1024 * 1024, process.WorkingSet64 * 4);
            return fallback;
        }
        catch
        {
            return 512L * 1024 * 1024;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KiB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:F1} MiB";
        return $"{bytes / 1024d / 1024d / 1024d:F2} GiB";
    }
}
