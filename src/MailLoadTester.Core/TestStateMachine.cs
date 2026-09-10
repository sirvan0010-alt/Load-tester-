namespace MailLoadTester;

public enum TestPhase
{
    Idle = 0, Validating = 1, PreparingAttachments = 2,
    ConnectingSmtp = 3, Sending = 4, BatchPause = 5,
    Completed = 6, Cancelled = 7, Failed = 8
}

public enum MessageStep
{
    Queued, WaitingRateLimit, RentingConnection, BuildingMime,
    SmtpSend, Succeeded, FailedTransient, FailedFinal, Cancelled
}

public sealed class TestStateMachine
{
    private readonly object _sync = new();
    public TestPhase Phase { get; private set; } = TestPhase.Idle;
    public event Action<TestPhase, TestPhase, string>? PhaseChanged;

    public bool Transition(TestPhase next, string reason)
    {
        Action<TestPhase, TestPhase, string>? handler;
        TestPhase previous;
        lock (_sync)
        {
            previous = Phase;
            if (previous == next) return true;
            if (!IsAllowed(previous, next)) return false;
            Phase = next;
            handler = PhaseChanged;
        }
        handler?.Invoke(previous, next, reason);
        return true;
    }

    public void ForceFailure(string reason)
    {
        Action<TestPhase, TestPhase, string>? handler;
        TestPhase previous;
        lock (_sync)
        {
            previous = Phase;
            if (previous == TestPhase.Failed) return;
            Phase = TestPhase.Failed;
            handler = PhaseChanged;
        }
        handler?.Invoke(previous, TestPhase.Failed, reason);
    }

    public static bool IsAllowed(TestPhase from, TestPhase to)
    {
        if (to == TestPhase.Cancelled && from != TestPhase.Completed) return true;
        if (from == TestPhase.Failed || from == TestPhase.Cancelled || from == TestPhase.Completed)
            return false;
        return (from, to) switch
        {
            (TestPhase.Idle, TestPhase.Validating) => true,
            (TestPhase.Validating, TestPhase.PreparingAttachments) => true,
            (TestPhase.PreparingAttachments, TestPhase.ConnectingSmtp) => true,
            (TestPhase.PreparingAttachments, TestPhase.Sending) => true,
            (TestPhase.ConnectingSmtp, TestPhase.Sending) => true,
            (TestPhase.Sending, TestPhase.BatchPause) => true,
            (TestPhase.BatchPause, TestPhase.Sending) => true,
            (TestPhase.Sending, TestPhase.Completed) => true,
            (TestPhase.Sending, TestPhase.Cancelled) => true,
            (TestPhase.BatchPause, TestPhase.Cancelled) => true,
            (TestPhase.Validating, TestPhase.Failed) => true,
            (TestPhase.PreparingAttachments, TestPhase.Failed) => true,
            (TestPhase.ConnectingSmtp, TestPhase.Failed) => true,
            (TestPhase.Sending, TestPhase.Failed) => true,
            _ => false
        };
    }

    public static string CurrentText(TestPhase phase) => phase switch
    {
        TestPhase.Idle => "Připraveno",
        TestPhase.Validating => "Validuji nastavení",
        TestPhase.PreparingAttachments => "Připravuji přílohy",
        TestPhase.ConnectingSmtp => "Navazuji SMTP spojení",
        TestPhase.Sending => "Odesílám zprávy",
        TestPhase.BatchPause => "Probíhá pauza dávky",
        TestPhase.Completed => "Test dokončen",
        TestPhase.Cancelled => "Test zastaven",
        TestPhase.Failed => "Test selhal",
        _ => phase.ToString()
    };

    public static string NextText(TestPhase phase) => phase switch
    {
        TestPhase.Idle => "Validace",
        TestPhase.Validating => "Příprava příloh",
        TestPhase.PreparingAttachments => "SMTP spojení",
        TestPhase.ConnectingSmtp => "Odesílání zpráv",
        TestPhase.Sending => "Další zpráva / pauza dávky / souhrn",
        TestPhase.BatchPause => "Další dávka",
        TestPhase.Completed => "Nový test nebo úprava parametrů",
        TestPhase.Cancelled => "Nový test",
        TestPhase.Failed => "Opravit chybu a test zopakovat",
        _ => ""
    };

    public static string MessageText(MessageStep step) => step switch
    {
        MessageStep.Queued => "Fronta",
        MessageStep.WaitingRateLimit => "Rate limit",
        MessageStep.RentingConnection => "SMTP spojení",
        MessageStep.BuildingMime => "MIME",
        MessageStep.SmtpSend => "SMTP SEND",
        MessageStep.Succeeded => "OK",
        MessageStep.FailedTransient => "4xx / retry",
        MessageStep.FailedFinal => "FINÁLNÍ CHYBA",
        MessageStep.Cancelled => "Zrušeno",
        _ => step.ToString()
    };
}
