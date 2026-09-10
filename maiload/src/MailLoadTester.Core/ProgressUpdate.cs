namespace MailLoadTester;

/// <summary>Thread-safe progress snapshot for UI/CLI.</summary>
public readonly record struct ProgressUpdate(
    int Sent,
    int Failed,
    string Status,
    double? EtaSeconds,
    int Phase,
    string CurrentStep,
    string NextStep,
    TestPhase TestPhase = TestPhase.Idle,
    MessageStep? MessageStep = null,
    int? MessageIndex = null,
    int? WorkerId = null,
    int WorkerCount = 0);
