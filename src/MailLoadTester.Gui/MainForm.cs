using System.Text;
using System.Text.Json;

namespace MailLoadTester.Gui;

public sealed class MainForm : Form
{
    // === Controls ===
    private Button btnStart = null!, btnStop = null!, btnSaveProfile = null!, btnLoadProfile = null!;
    private TabControl tabs = null!;
    private ToolTip toolTip = null!;

    // SMTP tab
    private TextBox txtHost = null!;
    private NumericUpDown numPort = null!;
    private ComboBox cmbSecurity = null!;
    private CheckBox chkIgnoreCert = null!;
    private CheckBox chkAuth = null!;
    private ComboBox cmbAuthMethod = null!;
    private TextBox txtUser = null!, txtPass = null!;
    private Button btnTestConnection = null!;
    private TextBox txtSourceIp = null!;
    private ComboBox cmbLocalIp = null!;
    private TextBox txtIpv6Prefix = null!;
    private NumericUpDown numIpv6PrefixLen = null!;
    private TextBox txtIpv4Rotation = null!;
    private CheckBox chkIpv4RotationRandom = null!;
    private TextBox txtClientCert = null!, txtClientCertPass = null!;
    private NumericUpDown numConnectTimeout = null!, numReadTimeout = null!;

    // Message tab
    private TextBox txtFrom = null!, txtTo = null!, txtCc = null!, txtBcc = null!;
    private TextBox txtDisplayName = null!, txtSubject = null!, txtBody = null!;
    private CheckBox chkHtmlBody = null!, chkRandomData = null!, chkSmtpUtf8 = null!;
    private CheckBox chkBogusData = null!, chkRandomHtml = null!, chkRandomAttachments = null!, chkVaryMessage = null!;
    private NumericUpDown numMaxRandomAttachments = null!, numRandomAttachmentSizeMb = null!;
    private Label lblRandomAttachmentSafety = null!;
    private ListBox lstAttachments = null!, lstInlineAttachments = null!;
    private TextBox txtHeaders = null!;
    private TextBox txtEmlTemplate = null!;
    private Button btnEmlBrowse = null!;

    // Test tab
    private NumericUpDown numCount = null!, numConcurrency = null!, numInterval = null!, numRetries = null!;
    private CheckBox chkBatchMode = null!;
    private NumericUpDown numBatchSize = null!, numBatchPause = null!;
    private CheckBox chkDryRun = null!, chkTestMode = null!;
    private TextBox txtAllowedDomains = null!;

    // Proxy tab
    private CheckBox chkSocks5 = null!;
    private TextBox txtProxyHost = null!;
    private NumericUpDown numProxyPort = null!;
    private TextBox txtProxyUser = null!, txtProxyPass = null!;
    private TextBox txtProxyList = null!;
    // Options without dedicated controls (persist via profile / defaults)
    private int _idleHealthCheckSeconds = 30;
    private int _circuitWindowSize = 100;
    private double _circuitFailurePercent = 90.0;
    private CheckBox chkProxyListRandom = null!;
    private NumericUpDown numProxyBanMinutes = null!;

    // Tempo a ochrana tab
    private ComboBox cmbProviderPreset = null!;
    private CheckBox chkJitter = null!, chkBurst = null!, chkBackoff = null!, chkPerRecipient = null!;
    private CheckBox chkTimeWindow = null!, chkWarmup = null!, chkGreylist = null!, chkRbl = null!, chkCollectObserved = null!;
    private NumericUpDown numJitterPercent = null!, numBurstSize = null!, numBurstPause = null!;
    private NumericUpDown numBackoffAfter = null!, numBackoffMult = null!, numMaxInterval = null!;
    private NumericUpDown numMaxPerRecipient = null!, numPerRecipientWindow = null!;
    private NumericUpDown numWindowFrom = null!, numWindowTo = null!;
    private NumericUpDown numGreylistMinutes = null!, numGreylistRetries = null!;
    private TextBox txtWarmupPhases = null!;
    private Label lblPaceStatus = null!, lblEffectiveInterval = null!;
    private DataGridView gridObserved = null!, gridKnowledge = null!;
    private Button btnCheckRbl = null!;
    private Label lblRblResult = null!;

    // Cesta odesílání (pipeline) – začíná zašedlá, zbarvuje se podle průběhu
    private FlowLayoutPanel pnlDeliveryPath = null!;
    private readonly Dictionary<DeliveryStepKind, Label> _pathLabels = new();
    private readonly DeliveryStepKind[] _pathOrder =
    [
        DeliveryStepKind.DnsMxLookup,
        DeliveryStepKind.TcpConnect,
        DeliveryStepKind.Ehlo,
        DeliveryStepKind.StartTls,
        DeliveryStepKind.Auth,
        DeliveryStepKind.MailFrom,
        DeliveryStepKind.RcptTo,
        DeliveryStepKind.Data,
        DeliveryStepKind.Quit
    ];

    // Advanced tab
    private CheckBox chkDirectMx = null!, chkPreWarm = null!, chkAdaptive = null!, chkCircuit = null!;
    private NumericUpDown numCircuitThreshold = null!;
    private CheckBox chkDashboard = null!;
    private NumericUpDown numDashboardPort = null!;
    private CheckBox chkAutoRestart = null!;
    private NumericUpDown numAutoRestartAttempts = null!;
    private CheckBox chkSessionLog = null!;
    private TextBox txtSessionLogPath = null!;

    // Síť / integrace tab
    private TextBox txtWebhookUrl = null!;
    private NumericUpDown numBandwidthLimit = null!;
    private ComboBox cmbIpVersion = null!;

    // Bottom panels
    private RichTextBox rtbLog = null!;
    private SmtpLogTab smtpLogTab = null!;
    private FlowLayoutPanel pnlWorkers = null!;
    private Label[]? _workerLabels;
    private Label[] _checklistLabels = new Label[6];
    private readonly string[] _checklistTexts = ["Fronta", "Rate limit", "SMTP", "MIME", "SEND", "OK"];

    // Status
    private StatusStrip statusStrip = null!;
    private ToolStripProgressBar progressBar = null!;
    private ToolStripStatusLabel lblStatus = null!, lblEta = null!, lblSentFailed = null!;

    // Runtime
    private CancellationTokenSource? _cts;
    private SmtpTestRunner? _runner;
    private bool _isRunning;
    private TaskCompletionSource<bool>? _runCompletion;
    private CancellationTokenSource? _testConnectionCts;
    private TaskCompletionSource<bool>? _testConnectionCompletion;
    private bool _closeApproved;
    private DateTime _logThrottleWindow = DateTime.MinValue;
    private int _logThrottleCount;
    private int _logSuppressed;
    private DateTime _observedThrottle = DateTime.MinValue;

    public MainForm()
    {
        Text = $"MailLoadTester {MailLoadTester.AppVersion.Current} — Maximum Brutal Edition";
        Size = new Size(1280, 920);
        MinimumSize = new Size(1000, 750);
        StartPosition = FormStartPosition.CenterScreen;
        InitializeComponent();
        WireEvents();
        SetDefaults();
        FormClosing += MainForm_FormClosing;
    }

