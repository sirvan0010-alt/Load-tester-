using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MailLoadTester;

/// <summary>
/// Zjištění síťových adaptérů a odhad, který Windows použije pro odchozí spojení.
/// Aplikace neváže SMTP na konkrétní NIC – používá se výchozí směrování OS.
/// </summary>
public static class NetworkAdapterInfo
{
    public sealed record AdapterSummary(
        string DisplayName,
        string Description,
        string IPv4,
        bool IsLikelyDefault,
        bool IsUp,
        long? SpeedBps,
        string TypeName);

    /// <summary>Krátký text pro status bar / label.</summary>
    public static string GetStatusLine()
    {
        try
        {
            var list = GetAdapters();
            var preferred = list.FirstOrDefault(a => a.IsLikelyDefault)
                            ?? list.FirstOrDefault(a => a.IsUp && !string.IsNullOrEmpty(a.IPv4));
            if (preferred is null)
                return "Síť: žádný aktivní adaptér s IPv4";
            var speed = preferred.SpeedBps is > 0
                ? $" · {FormatSpeed(preferred.SpeedBps.Value)}"
                : "";
            return $"Síť: {preferred.DisplayName} ({preferred.IPv4}){speed}";
        }
        catch (Exception ex)
        {
            return "Síť: nelze zjistit (" + ex.Message + ")";
        }
    }

    /// <summary>Podrobný popis pro ToolTip včetně návodu na změnu adaptéru ve Windows.</summary>
    public static string GetHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("SÍŤOVÝ ADAPTÉR (informativní)");
        sb.AppendLine("MailLoadTester neumožňuje přepnout kartu uvnitř aplikace.");
        sb.AppendLine("SMTP jde přes výchozí směrování Windows (stejně jako prohlížeč).");
        sb.AppendLine();
        sb.AppendLine("Jak Windows vybírá adaptér:");
        sb.AppendLine("• Použije se rozhraní s výchozí bránou (0.0.0.0) a nejnižší metrikou.");
        sb.AppendLine("• Ethernet má často nižší metriku než Wi‑Fi → má přednost.");
        sb.AppendLine("• USB/externí Ethernet se chová jako další síťová karta.");
        sb.AppendLine("• VPN může vytvořit vlastní výchozí trasu a „převzít“ provoz.");
        sb.AppendLine();
        sb.AppendLine("Jak zvolit JINÝ adaptér (když to nejde v aplikaci):");
        sb.AppendLine("1) Nastavení → Síť a Internet → Pokročilá nastavení sítě");
        sb.AppendLine("   → Další možnosti adaptéru (nebo ncpa.cpl)");
        sb.AppendLine("2) U NECHTĚNÉ karty: pravý klik → Zakázat");
        sb.AppendLine("   (nebo Vlastnosti → IPv4 → Upřesnit → zrušit „Automatická metrika“");
        sb.AppendLine("    a nastavit vyšší číslo metriky = nižší priorita)");
        sb.AppendLine("3) U POŽADOVANÉ karty nechte bránu a nižší metriku.");
        sb.AppendLine("4) Ověření v cmd:  route print -4");
        sb.AppendLine("   Řádek „0.0.0.0“ ukazuje, které rozhraní je výchozí.");
        sb.AppendLine();
        sb.AppendLine("Detekované adaptéry:");
        try
        {
            foreach (var a in GetAdapters())
            {
                var mark = a.IsLikelyDefault ? " ← pravděpodobně výchozí" : "";
                var up = a.IsUp ? "UP" : "down";
                sb.AppendLine($"• [{up}] {a.DisplayName} | {a.IPv4} | {a.TypeName}{mark}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("(seznam nedostupný: " + ex.Message + ")");
        }
        return sb.ToString();
    }

    public static IReadOnlyList<AdapterSummary> GetAdapters()
    {
        var result = new List<AdapterSummary>();
        var defaultIndex = TryGetDefaultInterfaceIndex();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            // Vynechat virtuální bez IP (často virtualbox host-only atd. necháme, pokud mají IP)

            var props = ni.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork
                                     && !IPAddress.IsLoopback(u.Address));
            if (ipv4 is null && ni.OperationalStatus != OperationalStatus.Up)
                continue;

            var ipStr = ipv4?.Address.ToString() ?? "—";
            var isUp = ni.OperationalStatus == OperationalStatus.Up;
            var isDefault = false;
            if (defaultIndex is int idx)
            {
                try
                {
                    var stats = ni.GetIPStatistics();
                    // Interface index: use IPv4 properties
                    var v4 = props.GetIPv4Properties();
                    if (v4 != null && v4.Index == idx)
                        isDefault = true;
                }
                catch { /* ignore */ }
            }
            // Fallback: has gateway
            if (!isDefault && isUp && props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork
                    && !g.Address.Equals(IPAddress.Any)))
            {
                // Označíme kandidáty s branou; přesný default má Index shodu
                if (defaultIndex is null)
                    isDefault = result.All(r => !r.IsLikelyDefault);
            }

            long? speed = null;
            try
            {
                if (ni.Speed > 0) speed = ni.Speed;
            }
            catch { }

            result.Add(new AdapterSummary(
                string.IsNullOrWhiteSpace(ni.Name) ? ni.Description : ni.Name,
                ni.Description,
                ipStr,
                isDefault && isUp,
                isUp,
                speed,
                ni.NetworkInterfaceType.ToString()));
        }

        // Pokud žádný IsLikelyDefault, označ první UP s branou
        if (result.All(r => !r.IsLikelyDefault))
        {
            var candidate = result.FirstOrDefault(r => r.IsUp && r.IPv4 != "—");
            if (candidate is not null)
            {
                var i = result.IndexOf(candidate);
                result[i] = candidate with { IsLikelyDefault = true };
            }
        }

        return result
            .OrderByDescending(a => a.IsLikelyDefault)
            .ThenByDescending(a => a.IsUp)
            .ToList();
    }

    static int? TryGetDefaultInterfaceIndex()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var p = ni.GetIPProperties();
                var v4 = p.GetIPv4Properties();
                if (v4 is null) continue;
                if (p.GatewayAddresses.Any(g =>
                        g.Address.AddressFamily == AddressFamily.InterNetwork
                        && !g.Address.Equals(IPAddress.Any)
                        && !g.Address.Equals(IPAddress.None)))
                    return v4.Index;
            }
        }
        catch { }
        return null;
    }

    static string FormatSpeed(long bps)
    {
        if (bps >= 1_000_000_000) return $"{bps / 1_000_000_000.0:0.#} Gb/s";
        if (bps >= 1_000_000) return $"{bps / 1_000_000.0:0.#} Mb/s";
        if (bps >= 1_000) return $"{bps / 1_000.0:0.#} kb/s";
        return $"{bps} b/s";
    }
}
