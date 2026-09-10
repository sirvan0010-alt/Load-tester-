using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailLoadTester;

public static class SmtpConnectivityTester
{
    public static async Task<string> TestAsync(MailTestOptions o, CancellationToken ct)
    {
        Validation.Validate(o);
        if (o.DryRun)
            return $"DRY-RUN: simulace připojení k {o.SmtpHost}:{o.Port} (Zabezpečení={o.Security}) — žádné reálné spojení.";

        using var client = new SmtpClient();
        client.Timeout = 10_000;
        if (o.IgnoreCertificateErrors)
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

        var socket = ToSocketOptions(o.Security);
        await client.ConnectAsync(o.SmtpHost, o.Port, socket, ct);

        var auth = "Autentizace netestována";
        if (o.UseAuthentication)
        {
            await client.AuthenticateAsync(o.Username, o.Password, ct);
            auth = "Autentizace OK";
        }

        await client.DisconnectAsync(true, ct);
        return $"SMTP spojení OK\r\nServer: {o.SmtpHost}:{o.Port}\r\nZabezpečení: {o.Security}\r\nIgnoreCertErrors: {o.IgnoreCertificateErrors}\r\n{auth}";
    }

    internal static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.ImplicitTls => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        _ => SecureSocketOptions.None
    };
}