    /// <summary>
    /// Closing the window while a test is running used to do nothing special: the
    /// background Task.Run(... RunAsync ...) kept going with no cancellation, and
    /// every subsequent progress/log callback that reached UpdateProgress/AppendLog
    /// would call Invoke() on a Control whose handle was being torn down — throwing
    /// ObjectDisposedException/InvalidOperationException *inside* SmtpTestRunner's
    /// Report() call. Since the per-message send loop's outermost catch treats any
    /// exception as a message failure, this didn't crash the app — it silently made
    /// every remaining message in the run "fail" for an unrelated GUI-teardown
    /// reason, burned through retries, and kept the background loop alive until
    /// MessageCount was exhausted, never actually stopping. Now: request
    /// cancellation, block the close until the run has genuinely wound down, then
    /// let it close.
    /// </summary>
    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closeApproved)
            return;

        var runActive = _isRunning;
        var connectionTestActive = _testConnectionCts != null;
        if (!runActive && !connectionTestActive)
            return;

        e.Cancel = true;
        lblStatus.Text = "Zavírám — čekám na ukončení běžící operace…";
        _cts?.Cancel();
        _testConnectionCts?.Cancel();

        if (runActive)
        {
            var completion = _runCompletion;
            if (completion != null)
            {
                try { await completion.Task.ConfigureAwait(true); }
                catch { /* the run's own try/catch in OnStartAsync already handled/reported this */ }
            }
        }

        if (connectionTestActive)
        {
            var completion = _testConnectionCompletion;
            if (completion != null)
            {
                try { await completion.Task.ConfigureAwait(true); }
                catch { /* TestConnection owns its cancellation/error handling. */ }
            }
        }

        _closeApproved = true;
        if (!IsDisposed)
            Close();
    }

    private void InitializeComponent()
    {
        toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };

        // === Top button bar ===
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8, 6, 8, 6) };
        btnStart = new Button
        {
            Text = "▶ START", Width = 120, Height = 36, Left = 8, Top = 6,
            BackColor = Color.FromArgb(34, 139, 34), ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnStart.FlatAppearance.BorderSize = 0;
        btnStop = new Button
        {
            Text = "■ STOP", Width = 120, Height = 36, Left = 140, Top = 6,
            BackColor = Color.FromArgb(178, 34, 34), ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Enabled = false, Cursor = Cursors.Hand
        };
        btnStop.FlatAppearance.BorderSize = 0;
        topPanel.Controls.Add(btnStart);
        topPanel.Controls.Add(btnStop);
        btnSaveProfile = new Button
        {
            Text = "💾 Uložit profil", Width = 130, Height = 36, Left = 280, Top = 6,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnLoadProfile = new Button
        {
            Text = "📂 Načíst profil", Width = 130, Height = 36, Left = 420, Top = 6,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        toolTip.SetToolTip(btnSaveProfile, "Uloží aktuální nastavení testu do JSON (bez hesel).");
        toolTip.SetToolTip(btnLoadProfile, "Načte profil testu z JSON souboru.");
        topPanel.Controls.Add(btnSaveProfile);
        topPanel.Controls.Add(btnLoadProfile);
        Controls.Add(topPanel);

        // === TabControl ===
        tabs = new TabControl { Dock = DockStyle.Top, Height = 480 };

        // --- Tab: SMTP Server ---
        var tabSmtp = new TabPage("SMTP Server") { Padding = new Padding(12) };
        var tblSmtp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tblSmtp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblSmtp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var gbConn = new GroupBox { Text = "Připojení", Dock = DockStyle.Fill };
        var flConn = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtHost = AddLabeledText(flConn, "SMTP Host:", "Např. smtp.gmail.com nebo IP adresa serveru.");
        numPort = AddLabeledNumeric(flConn, "Port:", 1, 65535, 587, "25=plain, 587=STARTTLS, 465=Implicit TLS");
        cmbSecurity = AddLabeledCombo(flConn, "Zabezpečení:", ["None", "StartTls", "ImplicitTls"], "StartTls", "None=bez šifrování, StartTLS=TLS po EHLO, ImplicitTLS=SSL hned po connectu.");
        chkIgnoreCert = AddCheckBox(flConn, "Ignorovat chyby certifikátu", false, "Pouze pro testování – nepoužívej v produkci!");
        txtSourceIp = AddLabeledText(flConn, "Source IP (volitelné):", "Váže SMTP socket na konkrétní lokální IP. Můžeš vybrat ze seznamu níže.");
        var lblLocalIp = new Label { Text = "Lokální IPv4 (výběr):", AutoSize = true, Margin = new Padding(0, 6, 0, 2) };
        flConn.Controls.Add(lblLocalIp);
        cmbLocalIp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        cmbLocalIp.Items.Add("(automaticky – systém)");
        try
        {
            foreach (var ip in NetworkAdapterInfo.GetLocalIPv4Addresses())
                cmbLocalIp.Items.Add(ip);
        }
        catch { /* ignore */ }
        cmbLocalIp.SelectedIndex = 0;
        cmbLocalIp.SelectedIndexChanged += (s, e) =>
        {
            if (cmbLocalIp.SelectedIndex > 0)
                txtSourceIp.Text = cmbLocalIp.SelectedItem?.ToString() ?? "";
            else if (cmbLocalIp.SelectedIndex == 0)
                txtSourceIp.Text = "";
        };
        toolTip.SetToolTip(cmbLocalIp, "Vybere lokální IPv4 adresu pro IP binding (Source IP).");
        flConn.Controls.Add(cmbLocalIp);
        txtIpv6Prefix = AddLabeledText(flConn, "IPv6 prefix (rotace):",
            "Např. 2001:db8:85a3:0:: — každé nové SMTP spojení dostane náhodnou adresu z /N. OS musí prefix routovat. Prázdné = vypnuto. Má přednost před Source IP.");
        numIpv6PrefixLen = AddLabeledNumeric(flConn, "IPv6 prefix length:", 1, 128, 64,
            "Délka prefixu v bitech (běžně 64).");
        txtIpv4Rotation = AddLabeledText(flConn, "IPv4 rotace (seznam/CIDR):",
            "Příklady: 192.0.2.10,192.0.2.11  nebo  192.0.2.0/28. Každé NOVÉ SMTP spojení použije další adresu. Musí existovat na síťové kartě. Prázdné = vypnuto. Priorita: IPv6 rotace > IPv4 rotace > Source IP.");
        chkIpv4RotationRandom = AddCheckBox(flConn, "IPv4 rotace náhodně (ne round-robin)", false,
            "Zapnuto = náhodná adresa z poolu. Vypnuto = postupně dokola.");
        cmbIpVersion = AddLabeledCombo(flConn, "IP verze:", ["Any", "IPv4Only", "IPv6Only", "DualStack"], "Any", "Výběr IP verze pro spojení.");
        gbConn.Controls.Add(flConn);
        tblSmtp.Controls.Add(gbConn, 0, 0);

        var gbAuth = new GroupBox { Text = "Autentizace & Certifikáty", Dock = DockStyle.Fill };
        var flAuth = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkAuth = AddCheckBox(flAuth, "Použít autentizaci", false, "Zaškrtni, pokud server vyžaduje jméno a heslo.");
        cmbAuthMethod = AddLabeledCombo(flAuth, "Auth metoda:", ["Auto", "Plain", "Login", "CramMd5", "ScramSha1", "Ntlm", "OAuth2"], "Auto", "Výběr SMTP autentizační metody. Auto zvolí nejlepší podle serveru. OAuth2: do pole Heslo vlož access token (ne refresh token).");
        txtUser = AddLabeledText(flAuth, "Uživatel:", "SMTP username (často celý e-mail).");
        txtPass = AddLabeledText(flAuth, "Heslo:", "SMTP heslo, app-password, nebo při OAuth2 access token.");
        txtPass.PasswordChar = '●';
        txtClientCert = AddLabeledText(flAuth, "Client cert (.pfx):", "Cesta k klientskému certifikátu pro mTLS.");
        txtClientCertPass = AddLabeledText(flAuth, "Heslo certifikátu:", "Heslo k .pfx souboru (pokud je zašifrovaný).");
        txtClientCertPass.PasswordChar = '●';
        numConnectTimeout = AddLabeledNumeric(flAuth, "Connect timeout (ms):", 1000, 300000, 20000, "Max čas na navázání TCP spojení.");
        numReadTimeout = AddLabeledNumeric(flAuth, "Read timeout (ms):", 1000, 300000, 20000, "Max čas na čekání na odpověď serveru.");
        btnTestConnection = new Button { Text = "🖧 Otestovat připojení", Width = 180, Height = 32, Margin = new Padding(0, 8, 0, 0) };
        flAuth.Controls.Add(btnTestConnection);
        toolTip.SetToolTip(btnTestConnection, "Ověří spojení k serveru bez odeslání zprávy.");
        gbAuth.Controls.Add(flAuth);
        tblSmtp.Controls.Add(gbAuth, 1, 0);

        tabSmtp.Controls.Add(tblSmtp);
        tabs.TabPages.Add(tabSmtp);

        // --- Tab: Zpráva ---
        var tabMsg = new TabPage("Zpráva") { Padding = new Padding(12) };
        var tblMsg = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tblMsg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblMsg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var gbAddr = new GroupBox { Text = "Adresy", Dock = DockStyle.Fill };
        var flAddr = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtFrom = AddLabeledText(flAddr, "Od:", "Odesílatel (např. test@example.com).");
        txtTo = AddLabeledText(flAddr, "Komu:", "Příjemci oddělení čárkou nebo novým řádkem.");
        txtTo.Multiline = true; txtTo.Height = 50;
        txtCc = AddLabeledText(flAddr, "CC:", "Kopie — oddělené čárkou.");
        txtBcc = AddLabeledText(flAddr, "BCC:", "Skryté kopie — oddělené čárkou.");
        txtDisplayName = AddLabeledText(flAddr, "Zobrazované jméno:", "Jméno, které se zobrazí v mail klientu.");
        gbAddr.Controls.Add(flAddr);
        tblMsg.Controls.Add(gbAddr, 0, 0);

        var gbContent = new GroupBox { Text = "Obsah & Šablona", Dock = DockStyle.Fill };
        var flContent = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtSubject = AddLabeledText(flContent, "Předmět:",
            "Předmět e-mailu. Dynamické tagy: {TIMESTAMP}, {GUID}, {RANDOM_WORD}, {RANDOM_WORD:12}, {TEST_ID} – každá zpráva bude unikátní.");
        var lblBody = new Label { Text = "Tělo zprávy:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        flContent.Controls.Add(lblBody);
        txtBody = new TextBox { Multiline = true, Height = 80, Width = 360, ScrollBars = ScrollBars.Vertical };
        flContent.Controls.Add(txtBody);
        toolTip.SetToolTip(txtBody, "Text e-mailu. HTML: zaškrtni HTML tělo. Inline obrázky: {{cid:nazev}}. Dynamické tagy: {TIMESTAMP}, {GUID}, {RANDOM_WORD}, {RANDOM_WORD:12}, {TEST_ID}.");
        chkHtmlBody = AddCheckBox(flContent, "HTML tělo", false, "Zpráva bude odeslána jako text/html.");
        chkRandomData = AddCheckBox(flContent, "Náhodná testovací data", false, "Automaticky vygeneruje náhodný předmět, tělo a jméno.");
        chkBogusData = AddCheckBox(flContent, "Realistická data (Bogus)", false,
            "Použije realistická jména, názvy produktů a text. Nemění From.");
        chkRandomHtml = AddCheckBox(flContent, "Náhodné HTML tělo", false,
            "Každá zpráva dostane synteticky generované HTML tělo.");
        chkRandomAttachments = AddCheckBox(flContent, "Náhodné přílohy", false,
            "Ke každé zprávě přidá 1 až zvolený počet testovacích souborů. Velikost lze nastavit; bezpečnost RAM se kontroluje před startem.");
        var pnlRandomAttachments = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        pnlRandomAttachments.Controls.Add(new Label { Text = "Max. náhodných příloh:", AutoSize = true, Padding = new Padding(0, 5, 4, 0) });
        numMaxRandomAttachments = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 2, Width = 55 };
        pnlRandomAttachments.Controls.Add(numMaxRandomAttachments);
        pnlRandomAttachments.Controls.Add(new Label { Text = "Velikost / příloha (MB):", AutoSize = true, Padding = new Padding(12, 5, 4, 0) });
        numRandomAttachmentSizeMb = new NumericUpDown { Minimum = 0, Maximum = 128, Value = 0, DecimalPlaces = 0, Width = 70 };
        pnlRandomAttachments.Controls.Add(numRandomAttachmentSizeMb);
        flContent.Controls.Add(pnlRandomAttachments);
        lblRandomAttachmentSafety = new Label { AutoSize = false, Width = 390, Height = 62, Text = "Bezpečnost příloh: Auto – výpočet podle RAM, počtu příloh a paralelismu.", Margin = new Padding(0, 2, 0, 6) };
        flContent.Controls.Add(lblRandomAttachmentSafety);
        toolTip.SetToolTip(numRandomAttachmentSizeMb, "0 = Auto. Program před startem odhadne špičku: velikost × počet příloh × paralelní workery + raw data + MIME/base64 režie. Pokud odhad překročí bezpečný rozpočet dostupné RAM, test se před alokací zastaví. Před každou velkou generovanou přílohou se RAM ještě znovu ověří, protože dostupná RAM se může během testu změnit. Ruční hodnota je v MB, maximum 128 MB.");
        toolTip.SetToolTip(lblRandomAttachmentSafety, "Bezpečnostní odhad není přesný měřič celé RAM. Je záměrně konzervativní. Test se při nebezpečné kombinaci nespustí, takže vysoká hodnota sama o sobě PC nezasekne.");
        chkVaryMessage = AddCheckBox(flContent, "Variace Subject/Body u každé zprávy", false,
            "Přidá jedinečné ID/token ke každé zprávě i bez plného random režimu.");
        chkSmtpUtf8 = AddCheckBox(flContent, "SMTPUTF8 (Unicode adresy)", false, "Povolí Unicode v e-mailových adresách (RFC 6531).");
        var lblEml = new Label { Text = "EML šablona:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        flContent.Controls.Add(lblEml);
        var pnlEml = new Panel { Width = 360, Height = 26 };
        txtEmlTemplate = new TextBox { Width = 280, Height = 23, Left = 0, Top = 0 };
        btnEmlBrowse = new Button { Text = "…", Width = 30, Height = 23, Left = 282, Top = 0 };
        pnlEml.Controls.Add(txtEmlTemplate);
        pnlEml.Controls.Add(btnEmlBrowse);
        flContent.Controls.Add(pnlEml);
        toolTip.SetToolTip(txtEmlTemplate, "Načte předmět, tělo a přílohy ze souboru .eml místo ručního zadání.");
        gbContent.Controls.Add(flContent);
        tblMsg.Controls.Add(gbContent, 1, 0);

        var gbAttach = new GroupBox { Text = "Přílohy", Dock = DockStyle.Bottom, Height = 160 };
        var flAttach = new FlowLayoutPanel { Dock = DockStyle.Fill };
        lstAttachments = new ListBox { Width = 280, Height = 90 };
        flAttach.Controls.Add(lstAttachments);
        var btnAdd = new Button { Text = "Přidat…", Width = 80, Height = 26 };
        var btnRem = new Button { Text = "Odebrat", Width = 80, Height = 26 };
        flAttach.Controls.Add(btnAdd);
        flAttach.Controls.Add(btnRem);
        btnAdd.Click += (s, e) => { using var dlg = new OpenFileDialog { Multiselect = true }; if (dlg.ShowDialog() == DialogResult.OK) lstAttachments.Items.AddRange(dlg.FileNames); };
        btnRem.Click += (s, e) => { if (lstAttachments.SelectedIndex >= 0) lstAttachments.Items.RemoveAt(lstAttachments.SelectedIndex); };
        toolTip.SetToolTip(lstAttachments, "Seznam souborů připojených ke každé zprávě.");
        gbAttach.Controls.Add(flAttach);
        tabMsg.Controls.Add(gbAttach);
        gbAttach.Dock = DockStyle.Bottom;

        var gbInline = new GroupBox { Text = "Inline přílohy (HTML CID)", Dock = DockStyle.Bottom, Height = 100 };
        var flInline = new FlowLayoutPanel { Dock = DockStyle.Fill };
        lstInlineAttachments = new ListBox { Width = 280, Height = 60 };
        flInline.Controls.Add(lstInlineAttachments);
        var btnAddInline = new Button { Text = "Přidat…", Width = 80, Height = 26 };
        var btnRemInline = new Button { Text = "Odebrat", Width = 80, Height = 26 };
        flInline.Controls.Add(btnAddInline);
        flInline.Controls.Add(btnRemInline);
        btnAddInline.Click += (s, e) => { using var dlg = new OpenFileDialog { Multiselect = true, Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp" }; if (dlg.ShowDialog() == DialogResult.OK) lstInlineAttachments.Items.AddRange(dlg.FileNames); };
        btnRemInline.Click += (s, e) => { if (lstInlineAttachments.SelectedIndex >= 0) lstInlineAttachments.Items.RemoveAt(lstInlineAttachments.SelectedIndex); };
        toolTip.SetToolTip(lstInlineAttachments, "Obrázky vložené do HTML těla. V HTML použij {{cid:nazev_souboru_bez_pripony}}.");
        gbInline.Controls.Add(flInline);
        tabMsg.Controls.Add(gbInline);
        gbInline.Dock = DockStyle.Bottom;

        var gbHeaders = new GroupBox { Text = "Vlastní hlavičky (X-…)", Dock = DockStyle.Bottom, Height = 100 };
        txtHeaders = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9) };
        txtHeaders.Text = "X-Test-ID: 123";
        toolTip.SetToolTip(txtHeaders, "Jedna hlavička na řádek. Formát: X-Něco: hodnota");
        gbHeaders.Controls.Add(txtHeaders);
        tabMsg.Controls.Add(gbHeaders);
        gbHeaders.Dock = DockStyle.Bottom;

        tabMsg.Controls.Add(tblMsg);
        tblMsg.Dock = DockStyle.Fill;
        tabs.TabPages.Add(tabMsg);

        // --- Tab: Test ---
        var tabTest = new TabPage("Test") { Padding = new Padding(12) };
        var tblTest = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tblTest.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblTest.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var gbParams = new GroupBox { Text = "Parametry", Dock = DockStyle.Fill };
        var flParams = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        numCount = AddLabeledNumeric(flParams, "Počet zpráv:", 1, 10000, 10, "Kolik zpráv celkem odeslat (1–10000).");
        numConcurrency = AddLabeledNumeric(flParams, "Paralelismus:", 1, 20, 1, "Kolik zpráv se odesílá současně (1–20).");
        numInterval = AddLabeledNumeric(flParams, "Interval (ms):", 0, 3600000, 0, "GLOBÁLNÍ rozestup mezi odesláními (ne per-worker). 1000 ms ≈ max 1 msg/s i při 20 workerech.");
        numRetries = AddLabeledNumeric(flParams, "Retry:", 0, 5, 0, "Kolikrát zkusit znovu při dočasné chybě (0–5).");
        gbParams.Controls.Add(flParams);
        tblTest.Controls.Add(gbParams, 0, 0);

        var gbBatch = new GroupBox { Text = "Dávkový režim", Dock = DockStyle.Fill };
        var flBatch = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkBatchMode = AddCheckBox(flBatch, "Použít dávky", false, "Rozdělí zprávy do dávek s pauzou mezi nimi.");
        numBatchSize = AddLabeledNumeric(flBatch, "Velikost dávky:", 1, 1000, 10, "Kolik zpráv v jedné dávce.");
        numBatchPause = AddLabeledNumeric(flBatch, "Pauza (s):", 1, 86400, 1, "Kolik sekund počkat mezi dávkami.");
        gbBatch.Controls.Add(flBatch);
        tblTest.Controls.Add(gbBatch, 1, 0);

        tabTest.Controls.Add(tblTest);
        tblTest.Dock = DockStyle.Fill;

        var gbMode = new GroupBox { Text = "Režim", Dock = DockStyle.Bottom, Height = 110 };
        var flMode = new FlowLayoutPanel { Dock = DockStyle.Fill };
        chkDryRun = AddCheckBox(flMode, "Dry-run (bez reálného SMTP)", false, "Simuluje odesílání bez spojení k serveru. Pipeline cesty je v dry-run jen ilustrativní, ne skutečná SMTP komunikace.");
        chkTestMode = AddCheckBox(flMode, "Test mode (omezit domény)", false, "Povolí odesílat jen na vybrané domény.");
        txtAllowedDomains = AddLabeledText(flMode, "Povolené domény:", "Seznam domén oddělených čárkou (např. example.com,test.cz).");
        gbMode.Controls.Add(flMode);
        tabTest.Controls.Add(gbMode);
        tabs.TabPages.Add(tabTest);

        // --- Tab: Proxy ---
        var tabProxy = new TabPage("Proxy") { Padding = new Padding(12) };
        var flProxy = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkSocks5 = AddCheckBox(flProxy, "Použít SOCKS5 proxy (jedna)", false, "Jedna SOCKS5 proxy níže. Pro více serverů použij seznam rotace.");
        txtProxyList = AddLabeledText(flProxy, "Proxy seznam (rotace):",
            "Jeden záznam na řádek nebo středník. Příklady: socks5://127.0.0.1:1080  |  http://user:pass@proxy:8080  |  host:1080");
        // multiline
        txtProxyHost = AddLabeledText(flProxy, "Proxy host:", "SOCKS5 proxy host (např. 127.0.0.1).");
        numProxyPort = AddLabeledNumeric(flProxy, "Proxy port:", 1, 65535, 1080, "SOCKS5 proxy port (Tor používá 9050).");
        txtProxyUser = AddLabeledText(flProxy, "Proxy uživatel:", "Volitelné — pouze pokud proxy vyžaduje autentizaci.");
        txtProxyPass = AddLabeledText(flProxy, "Proxy heslo:", "Volitelné heslo pro SOCKS5 proxy.");
        txtProxyPass.PasswordChar = '●';
        tabProxy.Controls.Add(flProxy);
        tabs.TabPages.Add(tabProxy);

        // --- Tab: Pokročilé ---
        var tabAdv = new TabPage("Pokročilé") { Padding = new Padding(12) };
        var flAdv = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkDirectMx = AddCheckBox(flAdv, "Direct MX delivery", false, "Místo relay SMTP se zeptá DNS na MX záznam a mluví přímo s cílovým serverem.");
        chkPreWarm = AddCheckBox(flAdv, "Pre-warm spojení", false, "Před testem vytvoří a ověří všechna spojení v poolu — první dávka nečeká na handshake.");
        chkAdaptive = AddCheckBox(flAdv, "Adaptive concurrency", false, "Při návalu 4xx/timeout automaticky snižuje paralelismus, po chvíli zase zvedne.");
        chkCircuit = AddCheckBox(flAdv, "Circuit breaker", false, "Po N po sobě jdoucích selháních stejného typu přestane zkoušet a vrátí výsledek.");
        numCircuitThreshold = AddLabeledNumeric(flAdv, "Circuit breaker threshold:", 1, 50, 5, "Kolik po sobě jdoucích selhání otevře jistič.");
        chkDashboard = AddCheckBox(flAdv, "HTTP Dashboard", false, "Spustí mini webový server na localhost — otevři prohlížeč a sleduj live grafy.");
        numDashboardPort = AddLabeledNumeric(flAdv, "Dashboard port:", 1, 65535, 5000, "Port pro live dashboard (výchozí 5000).");
        chkAutoRestart = AddCheckBox(flAdv, "Auto-restart při selhání", false, "POZOR: znovu odešle CELÝ test (může doručit zprávy vícekrát). Defaultně vypnuto. Snižuje paralelismus po většinovém selhání.");
        numAutoRestartAttempts = AddLabeledNumeric(flAdv, "Max auto-restartů:", 1, 10, 3, "Kolikrát zkusit restart při selhání.");
        chkSessionLog = AddCheckBox(flAdv, "SMTP session log", false, "Uloží kompletní SMTP konverzaci (C:/S:) do .txt pro debug.");
        txtSessionLogPath = AddLabeledText(flAdv, "Session log soubor:", "Cesta k .txt souboru pro SMTP log.");
        tabAdv.Controls.Add(flAdv);
        tabs.TabPages.Add(tabAdv);

        // --- Tab: Síť / integrace ---
        var tabExp = new TabPage("Síť / integrace") { Padding = new Padding(12) };
        var flExp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtWebhookUrl = AddLabeledText(flExp, "Webhook URL:", "Po testu automaticky POSTne JSON výsledek na tuto URL (Slack, Teams, CI/CD).");
        numBandwidthLimit = AddLabeledNumeric(flExp, "Bandwidth limit (kbps):", 0, 1000000, 0, "Omezení rychlosti odesílání v kbps (0 = bez limitu).");
        var btnNet = new Button { Text = "Zobrazit síťové adaptéry", Width = 180, Height = 32, Margin = new Padding(0, 16, 0, 0) };
        btnNet.Click += (s, e) => MessageBox.Show(NetworkAdapterInfo.GetHelpText(), "Síťové adaptéry", MessageBoxButtons.OK, MessageBoxIcon.Information);
        flExp.Controls.Add(btnNet);
        tabExp.Controls.Add(flExp);
        tabs.TabPages.Add(tabExp);

        // --- Tab: Tempo a ochrana ---
        var tabPace = new TabPage("Tempo a ochrana") { Padding = new Padding(8) };
        var flPace = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        var lblPreset = new Label { Text = "Předvolba poskytovatele:", AutoSize = true, Margin = new Padding(0, 4, 0, 2) };
        flPace.Controls.Add(lblPreset);
        cmbProviderPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        foreach (var p in ProviderPresets.All)
            cmbProviderPreset.Items.Add(p.DisplayName);
        cmbProviderPreset.SelectedIndex = 2; // Standard
        cmbProviderPreset.SelectedIndexChanged += (s, e) => ApplyProviderPreset();
        toolTip.SetToolTip(cmbProviderPreset, "Vybere doporučené tempo a ochranu pro daný typ serveru. Poté můžete cokoliv upravit ručně.");
        flPace.Controls.Add(cmbProviderPreset);

        chkJitter = AddCheckBox(flPace, "Zapnout jitter (± %)", true, "Náhodná odchylka intervalu snižuje detekci robotického vzoru.");
        numJitterPercent = AddLabeledNumeric(flPace, "Jitter (± %):", 0, 50, 15, "15 % při 1000 ms = 850–1150 ms.");
        chkBurst = AddCheckBox(flPace, "Burst + pause", false, "Po N zprávách delší pauza – podobá se lidskému chování.");
        numBurstSize = AddLabeledNumeric(flPace, "Velikost burstu:", 1, 100, 10, "Počet zpráv v jedné dávce.");
        numBurstPause = AddLabeledNumeric(flPace, "Pauza po burstu (s):", 1, 3600, 30, "Délka pauzy po dokončení burstu.");
        chkBackoff = AddCheckBox(flPace, "Progressive backoff", true, "Po chybách 4xx/5xx nebo po N úspěšných zprávách zpomalovat.");
        numBackoffAfter = AddLabeledNumeric(flPace, "Úspěchů do zpomalení:", 1, 10000, 50, "Po kolika úspěšných zprávách se interval prodlouží.");
        numBackoffMult = AddLabeledNumeric(flPace, "Násobitel intervalu (×10):", 11, 50, 15, "1.5 = hodnota 15. Interval se vynásobí tímto číslem.");
        numMaxInterval = AddLabeledNumeric(flPace, "Max. interval (ms):", 100, 3600000, 60000, "Strop, kam až může interval narůst.");
        chkPerRecipient = AddCheckBox(flPace, "Limit na příjemce", false, "Max. X zpráv na jednu adresu za časové okno.");
        numMaxPerRecipient = AddLabeledNumeric(flPace, "Max. zpráv / příjemce:", 1, 10000, 20, "");
        numPerRecipientWindow = AddLabeledNumeric(flPace, "Okno (minuty):", 1, 10080, 60, "");
        chkTimeWindow = AddCheckBox(flPace, "Omezit na denní dobu", false, "Odesílat jen v zadaném intervalu hodin.");
        numWindowFrom = AddLabeledNumeric(flPace, "Od hodiny:", 0, 23, 8, "");
        numWindowTo = AddLabeledNumeric(flPace, "Do hodiny:", 0, 23, 18, "");
        chkWarmup = AddCheckBox(flPace, "Warm-up plán", false, "Postupné zvyšování rychlosti.");
        txtWarmupPhases = AddLabeledText(flPace, "Fáze (zprávy; oddělené):", "Např. 50;150;500 – počet zpráv v jednotlivých fázích.");
        txtWarmupPhases.Text = "50;150;500";
        chkGreylist = AddCheckBox(flPace, "Detekovat greylist", true, "451/452/try again – odložený retry.");
        numGreylistMinutes = AddLabeledNumeric(flPace, "Greylist retry (min):", 1, 120, 10, "");
        numGreylistRetries = AddLabeledNumeric(flPace, "Max. greylist retry:", 0, 20, 3, "");
        chkRbl = AddCheckBox(flPace, "Zkontrolovat RBL před startem", false, "Spamhaus Zen – pouze varování, nestopuje test.");
        chkCollectObserved = AddCheckBox(flPace, "Sbírat pozorované SMTP odpovědi", true, "Živá tabulka kódů a doporučení.");

        lblPaceStatus = new Label { Text = "Stav tempa: —", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 12, 0, 2) };
        flPace.Controls.Add(lblPaceStatus);
        lblEffectiveInterval = new Label { Text = "Efektivní interval: —", AutoSize = true, Margin = new Padding(0, 2, 0, 8) };
        flPace.Controls.Add(lblEffectiveInterval);

        btnCheckRbl = new Button { Text = "Zkontrolovat source IP na RBL", Width = 220, Height = 28, Margin = new Padding(0, 4, 0, 4) };
        btnCheckRbl.Click += async (s, e) => await OnCheckRblAsync();
        flPace.Controls.Add(btnCheckRbl);
        lblRblResult = new Label { Text = "", AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 2, 0, 8) };
        flPace.Controls.Add(lblRblResult);

        var lblObs = new Label { Text = "Pozorované SMTP odpovědi (během testu):", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        flPace.Controls.Add(lblObs);
        gridObserved = new DataGridView
        {
            Width = 520, Height = 120, AllowUserToAddRows = false, ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false
        };
        gridObserved.Columns.Add("Code", "Kód");
        gridObserved.Columns.Add("Text", "Text");
        gridObserved.Columns.Add("Count", "Počet");
        gridObserved.Columns.Add("Class", "Klasifikace");
        gridObserved.Columns.Add("Action", "Doporučení");
        flPace.Controls.Add(gridObserved);

        var lblKnow = new Label { Text = "Znalostní báze filtrů a limitů:", AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
        flPace.Controls.Add(lblKnow);
        gridKnowledge = new DataGridView
        {
            Width = 520, Height = 140, AllowUserToAddRows = false, ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false
        };
        gridKnowledge.Columns.Add("Provider", "Poskytovatel");
        gridKnowledge.Columns.Add("Limit", "Typický limit");
        gridKnowledge.Columns.Add("Response", "Reakce");
        gridKnowledge.Columns.Add("Reco", "Doporučení");
        foreach (var e in FilterKnowledgeBase.DefaultEntries)
            gridKnowledge.Rows.Add(e.Provider, e.TypicalLimit, e.TypicalResponse, e.Recommendation);
        flPace.Controls.Add(gridKnowledge);

        tabPace.Controls.Add(flPace);
        tabs.TabPages.Add(tabPace);

        // Záložka rychlého SMTP logu (DataGrid, max 1000 řádků, thread-safe)
        var tabSmtpLog = new TabPage("SMTP log") { Padding = new Padding(4) };
        smtpLogTab = new SmtpLogTab();
        tabSmtpLog.Controls.Add(smtpLogTab);
        tabs.TabPages.Add(tabSmtpLog);

        Controls.Add(tabs);

        // === Bottom area ===
        var bottomPanel = new Panel { Dock = DockStyle.Fill };
        var bottomTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottomTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        bottomTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));

        rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGray,
            Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        bottomTbl.Controls.Add(rtbLog, 0, 0);

        var rightPanel = new Panel { Dock = DockStyle.Fill };

        // Cesta odesílání – pipeline od PC k cílovému serveru (začíná zašedlá)
        var lblPath = new Label
        {
            Text = "Cesta odesílání (PC → cílový server):",
            AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Top
        };
        rightPanel.Controls.Add(lblPath);
        pnlDeliveryPath = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 52, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(250, 250, 250),
            Padding = new Padding(4)
        };
        BuildDeliveryPathPipeline();
        rightPanel.Controls.Add(pnlDeliveryPath);

        var lblWorkers = new Label { Text = "Workery:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Top };
        pnlWorkers = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(245, 245, 245) };
        rightPanel.Controls.Add(pnlWorkers);
        rightPanel.Controls.Add(lblWorkers);

        var lblCheck = new Label { Text = "Stav zprávy:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 4) };
        rightPanel.Controls.Add(lblCheck);
        var pnlCheck = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        for (int i = 0; i < 6; i++)
        {
            _checklistLabels[i] = new Label
            {
                Text = _checklistTexts[i], AutoSize = true,
                Padding = new Padding(6, 3, 6, 3), Margin = new Padding(2),
                BackColor = Color.LightGray, ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.FixedSingle
            };
            pnlCheck.Controls.Add(_checklistLabels[i]);
            if (i < 5)
                pnlCheck.Controls.Add(new Label { Text = "→", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(0, 3, 0, 3) });
        }
        rightPanel.Controls.Add(pnlCheck);

        bottomTbl.Controls.Add(rightPanel, 1, 0);
        bottomPanel.Controls.Add(bottomTbl);
        Controls.Add(bottomPanel);

        // === StatusStrip ===
        statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        progressBar = new ToolStripProgressBar { Width = 200, Maximum = 100 };
        lblStatus = new ToolStripStatusLabel { Text = "Připraveno", Spring = true };
        lblEta = new ToolStripStatusLabel { Text = "ETA: —" };
        lblSentFailed = new ToolStripStatusLabel { Text = "OK:0 FAIL:0" };
        statusStrip.Items.Add(lblStatus);
        statusStrip.Items.Add(progressBar);
        statusStrip.Items.Add(lblEta);
        statusStrip.Items.Add(lblSentFailed);
        Controls.Add(statusStrip);
    }

    private TextBox AddLabeledText(Control parent, string label, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var txt = new TextBox { Width = 360, Margin = new Padding(0, 0, 0, 4) };
        parent.Controls.Add(txt);
        toolTip.SetToolTip(txt, tip);
        return txt;
    }

    private NumericUpDown AddLabeledNumeric(Control parent, string label, decimal min, decimal max, decimal value, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var num = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 120, Margin = new Padding(0, 0, 0, 4) };
        parent.Controls.Add(num);
        toolTip.SetToolTip(num, tip);
        return num;
    }

    private ComboBox AddLabeledCombo(Control parent, string label, string[] items, string selected, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Margin = new Padding(0, 0, 0, 4) };
        cmb.Items.AddRange(items);
        cmb.SelectedItem = selected;
        parent.Controls.Add(cmb);
        toolTip.SetToolTip(cmb, tip);
        return cmb;
    }

    private CheckBox AddCheckBox(Control parent, string text, bool check, string tip)
    {
        var chk = new CheckBox { Text = text, AutoSize = true, Checked = check, Margin = new Padding(0, 6, 0, 2) };
        parent.Controls.Add(chk);
        toolTip.SetToolTip(chk, tip);
        return chk;
    }

    private void WireEvents()
    {
        btnStart.Click += async (s, e) => await OnStartAsync();
        btnStop.Click += (s, e) => OnStop();
        btnSaveProfile.Click += async (s, e) => await OnSaveProfileAsync();
        btnLoadProfile.Click += async (s, e) => await OnLoadProfileAsync();
        btnTestConnection.Click += async (s, e) => await OnTestConnectionAsync();
        numMaxRandomAttachments.ValueChanged += (s, e) => UpdateRandomAttachmentSafetyInfo();
        numRandomAttachmentSizeMb.ValueChanged += (s, e) => UpdateRandomAttachmentSafetyInfo();
        numConcurrency.ValueChanged += (s, e) => UpdateRandomAttachmentSafetyInfo();
        btnEmlBrowse.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "EML files|*.eml|All files|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) txtEmlTemplate.Text = dlg.FileName;
        };
    }

    private void SetDefaults()
    {
        txtHost.Text = "smtp.example.com";
        txtFrom.Text = "test@example.com";
        txtTo.Text = "user@example.com";
        txtSubject.Text = "MailLoadTester test";
        txtBody.Text = "Toto je testovací zpráva generovaná aplikací MailLoadTester.";
        txtDisplayName.Text = "MailLoadTester";
        txtProxyHost.Text = "127.0.0.1";
        txtSessionLogPath.Text = Path.Combine(AppContext.BaseDirectory, "smtp-session.log");
        UpdateRandomAttachmentSafetyInfo();
    }

    private void UpdateRandomAttachmentSafetyInfo()
    {
        if (lblRandomAttachmentSafety is null || numRandomAttachmentSizeMb is null || numMaxRandomAttachments is null || numConcurrency is null) return;
        var estimate = AttachmentPlanner.EstimateRandomAttachments(
            (int)numRandomAttachmentSizeMb.Value,
            (int)numMaxRandomAttachments.Value,
            (int)numConcurrency.Value);
        var status = estimate.IsSafe ? "✓ Bezpečné" : "⚠ BLOKOVÁNO – před startem se RAM nebude alokovat";
        lblRandomAttachmentSafety.Text = $"{status}\r\n{estimate.Explanation}\r\nDostupná RAM: {AttachmentPlanner.FormatBytes(estimate.AvailableMemoryBytes)}";
        lblRandomAttachmentSafety.ForeColor = estimate.IsSafe ? Color.DarkGreen : Color.DarkRed;
    }

    private MailTestOptions BuildOptions()
    {
        var recipients = txtTo.Text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cc = string.IsNullOrWhiteSpace(txtCc.Text) ? Array.Empty<string>() : txtCc.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bcc = string.IsNullOrWhiteSpace(txtBcc.Text) ? Array.Empty<string>() : txtBcc.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var attachments = lstAttachments.Items.Cast<string>().ToList();
        var inline = lstInlineAttachments.Items.Cast<string>().ToList();
        var headers = Validation.ParseHeaders(txtHeaders.Text);
        var security = Enum.Parse<SmtpSecurity>(cmbSecurity.SelectedItem?.ToString() ?? "StartTls", true);
        var authMethod = Enum.Parse<SmtpAuthMethod>(cmbAuthMethod.SelectedItem?.ToString() ?? "Auto", true);
        var ipVersion = Enum.Parse<IpVersionPreference>(cmbIpVersion.SelectedItem?.ToString() ?? "Any", true);

        return new MailTestOptions(
            txtFrom.Text.Trim(), recipients.ToList(),
            txtHost.Text.Trim(), (int)numPort.Value, security,
            chkAuth.Checked, txtUser.Text, txtPass.Text,
            (int)numCount.Value, (int)numInterval.Value,
            chkBatchMode.Checked, (int)numBatchSize.Value, (int)numBatchPause.Value,
            (int)numConcurrency.Value, txtSubject.Text, txtBody.Text, txtDisplayName.Text,
            chkRandomData.Checked, chkTestMode.Checked, txtAllowedDomains.Text,
            chkHtmlBody.Checked, attachments, headers, chkIgnoreCert.Checked, (int)numRetries.Value, chkDryRun.Checked,
            chkSocks5.Checked, txtProxyHost.Text, (int)numProxyPort.Value, txtProxyUser.Text, txtProxyPass.Text, ProxyList: txtProxyList?.Text ?? "", ProxyListRandom: chkProxyListRandom?.Checked ?? false, ProxyBanMinutes: (int)(numProxyBanMinutes?.Value ?? 15),
            chkDirectMx.Checked, chkPreWarm.Checked, chkAdaptive.Checked, chkCircuit.Checked, (int)numCircuitThreshold.Value,
            CircuitBreakerWindowSize: _circuitWindowSize,
            CircuitBreakerFailurePercent: _circuitFailurePercent,
            EnableDashboard: chkDashboard.Checked, DashboardPort: (int)numDashboardPort.Value,
            AuthMethod: authMethod, SourceIp: txtSourceIp.Text.Trim(), Ipv6Prefix: txtIpv6Prefix?.Text.Trim() ?? "", Ipv6PrefixLength: (int)(numIpv6PrefixLen?.Value ?? 64), Ipv4Rotation: txtIpv4Rotation?.Text.Trim() ?? "", Ipv4RotationRandom: chkIpv4RotationRandom?.Checked ?? false, ClientCertificatePath: txtClientCert.Text,
            ClientCertificatePassword: txtClientCertPass.Text,
            CcRecipients: cc.ToList(), BccRecipients: bcc.ToList(), SmtpUtf8: chkSmtpUtf8.Checked,
            InlineAttachments: inline,
            WebhookUrl: txtWebhookUrl.Text, BandwidthLimitKbps: (int)numBandwidthLimit.Value, IpVersion: ipVersion,
            ConnectTimeoutMs: (int)numConnectTimeout.Value, ReadTimeoutMs: (int)numReadTimeout.Value,
            EnableSessionLog: chkSessionLog.Checked, SessionLogPath: txtSessionLogPath.Text,
            AutoRestartOnFailure: chkAutoRestart.Checked, AutoRestartMaxAttempts: (int)numAutoRestartAttempts.Value,
            EmlTemplatePath: string.IsNullOrWhiteSpace(txtEmlTemplate.Text) ? null : txtEmlTemplate.Text,
            IdleConnectionHealthCheckSeconds: _idleHealthCheckSeconds,
            UseBogusData: chkBogusData.Checked, GenerateRandomHtml: chkRandomHtml.Checked,
            GenerateRandomAttachments: chkRandomAttachments.Checked,
            MaxRandomAttachments: (int)numMaxRandomAttachments.Value, VarySubjectBodyPerMessage: chkVaryMessage.Checked,
            RandomAttachmentSizeMb: (int)numRandomAttachmentSizeMb.Value,
            PaceProfile: ProviderPresets.All.FirstOrDefault(p => p.DisplayName == (cmbProviderPreset.SelectedItem?.ToString() ?? ""))?.Id
                ?? cmbProviderPreset.SelectedItem?.ToString() ?? "Standard",
            EnableJitter: chkJitter.Checked,
            JitterPercent: (int)numJitterPercent.Value,
            EnableBurstMode: chkBurst.Checked,
            BurstSize: (int)numBurstSize.Value,
            BurstPauseSeconds: (int)numBurstPause.Value,
            EnableProgressiveBackoff: chkBackoff.Checked,
            BackoffAfterSuccesses: (int)numBackoffAfter.Value,
            BackoffMultiplier: (int)numBackoffMult.Value / 10.0,
            MaxIntervalMs: (int)numMaxInterval.Value,
            EnablePerRecipientLimit: chkPerRecipient.Checked,
            MaxMessagesPerRecipient: (int)numMaxPerRecipient.Value,
            PerRecipientWindowMinutes: (int)numPerRecipientWindow.Value,
            EnableSendingTimeWindow: chkTimeWindow.Checked,
            SendingWindowFromHour: (int)numWindowFrom.Value,
            SendingWindowToHour: (int)numWindowTo.Value,
            EnableWarmup: chkWarmup.Checked,
            WarmupPhases: txtWarmupPhases.Text,
            DetectGreylist: chkGreylist.Checked,
            GreylistRetryMinutes: (int)numGreylistMinutes.Value,
            MaxGreylistRetries: (int)numGreylistRetries.Value,
            ProviderPreset: ProviderPresets.All.FirstOrDefault(p => p.DisplayName == (cmbProviderPreset.SelectedItem?.ToString() ?? ""))?.Id
                ?? cmbProviderPreset.SelectedItem?.ToString() ?? "Custom",
            CheckRblBeforeStart: chkRbl.Checked,
            CollectObservedResponses: chkCollectObserved.Checked);
    }

    private async Task OnStartAsync()
    {
        if (_isRunning) return;
        try
        {
            var options = BuildOptions();
            Validation.Validate(options);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chyba validace:\n{ex.Message}", "Nelze spustit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Pre-flight: SPF + DMARC pro doménu odesílatele (DnsClient.NET)
        try
        {
            var domain = DnsPolicyChecker.ExtractDomain(txtFrom.Text.Trim());
            if (!string.IsNullOrEmpty(domain))
            {
                lblStatus.Text = $"Kontroluji SPF/DMARC pro {domain}…";
                var policy = await DnsPolicyChecker.CheckAsync(domain);
                if (!policy.HasSpf)
                {
                    var text = policy.SpfCheckFailed
                        ? $"Nepodařilo se ověřit SPF záznam domény {domain} (DNS dotaz selhal).\n" +
                          "Nejde o potvrzeně chybějící SPF — DNS prostě teď neodpověděl.\n\n" +
                          "Chcete i přesto spustit test?"
                        : $"Doména {domain} nemá platný SPF záznam (TXT v=spf1).\n" +
                          "Bez SPF Gmail/Microsoft často zprávy zahodí nebo označí jako spam a poškodí reputaci IP/domény.\n\n" +
                          "Chcete i přesto spustit test?";
                    var r = MessageBox.Show(text,
                        policy.SpfCheckFailed ? "SPF neověřeno" : "Varování: Chybí SPF",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
                }
                if (!policy.HasDmarc)
                {
                    var text = policy.DmarcCheckFailed
                        ? $"Nepodařilo se ověřit DMARC politiku domény {domain} (DNS dotaz selhal).\n" +
                          "Nejde o potvrzeně chybějící DMARC — DNS prostě teď neodpověděl.\n\n" +
                          "Chcete i přesto spustit test?"
                        : $"Doména {domain} nemá DMARC politiku (_dmarc.{domain}, TXT v=DMARC1).\n" +
                          "Pro hromadné testy vůči Gmail/Yahoo je DMARC často vyžadován.\n\n" +
                          "Chcete i přesto spustit test?";
                    var r = MessageBox.Show(text,
                        policy.DmarcCheckFailed ? "DMARC neověřeno" : "Varování: Chybí DMARC",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
                }
            }
        }
        catch (Exception ex)
        {
            var r = MessageBox.Show(
                $"Kontrola SPF/DMARC selhala ({ex.Message}).\nPokračovat bez ověření?",
                "DNS kontrola", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
        }

        // Volitelná RBL kontrola source IP
        if (chkRbl != null && chkRbl.Checked && !string.IsNullOrWhiteSpace(txtSourceIp.Text))
        {
            try
            {
                lblStatus.Text = "Kontroluji RBL (Spamhaus)…";
                var rbl = await RblChecker.CheckSpamhausZenAsync(txtSourceIp.Text.Trim());
                if (rbl.IsListed)
                {
                    var r = MessageBox.Show(
                        "Source IP je na blacklistu Spamhaus Zen.\n" + rbl.Detail + "\n\nPřesto spustit test?",
                        "RBL varování", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
                }
            }
            catch { /* neblokovat start při chybě DNSBL */ }
        }

        _isRunning = true;
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        rtbLog.Clear();
        smtpLogTab?.ClearLog();
        progressBar.Value = 0;
        lblStatus.Text = "Běží…";
        lblEta.Text = "ETA: počítám…";
        lblSentFailed.Text = "OK:0 FAIL:0";
        ResetDeliveryPath();
        if (lblPaceStatus != null) lblPaceStatus.Text = "Stav tempa: Běží";
        if (gridObserved != null) gridObserved.Rows.Clear();

        pnlWorkers.Controls.Clear();
        _workerLabels = new Label[(int)numConcurrency.Value];
        for (int w = 0; w < _workerLabels.Length; w++)
        {
            _workerLabels[w] = new Label
            {
                Text = $"W{w + 1}: —", AutoSize = true,
                Padding = new Padding(4, 2, 4, 2), Margin = new Padding(2),
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5f)
            };
            pnlWorkers.Controls.Add(_workerLabels[w]);
        }

        _cts = new CancellationTokenSource();
        _runner = new SmtpTestRunner();
        _runCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new Progress<ProgressUpdate>(UpdateProgress);

        try
        {
            var result = await Task.Run(async () =>
                await _runner.RunAsync(BuildOptions(), progress, _cts.Token), _cts.Token);

            progressBar.Value = 100;
            lblStatus.Text = result.Cancelled ? "Zastaveno uživatelem" : "Dokončeno";
            lblEta.Text = "";
            lblSentFailed.Text = $"OK:{result.Sent} FAIL:{result.Failed} Retry:{result.Retries}";

            AppendLog($"\n=== VÝSLEDEK ===", Color.Cyan);
            AppendLog($"Odesláno: {result.Sent}, Selhalo: {result.Failed}, Zrušeno: {result.Cancelled}", result.Failed > 0 ? Color.OrangeRed : Color.LightGreen);
            AppendLog($"Trvání: {result.Elapsed:hh\\:mm\\:ss\\.fff}, Throughput: {result.ThroughputPerSec:F2} msg/s", Color.LightGray);
            AppendLog($"Latence avg/p95/p99: {result.AvgLatencyMs:F1} / {result.P95LatencyMs:F1} / {result.P99LatencyMs:F1} ms", Color.LightGray);
            if (result.AdaptiveConcurrency > 0)
                AppendLog($"Adaptive concurrency: {result.AdaptiveConcurrency}", Color.LightBlue);
            if (result.CircuitBreakerOpen)
                AppendLog("Circuit breaker: OPEN", Color.Red);
            if (result.MxHosts?.Count > 0)
                AppendLog($"MX hosts: {string.Join(", ", result.MxHosts)}", Color.LightBlue);
            if (!string.IsNullOrEmpty(result.LastError))
                AppendLog($"Poslední chyba: {result.LastError}", Color.Red);

            // Webhook
            if (!string.IsNullOrWhiteSpace(txtWebhookUrl.Text))
            {
                try
                {
                    // Was CancellationToken.None — meant a webhook that hung for up to
                    // its own 30s HttpClient timeout could not be interrupted, so
                    // closing the window right after a test finished (but before the
                    // webhook POST completed) blocked FormClosing's wait on
                    // _runCompletion for up to 30s, since that TCS isn't set until
                    // this whole method's finally block runs. Tying it to _cts means
                    // MainForm_FormClosing's _cts?.Cancel() actually interrupts it.
                    await WebhookNotifier.NotifyAsync(txtWebhookUrl.Text, result, _cts?.Token ?? CancellationToken.None);
                    AppendLog("Webhook notifikace odeslána.", Color.LightGreen);
                }
                catch (OperationCanceledException)
                {
                    AppendLog("Webhook přeskočen (test byl zrušen/okno se zavírá).", Color.Orange);
                }
                catch (Exception ex)
                {
                    AppendLog($"Webhook selhal: {ex.Message}", Color.Orange);
                }
            }
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Zrušeno";
            AppendLog("Test zrušen uživatelem.", Color.Yellow);
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Chyba";
            AppendLog($"Kritická chyba: {ex.Message}", Color.Red);
        }
        finally
        {
            _isRunning = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            _runCompletion?.TrySetResult(true);
        }
    }

    private void OnStop()
    {
        if (_cts is null || _cts.IsCancellationRequested) return;
        _cts.Cancel();
        lblStatus.Text = "Zastavuji…";
    }

    private async Task OnTestConnectionAsync()
    {
        if (_testConnectionCts != null) return;

        using var cts = new CancellationTokenSource();
        _testConnectionCts = cts;
        _testConnectionCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        btnTestConnection.Enabled = false;
        lblStatus.Text = "Testuji SMTP…";
        try
        {
            var options = BuildOptions();
            Validation.Validate(options);
            var result = await SmtpConnectivityTester.TestAsync(options, cts.Token);
            if (!cts.IsCancellationRequested && !_closeApproved && !IsDisposed)
                MessageBox.Show(result, "SMTP Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Closing the form or an explicit cancellation is an expected outcome.
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested && !_closeApproved && !IsDisposed)
                MessageBox.Show($"Chyba při testu:\n{ex.Message}", "SMTP Test selhal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _testConnectionCts = null;
            _testConnectionCompletion?.TrySetResult(true);
            _testConnectionCompletion = null;
            if (!IsDisposed && !_closeApproved)
            {
                btnTestConnection.Enabled = true;
                lblStatus.Text = "Připraveno";
            }
        }
    }

    private void UpdateProgress(ProgressUpdate u)
    {
        // Defense in depth alongside MainForm_FormClosing: a progress event queued
        // just before Cancel() took effect could still arrive while the form is
        // disposing. InvokeRequired itself can throw on a torn-down handle, so
        // check IsDisposed first rather than relying solely on the closing handler.
        if (IsDisposed) return;
        if (InvokeRequired) { try { Invoke(UpdateProgress, u); } catch (ObjectDisposedException) { } catch (InvalidOperationException) { } return; }
        if (IsDisposed) return;

        var done = u.Sent + u.Failed;
        var total = Math.Max(1, (int)numCount.Value);
        progressBar.Value = Math.Min(100, (int)(done * 100.0 / total));
        lblSentFailed.Text = $"OK:{u.Sent} FAIL:{u.Failed}";
        lblStatus.Text = u.CurrentStep;
        if (u.EtaSeconds.HasValue)
            lblEta.Text = $"ETA: {u.EtaSeconds.Value:F0}s";

        if (u.WorkerId.HasValue && _workerLabels != null)
        {
            int idx = u.WorkerId.Value - 1;
            if (idx >= 0 && idx < _workerLabels.Length)
            {
                var lbl = _workerLabels[idx];
                lbl.Text = $"W{u.WorkerId.Value}: {u.CurrentStep}";
                lbl.BackColor = u.MessageStep switch
                {
                    MessageStep.Succeeded => Color.FromArgb(200, 255, 200),
                    MessageStep.FailedFinal => Color.FromArgb(255, 200, 200),
                    MessageStep.FailedTransient => Color.FromArgb(255, 235, 180),
                    _ => Color.White
                };
            }
        }

        if (u.MessageStep.HasValue)
        {
            int stepIdx = u.MessageStep.Value switch
            {
                MessageStep.Queued => 0,
                MessageStep.WaitingRateLimit => 1,
                MessageStep.RentingConnection => 2,
                MessageStep.BuildingMime => 3,
                MessageStep.SmtpSend => 4,
                MessageStep.Succeeded => 5,
                MessageStep.FailedFinal => 5,
                _ => -1
            };
            for (int i = 0; i < 6; i++)
            {
                if (i == stepIdx)
                {
                    _checklistLabels[i].BackColor = Color.FromArgb(66, 153, 225);
                    _checklistLabels[i].ForeColor = Color.White;
                    _checklistLabels[i].Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                }
                else if (i < stepIdx)
                {
                    _checklistLabels[i].BackColor = Color.FromArgb(154, 230, 180);
                    _checklistLabels[i].ForeColor = Color.FromArgb(20, 60, 20);
                    _checklistLabels[i].Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                }
                else
                {
                    _checklistLabels[i].BackColor = Color.LightGray;
                    _checklistLabels[i].ForeColor = Color.DimGray;
                    _checklistLabels[i].Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                }
            }
        }

        var color = u.MessageStep switch
        {
            MessageStep.Succeeded => Color.LightGreen,
            MessageStep.FailedFinal => Color.OrangeRed,
            MessageStep.FailedTransient => Color.Yellow,
            _ => Color.LightGray
        };
        // Hlavní log: jen finální stavy + fáze přípravy (ne každý CONNECT/EHLO)
        if (u.MessageStep is MessageStep.Succeeded or MessageStep.FailedFinal or MessageStep.FailedTransient
            || u.Phase <= 2)
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {u.CurrentStep}", color);

        // Rychlý grid log (SMTP log záložka) – jen významné stavy
        if (smtpLogTab != null && u.MessageStep is MessageStep.Succeeded or MessageStep.FailedFinal or MessageStep.FailedTransient)
        {
            var st = u.MessageStep switch
            {
                MessageStep.Succeeded => ("OK", Color.SeaGreen),
                MessageStep.FailedTransient => ("RETRY", Color.DarkOrange),
                _ => ("FAIL", Color.Firebrick)
            };
            smtpLogTab.AddLogEntry(
                email: u.CurrentStep.Contains('→') ? u.CurrentStep : $"#{u.MessageIndex}",
                status: st.Item1,
                message: u.CurrentStep,
                statusColor: st.Item2);
        }

        // Živá tabulka pozorovaných SMTP odpovědí (max ~2×/s)
        if (u.ObservedSnapshot != null && gridObserved != null)
        {
            var nowObs = DateTime.UtcNow;
            if ((nowObs - _observedThrottle).TotalMilliseconds >= 500 || u.MessageStep == MessageStep.FailedFinal)
            {
                _observedThrottle = nowObs;
                gridObserved.SuspendLayout();
                try
                {
                    gridObserved.Rows.Clear();
                    foreach (var row in u.ObservedSnapshot.Take(50))
                    {
                        gridObserved.Rows.Add(
                            row.SmtpCode,
                            row.SampleText,
                            row.Count,
                            row.Classification.ToString(),
                            row.RecommendedAction);
                    }
                }
                finally { gridObserved.ResumeLayout(); }
            }
        }

        if (!string.IsNullOrEmpty(u.PaceStatus) && lblPaceStatus != null)
            lblPaceStatus.Text = "Stav tempa: " + u.PaceStatus
                + (u.EffectiveIntervalMs is int ms ? $" · interval {ms} ms" : "");

        // Cesta odesílání – postupné zbarvování
        if (u.PathStep.HasValue)
            ColorPathStep(u.PathStep.Value, u.PathStepSuccess ?? true);

        // Mapování MessageStep → path kroky (když runner posílá MessageStep)
        if (u.MessageStep.HasValue)
        {
            var mapped = u.MessageStep.Value switch
            {
                MessageStep.RentingConnection => DeliveryStepKind.TcpConnect,
                MessageStep.BuildingMime => DeliveryStepKind.MailFrom,
                MessageStep.SmtpSend => DeliveryStepKind.Data,
                MessageStep.Succeeded => DeliveryStepKind.Quit,
                MessageStep.FailedFinal => DeliveryStepKind.Error,
                _ => (DeliveryStepKind?)null
            };
            if (mapped.HasValue)
                ColorPathStep(mapped.Value, u.MessageStep != MessageStep.FailedFinal && u.MessageStep != MessageStep.FailedTransient);
        }

        if (!string.IsNullOrEmpty(u.PaceStatus) && lblPaceStatus != null)
            lblPaceStatus.Text = "Stav tempa: " + u.PaceStatus;
        if (u.EffectiveIntervalMs.HasValue && lblEffectiveInterval != null)
            lblEffectiveInterval.Text = $"Efektivní interval: {u.EffectiveIntervalMs.Value} ms";
    }

    private void AppendLog(string text, Color color)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { Invoke(AppendLog, text, color); } catch (ObjectDisposedException) { } catch (InvalidOperationException) { } return; }
        if (IsDisposed) return;

        // Throttle: max 25 řádků / 200 ms (ochrana UI při vysokém paralelismu)
        var now = DateTime.UtcNow;
        if ((now - _logThrottleWindow).TotalMilliseconds > 200)
        {
            if (_logSuppressed > 0)
            {
                rtbLog.SelectionStart = rtbLog.TextLength;
                rtbLog.SelectionColor = Color.DimGray;
                rtbLog.AppendText($"… potlačeno {_logSuppressed} log řádků …\n");
                _logSuppressed = 0;
            }
            _logThrottleWindow = now;
            _logThrottleCount = 0;
        }
        if (_logThrottleCount >= 25)
        {
            _logSuppressed++;
            return;
        }
        _logThrottleCount++;

        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionColor = color;
        rtbLog.AppendText(text + "\n");
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();

        var lines = rtbLog.Lines;
        if (lines.Length > 5000)
        {
            rtbLog.Text = string.Join("\n", lines.Skip(lines.Length - 4000));
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.ScrollToCaret();
        }
    }

    /// <summary>
    /// Vytvoří zašedlou pipeline: DNS → TCP → EHLO → STARTTLS → AUTH → MAIL FROM → RCPT TO → DATA → QUIT
    /// </summary>
    private void BuildDeliveryPathPipeline()
    {
        pnlDeliveryPath.Controls.Clear();
        _pathLabels.Clear();
        var titles = new Dictionary<DeliveryStepKind, string>
        {
            [DeliveryStepKind.DnsMxLookup] = "DNS MX",
            [DeliveryStepKind.TcpConnect] = "TCP",
            [DeliveryStepKind.Ehlo] = "EHLO",
            [DeliveryStepKind.StartTls] = "STARTTLS",
            [DeliveryStepKind.Auth] = "AUTH",
            [DeliveryStepKind.MailFrom] = "MAIL FROM",
            [DeliveryStepKind.RcptTo] = "RCPT TO",
            [DeliveryStepKind.Data] = "DATA",
            [DeliveryStepKind.Quit] = "QUIT"
        };
        for (int i = 0; i < _pathOrder.Length; i++)
        {
            var kind = _pathOrder[i];
            var lbl = new Label
            {
                Text = titles[kind],
                AutoSize = true,
                Padding = new Padding(6, 4, 6, 4),
                Margin = new Padding(2),
                BackColor = Color.Gainsboro,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8f),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Tag = kind
            };
            toolTip.SetToolTip(lbl, $"Krok: {titles[kind]}\nZašedlý = ještě neproběhl.\nZelený = úspěch.\nČervený = chyba.\nOranžový = probíhá.");
            lbl.Click += (s, e) =>
            {
                if (s is Label l && l.Tag is DeliveryStepKind k)
                    MessageBox.Show($"Krok cesty: {titles[k]}\n\nStav: {(l.BackColor == Color.Gainsboro ? "čeká" : l.BackColor.R > 200 && l.BackColor.G < 150 ? "chyba" : "dokončeno / probíhá")}",
                        "Cesta odesílání", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _pathLabels[kind] = lbl;
            pnlDeliveryPath.Controls.Add(lbl);
            if (i < _pathOrder.Length - 1)
                pnlDeliveryPath.Controls.Add(new Label { Text = "→", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(0, 4, 0, 0) });
        }
    }

    /// <summary>
    /// Zbarví krok cesty a všechny předchozí jako hotové (zelené).
    /// </summary>
    private void ColorPathStep(DeliveryStepKind step, bool success)
    {
        if (!_pathLabels.ContainsKey(step) && step != DeliveryStepKind.Error) return;

        int idx = Array.IndexOf(_pathOrder, step);
        if (step == DeliveryStepKind.Error)
        {
            // Označ poslední aktivní jako chybu
            foreach (var kv in _pathLabels)
            {
                if (kv.Value.BackColor == Color.FromArgb(255, 200, 100))
                {
                    kv.Value.BackColor = Color.FromArgb(255, 160, 160);
                    kv.Value.ForeColor = Color.DarkRed;
                }
            }
            return;
        }

        for (int i = 0; i < _pathOrder.Length; i++)
        {
            var kind = _pathOrder[i];
            if (!_pathLabels.TryGetValue(kind, out var lbl)) continue;
            if (i < idx)
            {
                lbl.BackColor = Color.FromArgb(180, 230, 180);
                lbl.ForeColor = Color.FromArgb(20, 80, 20);
            }
            else if (i == idx)
            {
                if (success)
                {
                    lbl.BackColor = Color.FromArgb(100, 200, 120);
                    lbl.ForeColor = Color.White;
                    lbl.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                }
                else
                {
                    lbl.BackColor = Color.FromArgb(255, 160, 160);
                    lbl.ForeColor = Color.DarkRed;
                    lbl.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                }
            }
            else if (lbl.BackColor != Color.FromArgb(100, 200, 120) && lbl.BackColor != Color.FromArgb(180, 230, 180))
            {
                // ještě neproběhlo – nech zašedlé, nebo označ „probíhá“ oranžově jen aktuální
            }
        }
    }

    private void ResetDeliveryPath()
    {
        foreach (var lbl in _pathLabels.Values)
        {
            lbl.BackColor = Color.Gainsboro;
            lbl.ForeColor = Color.DimGray;
            lbl.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
        }
    }

    private void ApplyProviderPreset()
    {
        var name = cmbProviderPreset.SelectedItem?.ToString();
        var preset = ProviderPresets.All.FirstOrDefault(p => p.DisplayName == name);
        if (preset is null) return;

        numInterval.Value = Math.Clamp(preset.IntervalMs, numInterval.Minimum, numInterval.Maximum);
        chkJitter.Checked = preset.EnableJitter;
        numJitterPercent.Value = preset.JitterPercent;
        chkBurst.Checked = preset.EnableBurstMode;
        numBurstSize.Value = preset.BurstSize;
        numBurstPause.Value = preset.BurstPauseSeconds;
        chkBackoff.Checked = preset.EnableProgressiveBackoff;
        numBackoffAfter.Value = preset.BackoffAfterSuccesses;
        numBackoffMult.Value = (decimal)Math.Clamp(preset.BackoffMultiplier * 10, 11, 50);
        numMaxInterval.Value = Math.Clamp(preset.MaxIntervalMs, numMaxInterval.Minimum, numMaxInterval.Maximum);
        numConcurrency.Value = Math.Clamp(preset.MaxConcurrency, numConcurrency.Minimum, numConcurrency.Maximum);
        chkGreylist.Checked = preset.DetectGreylist;
        numGreylistMinutes.Value = preset.GreylistRetryMinutes;
        chkWarmup.Checked = preset.EnableWarmup;
        txtWarmupPhases.Text = preset.WarmupPhases;
        numPort.Value = preset.SuggestedPort;
        cmbSecurity.SelectedItem = preset.SuggestedSecurity.ToString();
    }


    private async Task OnSaveProfileAsync()
    {
        try
        {
            var options = BuildOptions();
            using var dlg = new SaveFileDialog
            {
                Filter = "JSON profil|*.json",
                FileName = "mailloadtester-profile.json",
                Title = "Uložit profil testu"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            await ProfileStore.SaveAsync(dlg.FileName, options, profileName: Path.GetFileNameWithoutExtension(dlg.FileName), includeSecrets: false);
            AppendLog($"Profil uložen: {dlg.FileName} (hesla vynechána)", Color.LightGreen);
            MessageBox.Show("Profil uložen.\nHesla a tajemství se do souboru neukládají.", "Profil", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Uložení profilu selhalo:\n" + ex.Message, "Profil", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnLoadProfileAsync()
    {
        try
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "JSON profil|*.json|All files|*.*",
                Title = "Načíst profil testu"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var file = await ProfileStore.LoadAsync(dlg.FileName);
            ApplyOptionsToUi(file.Options);
            AppendLog($"Profil načten: {dlg.FileName}" + (file.Name is { } n ? $" ({n})" : ""), Color.LightBlue);
            var msg = "Profil načten do formuláře.\nHesla doplňte ručně, pokud jsou potřeba.";
            if (ProfileStore.IsVersionMismatch(file))
                msg += $"\n\nUpozornění: profil je z verze {file.Version}, aplikace je {AppVersion.Current}. Novější volby mohou mít výchozí hodnoty.";
            MessageBox.Show(msg, "Profil", MessageBoxButtons.OK,
                ProfileStore.IsVersionMismatch(file) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Načtení profilu selhalo:\n" + ex.Message, "Profil", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Naplní GUI z MailTestOptions (hesla se nepřepisují, pokud jsou v profilu prázdná).</summary>
    private void ApplyOptionsToUi(MailTestOptions o)
    {
        txtHost.Text = o.SmtpHost;
        numPort.Value = Math.Clamp(o.Port, numPort.Minimum, numPort.Maximum);
        cmbSecurity.SelectedItem = o.Security.ToString();
        chkIgnoreCert.Checked = o.IgnoreCertificateErrors;
        chkAuth.Checked = o.UseAuthentication;
        cmbAuthMethod.SelectedItem = o.AuthMethod.ToString();
        txtUser.Text = o.Username;
        if (!string.IsNullOrEmpty(o.Password)) txtPass.Text = o.Password;
        txtSourceIp.Text = o.SourceIp ?? "";
        if (txtIpv6Prefix != null) txtIpv6Prefix.Text = o.Ipv6Prefix ?? "";
        if (numIpv6PrefixLen != null) numIpv6PrefixLen.Value = Math.Clamp(o.Ipv6PrefixLength <= 0 ? 64 : o.Ipv6PrefixLength, numIpv6PrefixLen.Minimum, numIpv6PrefixLen.Maximum);
        cmbIpVersion.SelectedItem = o.IpVersion.ToString();
        txtFrom.Text = o.From;
        txtTo.Text = string.Join(", ", o.Recipients ?? Array.Empty<string>());
        txtCc.Text = string.Join(", ", o.CcRecipients ?? Array.Empty<string>());
        txtBcc.Text = string.Join(", ", o.BccRecipients ?? Array.Empty<string>());
        txtDisplayName.Text = o.DisplayName;
        txtSubject.Text = o.Subject;
        txtBody.Text = o.Body;
        chkHtmlBody.Checked = o.HtmlBody;
        chkRandomData.Checked = o.RandomTestData;
        chkBogusData.Checked = o.UseBogusData;
        chkRandomHtml.Checked = o.GenerateRandomHtml;
        chkRandomAttachments.Checked = o.GenerateRandomAttachments;
        numMaxRandomAttachments.Value = Math.Clamp(o.MaxRandomAttachments, numMaxRandomAttachments.Minimum, numMaxRandomAttachments.Maximum);
        chkVaryMessage.Checked = o.VarySubjectBodyPerMessage;
        numRandomAttachmentSizeMb.Value = Math.Clamp(o.RandomAttachmentSizeMb, numRandomAttachmentSizeMb.Minimum, numRandomAttachmentSizeMb.Maximum);
        numCount.Value = Math.Clamp(o.MessageCount, numCount.Minimum, numCount.Maximum);
        numInterval.Value = Math.Clamp(o.IntervalMs, numInterval.Minimum, numInterval.Maximum);
        numConcurrency.Value = Math.Clamp(o.MaxConcurrency, numConcurrency.Minimum, numConcurrency.Maximum);
        chkBatchMode.Checked = o.BatchMode;
        numBatchSize.Value = Math.Clamp(o.BatchSize, numBatchSize.Minimum, numBatchSize.Maximum);
        numBatchPause.Value = Math.Clamp(o.BatchPauseSeconds, numBatchPause.Minimum, numBatchPause.Maximum);
        numRetries.Value = Math.Clamp(o.MaxRetries, numRetries.Minimum, numRetries.Maximum);
        chkDryRun.Checked = o.DryRun;
        chkTestMode.Checked = o.TestMode;
        txtAllowedDomains.Text = o.AllowedDomains;
        chkSocks5.Checked = o.UseSocks5Proxy;
        txtProxyHost.Text = o.ProxyHost;
        if (txtProxyList != null) txtProxyList.Text = o.ProxyList ?? "";
        _idleHealthCheckSeconds = Math.Clamp(o.IdleConnectionHealthCheckSeconds, 0, 86_400);
        _circuitWindowSize = o.CircuitBreakerWindowSize > 0 ? o.CircuitBreakerWindowSize : 100;
        _circuitFailurePercent = o.CircuitBreakerFailurePercent > 0 ? o.CircuitBreakerFailurePercent : 90.0;
        if (chkProxyListRandom != null) chkProxyListRandom.Checked = o.ProxyListRandom;
        if (numProxyBanMinutes != null) numProxyBanMinutes.Value = Math.Clamp(o.ProxyBanMinutes <= 0 ? 15 : o.ProxyBanMinutes, 1, 1440);
        numProxyPort.Value = Math.Clamp(o.ProxyPort <= 0 ? 1080 : o.ProxyPort, numProxyPort.Minimum, numProxyPort.Maximum);
        txtProxyUser.Text = o.ProxyUsername;
        if (!string.IsNullOrEmpty(o.ProxyPassword)) txtProxyPass.Text = o.ProxyPassword;
        chkJitter.Checked = o.EnableJitter;
        numJitterPercent.Value = Math.Clamp(o.JitterPercent, numJitterPercent.Minimum, numJitterPercent.Maximum);
        chkBurst.Checked = o.EnableBurstMode;
        numBurstSize.Value = Math.Clamp(o.BurstSize, numBurstSize.Minimum, numBurstSize.Maximum);
        numBurstPause.Value = Math.Clamp(o.BurstPauseSeconds, numBurstPause.Minimum, numBurstPause.Maximum);
        chkBackoff.Checked = o.EnableProgressiveBackoff;
        numBackoffAfter.Value = Math.Clamp(o.BackoffAfterSuccesses, numBackoffAfter.Minimum, numBackoffAfter.Maximum);
        chkGreylist.Checked = o.DetectGreylist;
        numGreylistMinutes.Value = Math.Clamp(o.GreylistRetryMinutes, numGreylistMinutes.Minimum, numGreylistMinutes.Maximum);
        chkWarmup.Checked = o.EnableWarmup;
        txtWarmupPhases.Text = o.WarmupPhases;
        chkTimeWindow.Checked = o.EnableSendingTimeWindow;
        numWindowFrom.Value = Math.Clamp(o.SendingWindowFromHour, numWindowFrom.Minimum, numWindowFrom.Maximum);
        numWindowTo.Value = Math.Clamp(o.SendingWindowToHour, numWindowTo.Minimum, numWindowTo.Maximum);
        chkPerRecipient.Checked = o.EnablePerRecipientLimit;
        numMaxPerRecipient.Value = Math.Clamp(o.MaxMessagesPerRecipient, numMaxPerRecipient.Minimum, numMaxPerRecipient.Maximum);
        chkRbl.Checked = o.CheckRblBeforeStart;
        chkCollectObserved.Checked = o.CollectObservedResponses;
        chkCircuit.Checked = o.UseCircuitBreaker;
        numCircuitThreshold.Value = Math.Clamp(o.CircuitBreakerThreshold, numCircuitThreshold.Minimum, numCircuitThreshold.Maximum);
        chkDirectMx.Checked = o.DirectMxDelivery;
        chkPreWarm.Checked = o.PreWarmConnections;
        chkAdaptive.Checked = o.UseAdaptiveConcurrency;
        UpdateRandomAttachmentSafetyInfo();
    }

    private async Task OnCheckRblAsync()
    {
        var ip = txtSourceIp.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            MessageBox.Show("Nejdříve vyplňte Source IP na záložce SMTP Server.", "RBL", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        btnCheckRbl.Enabled = false;
        lblRblResult.Text = "Kontroluji Spamhaus Zen…";
        lblRblResult.ForeColor = Color.DimGray;
        try
        {
            var result = await RblChecker.CheckSpamhausZenAsync(ip);
            lblRblResult.Text = result.IsListed ? "⚠ " + result.Detail
                : result.IsUnknown ? "? " + result.Detail
                : "✓ " + result.Detail;
            lblRblResult.ForeColor = result.IsListed ? Color.DarkRed
                : result.IsUnknown ? Color.DarkOrange
                : Color.DarkGreen;
        }
        catch (Exception ex)
        {
            lblRblResult.Text = "Chyba RBL: " + ex.Message;
            lblRblResult.ForeColor = Color.DarkRed;
        }
        finally
        {
            btnCheckRbl.Enabled = true;
        }
    }
}
