namespace MailLoadTester;

public static class RandomTestData
{
    private static readonly string[] Names = ["Test User", "QA Tester", "SMTP Test", "Load Test", "Mail QA"];
    private static readonly string[] Subjects = ["SMTP test", "Delivery test", "Load test message", "Mail pipeline test", "Automated QA message"];
    private static readonly string[] Bodies =
    [
        "Automaticky generovaná testovací zpráva. Slouží k ověření SMTP pipeline.",
        "Testovací zpráva MailLoadTester. Neobsahuje produkční data.",
        "SMTP delivery/load test – generated test payload.",
        "Automated mail test message. Test ID bude přidán při odeslání."
    ];

    public static (string Name, string Subject, string Body) Create(int id)
    {
        var r = Random.Shared;
        var token = r.Next(100000, 999999);
        return (
            $"{Names[r.Next(Names.Length)]} {token}",
            $"{Subjects[r.Next(Subjects.Length)]} #{id}",
            $"{Bodies[r.Next(Bodies.Length)]}\r\n\r\nTest ID: {id}\r\nRandom token: {token}\r\nUTC: {DateTime.UtcNow:O}"
        );
    }
}
