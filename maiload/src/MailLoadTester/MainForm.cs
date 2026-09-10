namespace MailLoadTester;

public sealed class MainForm : Form
{
    readonly TextBox from = new() { Text = "test@example.com" },
        to = new() { Text = "test@example.com;test2@example.com" },
        smtp = new() { Text = "smtp.example.com" },
        user = new(), pass = new() { Height = 23 },
        count = new() { Text = "10" },
        interval = new() { Text = "500" },
        batchSize = new() { Text = "10" },
        batchPause = new() { Text = "30" },
        concurrency = new() { Text = "1" },
        domains = new() { Text = "example.com, test.local", Height = 23 },
        subject = new() { Text = "SMTP test" },
        name = new() { Text = "MailLoadTester" },
        body = new() { Text = "Automaticky generovaná testovací zpráva.", Multiline = true, ScrollBars = ScrollBars.Vertical },
        headers = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 40 },
        retries = new() { Text = "1" };

    // Port: výběr z běžných + libovolné číslo (DropDown = editovatelný)
    readonly ComboBox port = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 90 };
    readonly ComboBox security = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    readonly Label memInfo = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        Text = "Přílohy: žádné — limit paměti se dopočítá podle paralelismu."
    };
    readonly Label netInfo = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        Text = "Síť: zjišťuji…",
        Cursor = Cursors.Help
    };
    // Barevný průběh fází
    readonly Label stagePrep = new() { AutoSize = true, Text = "1. Připraveno", Padding = new Padding(8, 4, 8, 4) },
        stageValidate = new() { AutoSize = true, Text = "2. Validace / přílohy", Padding = new Padding(8, 4, 8, 4) },
        stageConnect = new() { AutoSize = true, Text = "3. SMTP spojení", Padding = new Padding(8, 4, 8, 4) },
        stageSend = new() { AutoSize = true, Text = "4. Odesílání", Padding = new Padding(8, 4, 8, 4) },
        stageDone = new() { AutoSize = true, Text = "5. Hotovo", Padding = new Padding(8, 4, 8, 4) };
    readonly Panel stagePanel = new() { Height = 32, Dock = DockStyle.Top };
    /// <summary>Zeleně = už se stalo / právě dokončeno</summary>
    readonly Label detailDone = new()
    {
        AutoSize = true,
        ForeColor = Color.DarkGreen,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Text = "Hotovo: —"
    };
    /// <summary>Oranžově = co má následovat</summary>
    readonly Label detailNext = new()
    {
        AutoSize = true,
        ForeColor = Color.DarkOrange,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Text = "Následuje: vyplňte údaje a stiskněte TEST SMTP nebo SEND"
    };
    Label? lblFrom, lblTo, lblServer, lblPortSec, lblPort, lblUser, lblPass, lblDomains, lblSubject, lblName, lblHeaders, lblBody, lblAtt;
    readonly CheckBox auth = new() { Text = "SMTP autentizace", AutoSize = true },
        random = new() { Text = "Random test data", AutoSize = true },
        testMode = new() { Text = "Test mode", Checked = true, AutoSize = true },
        batch = new() { Text = "Batch / pause", AutoSize = true },
        htmlBody = new() { Text = "HTML body", AutoSize = true },
        ignoreCert = new() { Text = "Ignore certificate errors", AutoSize = true },
        dryRun = new() { Text = "Dry-run (bez odeslání)", AutoSize = true };

    readonly ListBox attachments = new() { Height = 58, IntegralHeight = false };
    readonly Button addAttachment = new() { Text = "Add…", Width = 70 },
        removeAttachment = new() { Text = "Remove", Width = 70 },
        send = new() { Text = "SEND", Width = 120, Height = 36 },
        testBtn = new() { Text = "TEST SMTP", Width = 120, Height = 36 },
        stop = new() { Text = "STOP", Width = 100, Height = 36, Enabled = false };

    readonly ProgressBar bar = new() { Dock = DockStyle.Fill, Height = 18 };
    readonly Label stats = new() { AutoSize = true, Text = "Ready" };
    readonly Label workerStatus = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Text = "Workers: —"
    };
    readonly FlowLayoutPanel workerPanel = new()
    {
        AutoSize = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(0, 2, 0, 2),
        Padding = new Padding(0)
    };
    readonly Label[] messageChecklist =
    [
        new Label { AutoSize = true, Text = "○ Fronta", Margin = new Padding(0, 0, 10, 0) },
        new Label { AutoSize = true, Text = "○ Rate limit", Margin = new Padding(0, 0, 10, 0) },
        new Label { AutoSize = true, Text = "○ SMTP spojení", Margin = new Padding(0, 0, 10, 0) },
        new Label { AutoSize = true, Text = "○ MIME", Margin = new Padding(0, 0, 10, 0) },
        new Label { AutoSize = true, Text = "○ SMTP SEND", Margin = new Padding(0, 0, 10, 0) },
        new Label { AutoSize = true, Text = "○ OK", Margin = new Padding(0, 0, 10, 0) }
    ];
    readonly Label lastFailure = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        Text = "Poslední chyba: —"
    };
    readonly TextBox log = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill, Font = new Font("Consolas", 9)
    };
    readonly StatusStrip statusStrip = new();
    readonly ToolStripStatusLabel statusLabel = new() { Text = "MailLoadTester 2.8.10  |  Připraveno" };
    readonly ToolStripStatusLabel netStatusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    readonly ToolTip tips = new()
    {
        AutoPopDelay = 30000,
        InitialDelay = 300,
        ReshowDelay = 100,
        ShowAlways = true,
        UseAnimation = false,
        UseFading = false
    };

    CancellationTokenSource? cts;

    public MainForm()
    {
        Text = "MailLoadTester 2.8.10";
        MinimumSize = new Size(1040, 920);
        StartPosition = FormStartPosition.CenterScreen;
        BuildUi();
        WireEvents();
        ApplyTooltips();
        SetStage(0);
        RefreshAttachmentMemoryInfo();
        RefreshNetworkInfo();
    }

    void WireEvents()
    {
        user.Enabled = pass.Enabled = false;
        auth.CheckedChanged += (_, _) => user.Enabled = pass.Enabled = auth.Checked;
        random.CheckedChanged += (_, _) =>
        {
            subject.Enabled = name.Enabled = body.Enabled = !random.Checked;
        };
        batch.CheckedChanged += (_, _) => batchSize.Enabled = batchPause.Enabled = batch.Checked;
        testMode.CheckedChanged += (_, _) => domains.Enabled = testMode.Checked;
        batchSize.Enabled = batchPause.Enabled = false;
        domains.Enabled = testMode.Checked;

        concurrency.TextChanged += (_, _) => RefreshAttachmentMemoryInfo();
        port.SelectedIndexChanged += (_, _) => SuggestSecurityForPort();
        port.Leave += (_, _) => SuggestSecurityForPort();

        addAttachment.Click += (_, _) =>
        {
            var planHint = GetAttachmentBudgetHint();
            using var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Vyberte přílohy — " + planHint
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (var f in ofd.FileNames)
                    if (!attachments.Items.Contains(f)) attachments.Items.Add(f);
                RefreshAttachmentMemoryInfo();
            }
        };
        removeAttachment.Click += (_, _) =>
        {
            while (attachments.SelectedItems.Count > 0)
            {
                var item = attachments.SelectedItems[0];
                if (item != null) attachments.Items.Remove(item);
            }
            RefreshAttachmentMemoryInfo();
        };

        testBtn.Click += async (_, _) => await TestSmtpAsync();
        send.Click += async (_, _) => await SendAsync();
        stop.Click += (_, _) => cts?.Cancel();
    }

    void SuggestSecurityForPort()
    {
        if (!int.TryParse(port.Text.Trim(), out var p)) return;
        if (p == 465) security.SelectedIndex = 2;      // Implicit TLS
        else if (p == 587 || p == 2587) security.SelectedIndex = 1; // STARTTLS
        else if (p == 25 || p == 2525) { /* leave user choice */ }
    }

    string GetAttachmentBudgetHint()
    {
        if (!int.TryParse(concurrency.Text, out var conc)) conc = 1;
        conc = Math.Clamp(conc, 1, 20);
        try
        {
            var plan = AttachmentPlanner.Prepare(Array.Empty<string>(), conc);
            return $"dostupná RAM ~{AttachmentPlanner.FormatBytes(plan.AvailableMemoryBytes)}, " +
                   $"rozpočet preload ~{AttachmentPlanner.FormatBytes(plan.PreloadBudgetBytes)} (při paralelismu {conc})";
        }
        catch
        {
            return "limit paměti se dopočítá automaticky";
        }
    }

    void RefreshAttachmentMemoryInfo()
    {
        try
        {
            if (!int.TryParse(concurrency.Text, out var conc)) conc = 1;
            conc = Math.Clamp(conc, 1, 20);
            var paths = attachments.Items.Cast<string>().ToList();
            if (paths.Count == 0)
            {
                var empty = AttachmentPlanner.Prepare(Array.Empty<string>(), conc);
                memInfo.Text = $"Přílohy: žádné | Dostupné ~{AttachmentPlanner.FormatBytes(empty.AvailableMemoryBytes)} | " +
                               $"Rozpočet preload ~{AttachmentPlanner.FormatBytes(empty.PreloadBudgetBytes)} při paralelismu {conc}. " +
                               "Velikost zvolených souborů se zobrazí po Add.";
                memInfo.ForeColor = Color.DimGray;
                return;
            }
            var plan = AttachmentPlanner.Prepare(paths, conc);
            memInfo.Text = plan.Description + $" | Paralelismus: {conc}";
            memInfo.ForeColor = plan.Preloaded ? Color.DarkGreen : Color.DarkOrange;
            tips.SetToolTip(memInfo, plan.Description +
                "\n\nJak to funguje:\n• Malé přílohy → do RAM (rychlejší)\n• Velké / vysoký paralelismus → čtení z disku (šetří paměť)\n" +
                "• Limit se přepočítá při změně paralelismu nebo příloh");
            var baseTip = tips.GetToolTip(attachments) ?? "";
            var cut = baseTip.Split(new[] { "\n\nAktuálně:" }, StringSplitOptions.None)[0];
            tips.SetToolTip(attachments, cut + "\n\nAktuálně:\n" + plan.Description);
        }
        catch (Exception ex)
        {
            memInfo.Text = "Přílohy: " + ex.Message;
            memInfo.ForeColor = Color.Firebrick;
        }
    }

    void RefreshNetworkInfo()
    {
        try
        {
            var line = NetworkAdapterInfo.GetStatusLine();
            var help = NetworkAdapterInfo.GetHelpText();
            netInfo.Text = line + "  (najeďte myší pro návod, jak změnit adaptér ve Windows)";
            netInfo.ForeColor = line.Contains("žádný", StringComparison.OrdinalIgnoreCase)
                ? Color.DarkOrange
                : Color.DimGray;
            tips.SetToolTip(netInfo, help);
            netStatusLabel.Text = line;
            tips.SetToolTip(statusStrip, help);
        }
        catch (Exception ex)
        {
            netInfo.Text = "Síť: chyba zjištění — " + ex.Message;
            netStatusLabel.Text = netInfo.Text;
        }
    }

    /// <summary>0=prep … 4=done, -1=error. Aktivní fáze = oranžová (právě běží), hotové = zelená.</summary>
    void SetStage(int active)
    {
        Label[] stages = [stagePrep, stageValidate, stageConnect, stageSend, stageDone];
        for (int i = 0; i < stages.Length; i++)
        {
            if (active < 0)
            {
                stages[i].BackColor = Color.FromArgb(255, 220, 220);
                stages[i].ForeColor = Color.DarkRed;
            }
            else if (i < active)
            {
                stages[i].BackColor = Color.FromArgb(198, 239, 206);
                stages[i].ForeColor = Color.DarkGreen;
            }
            else if (i == active)
            {
                stages[i].BackColor = Color.FromArgb(255, 243, 176);
                stages[i].ForeColor = Color.DarkOrange;
            }
            else
            {
                stages[i].BackColor = Color.FromArgb(240, 240, 240);
                stages[i].ForeColor = Color.Gray;
            }
        }
    }

    void SetDetail(string doneText, string nextText, bool error = false)
    {
        if (IsDisposed) return;
        detailDone.Text = "Hotovo / právě: " + doneText;
        detailDone.ForeColor = error ? Color.DarkRed : Color.DarkGreen;
        detailNext.Text = "Následuje: " + nextText;
        detailNext.ForeColor = error ? Color.DarkRed : Color.DarkOrange;
    }

    void ApplyTooltips()
    {
        tips.SetToolTip(from,
            "FROM – e-mail odesílatele (povinné).\n" +
            "Formát: jmeno@domena.cz\n" +
            "PROBLÉM „neplatný formát“:\n• Zkontrolujte zavináč a doménu\n• Bez mezer, bez úvozovek\n• Oprava: pole From nahoře v sekci Odesílatel");
        tips.SetToolTip(to,
            "TO – příjemci (povinné).\n" +
            "Více adres: středník, čárka nebo nový řádek.\n" +
            "PROBLÉM „neplatný e-mail / Test mode“:\n• Každá adresa musí mít tvar uziv@domena\n• V Test mode musí doména být v „Povolené domény“\n• Oprava: pole To nebo vypněte Test mode");
        tips.SetToolTip(smtp,
            "SMTP server – hostname nebo IP (povinné).\n" +
            "Příklad: smtp.firma.cz, 192.168.1.10\n" +
            "PROBLÉM „nelze se připojit“:\n• Ověřte DNS / ping / firewall (port níže)\n• Nejdřív použijte tlačítko TEST SMTP\n• Oprava: toto pole Server");
        tips.SetToolTip(port,
            "PORT – vyberte ze seznamu nebo zadejte vlastní (1–65535).\n" +
            "Nabídka: 25, 26, 465, 587, 2525, 2465, 2587, 3535, 8025, 1025, 10025, 443\n" +
            "• 25 / 2525 – často bez TLS nebo dle serveru\n" +
            "• 587 / 2587 – STARTTLS\n" +
            "• 465 / 2465 – Implicit TLS\n" +
            "PROBLÉM „port vs. zabezpečení“:\n• Po výběru 465/587 se TLS navrhne automaticky\n• Oprava: toto pole Port + Zabezpečení vedle");
        tips.SetToolTip(security,
            "ZABEZPEČENÍ spojení se serverem.\n" +
            "• None – bez šifrování (port 25)\n" +
            "• STARTTLS – TLS po EHLO (port 587)\n" +
            "• Implicit TLS – TLS hned (port 465)\n" +
            "PROBLÉM certifikátu:\n• Zaškrtněte „Ignore certificate errors“ (jen test)\n• Nebo nainstalujte důvěryhodný certifikát serveru");
        tips.SetToolTip(auth,
            "SMTP AUTENTIZACE (LOGIN/PLAIN).\n" +
            "Zapněte, pokud server vyžaduje přihlášení.\n" +
            "PROBLÉM 535 / autentizace selhala:\n• Zapněte tuto volbu a vyplňte Username + Password\n• Ověřte účet u správce pošty\n• Oprava: checkbox + pole Username/Password");
        tips.SetToolTip(user,
            "USERNAME pro SMTP login.\n" +
            "PROBLÉM „povinné uživatelské jméno“:\n• Buď vyplňte jméno, nebo vypněte „SMTP autentizace“\n• Oprava: toto pole Username");
        tips.SetToolTip(pass,
            "PASSWORD pro SMTP login.\n" +
            "PROBLÉM „povinné heslo“:\n• Vyplňte heslo, nebo vypněte autentizaci\n• V CLI raději MAILLOADTESTER_PASSWORD (ne --pass)\n• Oprava: toto pole Password");
        tips.SetToolTip(ignoreCert,
            "IGNORE CERTIFICATE ERRORS\n" +
            "Vypne kontrolu TLS certifikátu (self-signed).\n" +
            "POUZE pro testování – ne pro produkci.\n" +
            "PROBLÉM „SSL/certificate“:\n• Dočasně zaškrtněte tuto volbu a znovu TEST SMTP");
        tips.SetToolTip(count,
            "POČET ZPRÁV (1–10000).\n" +
            "PROBLÉM „mimo rozsah“:\n• Zadejte celé číslo 1 až 10000\n• Oprava: pole Zprávy v sekci Parametry testu");
        tips.SetToolTip(interval,
            "INTERVAL ms – minimální mezera mezi starty odeslání.\n" +
            "0 = bez omezení (max. rychlost dle paralelismu).\n" +
            "PROBLÉM přetížení serveru (421/451):\n• Zvyšte interval (např. 200–1000)\n• Snižte Paralelismus");
        tips.SetToolTip(concurrency,
            "PARALELISMUS – max. souběžných SMTP spojení (1–20).\n" +
            "PROBLÉM timeout / 421:\n• Snižte na 1–2\n• Oprava: pole Paralelismus");
        tips.SetToolTip(retries,
            "OPAKOVÁNÍ při dočasné chybě 4xx / timeout (0–5).\n" +
            "5xx se neopakují (trvalá chyba).\n" +
            "PROBLÉM stále FAIL po retry:\n• Podívejte se do logu na kód 4xx/5xx\n• Opravte účet, příjemce nebo kvótu na serveru");
        tips.SetToolTip(batch,
            "BATCH / PAUSE – dávkový režim.\n" +
            "Po každé dávce se čeká pauza (šetří server).\n" +
            "PROBLÉM „pauza musí být 1–86400“:\n• Zapněte Batch a nastavte Velikost dávky + Pauza s");
        tips.SetToolTip(batchSize,
            "VELIKOST DÁVKY (1–1000), jen když je Batch zapnutý.\n" +
            "PROBLÉM neplatné číslo:\n• Zadejte 1–1000\n• Oprava: pole Velikost dávky");
        tips.SetToolTip(batchPause,
            "PAUZA mezi dávkami v sekundách (1–86400).\n" +
            "PROBLÉM „pauza 0“:\n• Minimum je 1 sekunda, nebo vypněte Batch");
        tips.SetToolTip(random,
            "RANDOM TEST DATA – náhodný předmět/jméno/tělo + Test ID.\n" +
            "Při zapnutí se Subject/Name/Body ignorují.\n" +
            "PROBLÉM chcete vlastní text:\n• Vypněte Random test data a vyplňte Subject a Body");
        tips.SetToolTip(testMode,
            "TEST MODE – odesílání jen na domény ze seznamu Povolené domény.\n" +
            "Chrání před odesláním na produkční adresy.\n" +
            "PROBLÉM „příjemce není v Allowed domains“:\n• Přidejte doménu do Povolené domény\n• Nebo vypněte Test mode (opatrně!)");
        tips.SetToolTip(domains,
            "POVOLENÉ DOMÉNY pro Test mode (čárkou).\n" +
            "Příklad: example.com, test.local\n" +
            "PROBLÉM „zadejte alespoň jednu doménu“:\n• Vyplňte toto pole, nebo vypněte Test mode\n• Oprava: pole Povolené domény");
        tips.SetToolTip(htmlBody,
            "HTML BODY – tělo se odešle jako HTML.\n" +
            "Jinak plain text.\n" +
            "PROBLÉM špatné zobrazení v klientovi:\n• Pro čistý text nechte HTML vypnuté");
        tips.SetToolTip(dryRun,
            "DRY-RUN – simulace BEZ reálného SMTP.\n" +
            "Ověří UI, statistiky a validaci bez odeslání.\n" +
            "PROBLÉM „neodchází pošta“:\n• Dry-run musí být VYPNUTÝ pro skutečné odeslání");
        tips.SetToolTip(subject,
            "SUBJECT – předmět zprávy (max 998 znaků).\n" +
            "Ignorováno při Random test data.\n" +
            "PROBLÉM „příliš dlouhý“:\n• Zkraťte text v poli Subject");
        tips.SetToolTip(name,
            "DISPLAY NAME – jméno u From (max 200 znaků).\n" +
            "PROBLÉM příliš dlouhé:\n• Zkraťte pole Display name");
        tips.SetToolTip(body,
            "BODY – tělo zprávy (max cca 10 MB textu).\n" +
            "Plain text nebo HTML dle checkboxu HTML body.\n" +
            "PROBLÉM prázdné / velké tělo:\n• Doplňte text, nebo zapněte Random test data");
        tips.SetToolTip(headers,
            "CUSTOM HEADERS – jen hlavičky začínající X-\n" +
            "Jeden řádek: X-Nazev: hodnota\n" +
            "PROBLÉM „nepovolená hlavička“:\n• Pouze X-... (ne From/To/Subject)\n• Bez znaků nového řádku v hodnotě\n• Oprava: pole Custom headers");
        tips.SetToolTip(attachments,
            "PŘÍLOHY – soubory ke každé zprávě.\n" +
            "Velké soubory: app rozhodne preload vs. disk (RAM).\n" +
            "PROBLÉM „příloha neexistuje“:\n• Soubor byl smazán/přesunut – odeberte ho (Remove) a přidejte znovu\n• Oprava: seznam Attachments + Add/Remove");
        tips.SetToolTip(addAttachment,
            "ADD – vybrat soubory jako přílohy.\n" +
            "PROBLÉM nejde vybrat:\n• Zkontrolujte oprávnění k souboru");
        tips.SetToolTip(removeAttachment,
            "REMOVE – odebrat vybrané přílohy ze seznamu.\n" +
            "Nejdřív klikněte na řádek v seznamu, pak Remove.");
        tips.SetToolTip(testBtn,
            "TEST SMTP – jen handshake (+ auth), bez odeslání zpráv.\n" +
            "Použijte PŘED SEND při problémech se spojením.\n" +
            "PROBLÉM timeout:\n• Server/port/firewall/TLS – upravte Server, Port, Zabezpečení");
        tips.SetToolTip(send,
            "SEND – spustí load test / odesílání.\n" +
            "Průběh: progress bar + log dole.\n" +
            "PROBLÉM validace před startem:\n• Přečtěte hlášku – uvádí které pole opravit\n• Nejdřív TEST SMTP, případně Dry-run");
        tips.SetToolTip(stop,
            "STOP – zruší běžící test.\n" +
            "Částečné výsledky (odesláno/selhalo) zůstanou v souhrnu.");
        tips.SetToolTip(log,
            "LOG – časové zprávy OK/FAIL a varování.\n" +
            "PROBLÉM hledáte chybu:\n• Řádky FAIL #n obsahují SMTP kód a nápovědu\n• 4xx = dočasné, 5xx = trvalé (opravit data/účet)");
        tips.SetToolTip(bar,
            "PROGRESS – průběh testu (odesláno + selhalo / celkem).\n" +
            "Při vysokém paralelismu se UI aktualizuje max. cca 8×/s.");
        tips.SetToolTip(stats,
            "STATISTIKY – odesláno, selhalo, ETA, po dokončení latence a throughput.\n" +
            "PROBLÉM nejasný výsledek:\n• Celý souhrn je i v logu (poslední řádky)");
        tips.SetToolTip(statusStrip,
            "STAVOVÝ ŘÁDEK – stručný stav aplikace (dole).");
        tips.SetToolTip(memInfo,
            "Limit paměti pro přílohy se počítá automaticky z dostupné RAM a paralelismu.\n" +
            "Zelená = preload do RAM, oranžová = čtení z disku.");
        tips.SetToolTip(netInfo,
            "Zobrazení síťového adaptéru, který Windows pravděpodobně použije pro odchozí SMTP.\n" +
            "Podrobný návod na změnu adaptéru se doplní po načtení (RefreshNetworkInfo).");
        tips.SetToolTip(stagePrep, "Fáze 1: aplikace připravena, vyplňte údaje.");
        tips.SetToolTip(stageValidate, "Fáze 2: kontrola formuláře a pravidel (e-mail, port, TLS…).");
        tips.SetToolTip(stageConnect, "Fáze 3: SMTP spojení / TEST SMTP.");
        tips.SetToolTip(stageSend, "Fáze 4: odesílání zpráv (load test).");
        tips.SetToolTip(stageDone, "Fáze 5: test dokončen nebo zastaven — souhrn ve statistikách a logu.");
        tips.SetToolTip(detailDone,
            "ZELENÁ = krok už proběhl nebo právě dokončená akce.\nČERVENÁ = chyba / zastavení na konkrétním místě.");
        tips.SetToolTip(detailNext,
            "ORANŽOVÁ = co má přijít dál.\nAž se krok stane, přesune se do zeleného řádku výše.");

        // Stejný tooltip i na textových popiscích (From, To, Server, …)
        void Mirror(Label? lbl, Control c)
        {
            if (lbl is null) return;
            tips.SetToolTip(lbl, tips.GetToolTip(c));
        }
        Mirror(lblFrom, from);
        Mirror(lblTo, to);
        Mirror(lblServer, smtp);
        Mirror(lblPort, port);
        Mirror(lblPortSec, port);
        Mirror(lblUser, user);
        Mirror(lblPass, pass);
        Mirror(lblDomains, domains);
        Mirror(lblSubject, subject);
        Mirror(lblName, name);
        Mirror(lblHeaders, headers);
        Mirror(lblBody, body);
        Mirror(lblAtt, attachments);
    }

    void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 7,
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // stages
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Barevný průběh + info o síťovém adaptéru
        var topPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        var stageFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        stageFlow.Controls.AddRange(new Control[] { stagePrep, Arrow(), stageValidate, Arrow(), stageConnect, Arrow(), stageSend, Arrow(), stageDone });
        topPanel.Controls.Add(stageFlow, 0, 0);
        topPanel.Controls.Add(detailDone, 0, 1);
        topPanel.Controls.Add(detailNext, 0, 2);
        topPanel.Controls.Add(netInfo, 0, 3);
        topPanel.Controls.Add(workerStatus, 0, 4);
        root.Controls.Add(topPanel, 0, 0);

        var gIdent = CreateGroup("Odesílatel a příjemci");
        var tIdent = TwoCol();
        lblFrom = AddLabeled(tIdent, 0, "From:", from);
        lblTo = AddLabeled(tIdent, 1, "To:", to);
        gIdent.Controls.Add(tIdent);
        root.Controls.Add(gIdent, 0, 1);

        var gSmtp = CreateGroup("SMTP server");
        var tSmtp = TwoCol();
        lblServer = AddLabeled(tSmtp, 0, "Server:", smtp);
        var smtpRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        port.Items.AddRange(new object[]
        {
            "25", "26", "465", "587", "2525", "2465", "2587", "3535", "8025", "1025", "10025", "443"
        });
        port.Text = "587";
        security.Items.AddRange(new object[] { "None", "STARTTLS", "Implicit TLS" });
        security.SelectedIndex = 1;
        lblPort = new Label { Text = "Port:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        smtpRow.Controls.Add(lblPort);
        smtpRow.Controls.Add(port);
        smtpRow.Controls.Add(security);
        smtpRow.Controls.Add(auth);
        smtpRow.Controls.Add(ignoreCert);
        lblPortSec = new Label { Text = "Port / TLS:", AutoSize = true, Anchor = AnchorStyles.Left };
        tSmtp.Controls.Add(lblPortSec, 0, 1);
        tSmtp.Controls.Add(smtpRow, 1, 1);
        lblUser = AddLabeled(tSmtp, 2, "Username:", user);
        lblPass = AddLabeled(tSmtp, 3, "Password:", pass);
        pass.UseSystemPasswordChar = true;
        gSmtp.Controls.Add(tSmtp);
        root.Controls.Add(gSmtp, 0, 2);

        var gRun = CreateGroup("Parametry testu");
        var runFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        count.Width = 65; interval.Width = 65; concurrency.Width = 45; retries.Width = 40;
        batchSize.Width = 55; batchPause.Width = 55;
        runFlow.Controls.AddRange(new Control[]
        {
            Lbl("Zprávy"), count, Lbl("Interval ms"), interval,
            Lbl("Paralelismus"), concurrency, Lbl("Opakování"), retries,
            batch, Lbl("Velikost dávky"), batchSize, Lbl("Pauza s"), batchPause,
            random, testMode, htmlBody, dryRun
        });
        var tRun = TwoCol();
        tRun.Controls.Add(new Label { Text = "Nastavení:", AutoSize = true }, 0, 0);
        tRun.Controls.Add(runFlow, 1, 0);
        lblDomains = AddLabeled(tRun, 1, "Povolené domény:", domains);
        gRun.Controls.Add(tRun);
        root.Controls.Add(gRun, 0, 3);

        var gContent = CreateGroup("Obsah zprávy");
        var tContent = TwoCol();
        lblSubject = AddLabeled(tContent, 0, "Subject:", subject);
        lblName = AddLabeled(tContent, 1, "Display name:", name);
        headers.PlaceholderText = "X-Custom: value";
        lblHeaders = AddLabeled(tContent, 2, "Custom headers:", headers);
        body.Height = 60;
        lblBody = AddLabeled(tContent, 3, "Body:", body);

        var attPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Height = 88 };
        attPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        attPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        attPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        attPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        attachments.Dock = DockStyle.Fill;
        attPanel.Controls.Add(attachments, 0, 0);
        var attBtns = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        attBtns.Controls.Add(addAttachment);
        attBtns.Controls.Add(removeAttachment);
        attPanel.Controls.Add(attBtns, 1, 0);
        attPanel.Controls.Add(memInfo, 0, 1);
        attPanel.SetColumnSpan(memInfo, 2);
        lblAtt = new Label { Text = "Attachments:", AutoSize = true };
        tContent.Controls.Add(lblAtt, 0, 4);
        tContent.Controls.Add(attPanel, 1, 4);
        gContent.Controls.Add(tContent);
        root.Controls.Add(gContent, 0, 4);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 4) };
        btnPanel.Controls.Add(testBtn);
        btnPanel.Controls.Add(send);
        btnPanel.Controls.Add(stop);
        var progPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Height = 70 };
        progPanel.Controls.Add(btnPanel, 0, 0);
        progPanel.Controls.Add(bar, 0, 1);
        progPanel.Controls.Add(stats, 0, 2);
        root.Controls.Add(progPanel, 0, 5);

        root.Controls.Add(log, 0, 6);

        statusStrip.Items.Add(statusLabel);
        statusStrip.Items.Add(netStatusLabel);
        Controls.Add(root);
        Controls.Add(statusStrip);
    }

    static GroupBox CreateGroup(string title) => new()
    {
        Text = title,
        Dock = DockStyle.Top,
        AutoSize = true,
        Padding = new Padding(8, 6, 8, 8),
        Margin = new Padding(0, 0, 0, 6)
    };

    static TableLayoutPanel TwoCol()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    Label AddLabeled(TableLayoutPanel t, int row, string label, Control c)
    {
        var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
        t.Controls.Add(lbl, 0, row);
        c.Dock = DockStyle.Fill;
        t.Controls.Add(c, 1, row);
        return lbl;
    }

    static Label Lbl(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(4, 6, 0, 0) };

    static Label Arrow() => new()
    {
        Text = "→",
        AutoSize = true,
        ForeColor = Color.Gray,
        Padding = new Padding(2, 4, 2, 4)
    };

    bool TryGetOptions(out MailTestOptions? options, out string error)
    {
        options = null;
        error = "";
        if (!int.TryParse(port.Text.Trim(), out var portVal) || portVal is < 1 or > 65535)
        {
            error = "Chyba v poli Port.\nVyberte port ze seznamu nebo zadejte číslo 1–65535.\n\nJak opravit: sekce SMTP server → Port (25/465/587/…).";
            return false;
        }
        if (!int.TryParse(count.Text, out var countVal) || countVal is < 1 or > 10000)
        {
            error = "Chyba v poli Zprávy (Messages).\nPovoleno 1–10000.\n\nJak opravit: sekce Parametry testu → Zprávy.";
            return false;
        }
        if (!int.TryParse(interval.Text, out var intervalVal) || intervalVal < 0)
        {
            error = "Chyba v poli Interval ms.\nZadejte 0 nebo kladné číslo (max 3600000).\n\nJak opravit: sekce Parametry testu → Interval ms.";
            return false;
        }
        if (!int.TryParse(concurrency.Text, out var concVal) || concVal is < 1 or > 20)
        {
            error = "Chyba v poli Paralelismus.\nPovoleno 1–20.\n\nJak opravit: sekce Parametry testu → Paralelismus (při problémech zkuste 1).";
            return false;
        }
        if (!int.TryParse(retries.Text, out var retriesVal) || retriesVal is < 0 or > 5)
        {
            error = "Chyba v poli Opakování (Retries).\nPovoleno 0–5.\n\nJak opravit: sekce Parametry testu → Opakování.";
            return false;
        }
        if (!int.TryParse(batchSize.Text, out var bsVal)) bsVal = 10;
        if (!int.TryParse(batchPause.Text, out var bpVal)) bpVal = 30;

        var att = attachments.Items.Cast<string>().ToList();
        options = new MailTestOptions(
            from.Text.Trim(),
            to.Text.Split(new[] { ';', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            smtp.Text.Trim(),
            portVal,
            security.SelectedIndex switch
            {
                0 => SmtpSecurity.None,
                2 => SmtpSecurity.ImplicitTls,
                _ => SmtpSecurity.StartTls
            },
            auth.Checked,
            user.Text,
            pass.Text,
            countVal,
            intervalVal,
            batch.Checked,
            bsVal,
            bpVal,
            concVal,
            subject.Text,
            body.Text,
            name.Text,
            random.Checked,
            testMode.Checked,
            domains.Text,
            htmlBody.Checked,
            att,
            Validation.ParseHeaders(headers.Text),
            ignoreCert.Checked,
            retriesVal,
            dryRun.Checked);
        return true;
    }

    static string FormatValidationError(string message)
    {
        // Doplní konkrétní „kde opravit“, pokud zpráva z Validation neobsahuje návod
        if (message.Contains("Jak opravit", StringComparison.OrdinalIgnoreCase))
            return message;
        var hint = message switch
        {
            var m when m.Contains("From", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: sekce Odesílatel → pole From.",
            var m when m.Contains("příjemc", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: sekce Odesílatel → pole To (a případně Povolené domény / Test mode).",
            var m when m.Contains("SMTP server", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: sekce SMTP server → pole Server.",
            var m when m.Contains("Port", StringComparison.OrdinalIgnoreCase) || m.Contains("TLS", StringComparison.OrdinalIgnoreCase) || m.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: sekce SMTP server → Port + rozbalovací Zabezpečení (465=Implicit TLS, 587=STARTTLS).",
            var m when m.Contains("uživatelsk", StringComparison.OrdinalIgnoreCase) || m.Contains("heslo", StringComparison.OrdinalIgnoreCase) || m.Contains("autentiz", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: zaškrtněte SMTP autentizace a vyplňte Username + Password (nebo autentizaci vypněte).",
            var m when m.Contains("domén", StringComparison.OrdinalIgnoreCase) || m.Contains("Test mode", StringComparison.OrdinalIgnoreCase) || m.Contains("Allowed", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: Parametry testu → Povolené domény, nebo vypněte Test mode.",
            var m when m.Contains("Příloha", StringComparison.OrdinalIgnoreCase) || m.Contains("příloha", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: seznam Attachments → Remove neplatné + Add znovu.",
            var m when m.Contains("hlavičk", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: Custom headers – pouze řádky „X-Nazev: hodnota“ bez Enter uvnitř hodnoty.",
            var m when m.Contains("dávk", StringComparison.OrdinalIgnoreCase) || m.Contains("Pauza", StringComparison.OrdinalIgnoreCase) || m.Contains("Batch", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: Parametry testu → Batch / Velikost dávky / Pauza s.",
            var m when m.Contains("Předmět", StringComparison.OrdinalIgnoreCase) || m.Contains("Subject", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: pole Subject (max 998 znaků).",
            var m when m.Contains("Tělo", StringComparison.OrdinalIgnoreCase) || m.Contains("Body", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: pole Body.",
            var m when m.Contains("Display name", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: pole Display name (max 200 znaků).",
            var m when m.Contains("Paralel", StringComparison.OrdinalIgnoreCase)
                => "\n\nKde: Parametry testu → Paralelismus (1–20).",
            _ => "\n\nTip: najetím myši na pole zobrazíte nápovědu s postupem opravy (ToolTip)."
        };
        return message + hint;
    }

    async Task TestSmtpAsync()
    {
        if (!TryGetOptions(out var o, out var err) || o is null)
        {
            MessageBox.Show(err, "SMTP test – neplatné údaje", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            SetStage(1);
            SetDetail("Kontroluji vyplněné údaje", "SMTP handshake se serverem");
            Validation.Validate(o);
            SetStage(2);
            SetDetail("Validace OK — připojuji k SMTP", "Výsledek testu spojení");
            statusLabel.Text = "Probíhá SMTP test…";
            var result = await SmtpConnectivityTester.TestAsync(o, CancellationToken.None);
            Log(result);
            SetStage(4);
            SetDetail("SMTP test OK", "Můžete spustit SEND (odesílání zpráv)");
            statusLabel.Text = "SMTP test dokončen";
            MessageBox.Show(result, "SMTP test", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ArgumentException ex)
        {
            SetStage(-1);
            SetDetail("Validace selhala: " + ex.Message, "Opravte pole podle hlášky", error: true);
            MessageBox.Show(FormatValidationError(ex.Message), "SMTP test – chyba validace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            statusLabel.Text = "SMTP test: chyba validace";
        }
        catch (Exception ex)
        {
            SetStage(-1);
            var explained = Validation.ExplainSmtpError(ex);
            SetDetail("SMTP test selhal: " + explained, "Upravte server/port/TLS nebo Ignore cert", error: true);
            MessageBox.Show(
                FormatValidationError(explained) +
                "\n\nCo zkusit:\n1) TEST SMTP znovu po úpravě Port/TLS\n2) Ignore certificate errors (jen test)\n3) Snížit paralelismus / zkontrolovat firewall",
                "SMTP test selhal", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            statusLabel.Text = "SMTP test selhal";
            Log("SMTP TEST CHYBA: " + explained);
        }
    }

    async Task SendAsync()
    {
        if (!TryGetOptions(out var o, out var err) || o is null)
        {
            MessageBox.Show(err, "Neplatné údaje", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            Validation.Validate(o);
            if (o.TestMode && !o.DryRun && MessageBox.Show(
                    "Test mode je aktivní. Odesílat pouze na autorizované testovací příjemce v Allowed domains?\n\n" +
                    "Pokud chcete posílat i jinam: vypněte Test mode, nebo doplňte doménu do pole Povolené domény.",
                    "Potvrzení", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            send.Enabled = testBtn.Enabled = false;
            stop.Enabled = true;
            cts = new CancellationTokenSource();
            bar.Maximum = o.MessageCount;
            bar.Value = 0;
            log.Clear();
            statusLabel.Text = o.DryRun ? "Dry-run běží…" : "Odesílání probíhá…";

            // LOG vždy; progress bar max ~8×/s. Detailní fáze + oranžová/zelená nápověda vždy.
            long lastUiTicks = 0;
            const long uiIntervalTicks = TimeSpan.TicksPerMillisecond * 125;
            var progress = new Progress<ProgressUpdate>(p =>
            {
                if (IsDisposed) return;
                Log(p.Status);
                var isFail = p.Status.StartsWith("FAIL", StringComparison.Ordinal);
                SetDetail(p.CurrentStep, p.NextStep, isFail);
                SetStageForPhase(p.TestPhase); 
                if (p.WorkerCount > 0 && p.WorkerId.HasValue)
                {
                    var step = p.MessageStep.HasValue ? TestStateMachine.MessageText(p.MessageStep.Value) : "—";
                    workerStatus.Text = $"Aktivní worker: W{p.WorkerId}/{p.WorkerCount} · zpráva #{p.MessageIndex?.ToString() ?? "—"} · {step}";
                    workerStatus.ForeColor = p.MessageStep is MessageStep.FailedFinal or MessageStep.Cancelled
                        ? Color.DarkRed
                        : p.MessageStep is MessageStep.Succeeded ? Color.DarkGreen : Color.DarkOrange;
                    UpdateWorkerBadge(p.WorkerId.Value, p.WorkerCount, p.MessageIndex, p.MessageStep);
                    UpdateMessageChecklist(p.MessageStep);
                    if (p.MessageStep == MessageStep.FailedFinal)
                    {
                        lastFailure.Text = $"Poslední chyba: zpráva #{p.MessageIndex?.ToString() ?? "—"} · {p.CurrentStep}";
                        lastFailure.ForeColor = Color.Firebrick;
                    }
                }
                else
                {
                    workerStatus.Text = $"Workers: {o.MaxConcurrency}";
                    workerStatus.ForeColor = Color.DimGray;
                }
                var now = DateTime.UtcNow.Ticks;
                var force = p.Sent + p.Failed >= o.MessageCount
                    || p.Status.StartsWith("PAUZA", StringComparison.Ordinal)
                    || p.Status.StartsWith("VAROV", StringComparison.Ordinal)
                    || isFail
                    || p.Status.StartsWith("Přílohy", StringComparison.Ordinal);
                if (!force && now - Interlocked.Read(ref lastUiTicks) < uiIntervalTicks)
                    return;
                Interlocked.Exchange(ref lastUiTicks, now);
                bar.Value = Math.Min(o.MessageCount, p.Sent + p.Failed);
                var etaStr = p.EtaSeconds.HasValue ? $"  ETA: {TimeSpan.FromSeconds(p.EtaSeconds.Value):mm\\:ss}" : "";
                stats.Text = $"Odesláno: {p.Sent}    Selhalo: {p.Failed}{etaStr}    {p.Status}";
            });

            var result = await new SmtpTestRunner().RunAsync(o, progress, cts.Token);
            if (IsDisposed) return;

            var prefix = result.Cancelled ? "Zastaveno" : "Hotovo";
            stats.Text = $"{prefix} — Odesláno: {result.Sent}, Selhalo: {result.Failed}, Čas: {result.Elapsed:hh\\:mm\\:ss}  " +
                         $"Průměr: {result.AvgLatencyMs:F0} ms  p50: {result.P50LatencyMs:F0}  p95: {result.P95LatencyMs:F0}  p99: {result.P99LatencyMs:F0}  " +
                         $"Propustnost: {result.ThroughputPerSec:F2} msg/s | aktivně: {result.ActiveThroughputPerSec:F2} msg/s | " +
                         $"Retry: {result.Retries} | 4xx: {result.Smtp4xx} | 5xx: {result.Smtp5xx} | timeouty: {result.Timeouts} | spojení: {result.PoolConnections}";
            Log($"{prefix}. {result.Sent}/{result.Requested} úspěšných. " +
                $"Průměr {result.AvgLatencyMs:F1} ms | p50 {result.P50LatencyMs:F1} | p95 {result.P95LatencyMs:F1} | p99 {result.P99LatencyMs:F1} | " +
                $"{result.ThroughputPerSec:F2} msg/s | aktivně {result.ActiveThroughputPerSec:F2} msg/s | " +
                $"Retry {result.Retries} | 4xx {result.Smtp4xx} | 5xx {result.Smtp5xx} | timeouty {result.Timeouts} | SMTP spojení {result.PoolConnections}");
            statusLabel.Text = $"{prefix} — {result.Sent} OK, {result.Failed} FAIL";
            if (result.Cancelled)
                SetDetail($"Zastaveno — OK {result.Sent}, FAIL {result.Failed}", "Upravte parametry a spusťte znovu SEND", error: true);
            else
                SetDetail($"Test dokončen — OK {result.Sent}, FAIL {result.Failed}, {result.ThroughputPerSec:F1} msg/s",
                    "Můžete změnit nastavení nebo spustit další test");
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed) return;
            Log("Zastaveno uživatelem (bez souhrnu).");
            stats.Text = "Zastaveno";
            statusLabel.Text = "Zastaveno uživatelem";
            SetDetail("Zastaveno uživatelem", "Spusťte SEND znovu nebo upravte nastavení", error: true);
        }
        catch (ArgumentException ex)
        {
            if (IsDisposed) return;
            MessageBox.Show(FormatValidationError(ex.Message), "Chyba validace – co opravit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Log("VALIDACE: " + ex.Message);
            statusLabel.Text = "Chyba validace";
            SetDetail("Validace selhala: " + ex.Message, "Opravte označené pole a zkuste znovu", error: true);
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            var explained = Validation.ExplainSmtpError(ex);
            MessageBox.Show(
                FormatValidationError(explained) +
                "\n\nDalší kroky:\n• Podrobnosti v logu dole\n• Zkuste TEST SMTP\n• Snižte Paralelismus / zvyšte Interval při 421",
                "Chyba při odesílání", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log("CHYBA: " + explained);
            statusLabel.Text = "Chyba";
            SetDetail("Chyba: " + explained, "TEST SMTP → upravte server/port/TLS → SEND", error: true);
        }
        finally
        {
            // cts se ruší i po zavření okna (viz OnFormClosing) – dispose je bezpečné vždy,
            // ale ovládací prvky sahat po disposnutém formuláři nesmíme.
            cts?.Dispose();
            cts = null;
            if (!IsDisposed)
            {
                send.Enabled = testBtn.Enabled = true;
                stop.Enabled = false;
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cts is { IsCancellationRequested: false })
        {
            var choice = MessageBox.Show(
                "Test právě běží. Zavřením okna se test přeruší. Opravdu zavřít?",
                "MailLoadTester", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            cts.Cancel();
        }
        base.OnFormClosing(e);
    }

    void SetStageForPhase(TestPhase phase)
    {
        // Jediné mapování Core stavu → pět vizuálních fází GUI.
        // Runner rozhoduje o fázi; GUI pouze vykresluje.
        var active = phase switch
        {
            TestPhase.Idle => 0,
            TestPhase.Validating => 1,
            TestPhase.PreparingAttachments => 1,
            TestPhase.ConnectingSmtp => 2,
            TestPhase.Sending => 3,
            TestPhase.BatchPause => 3,
            TestPhase.Completed => 5,
            TestPhase.Cancelled => -1,
            TestPhase.Failed => -1,
            _ => 0
        };
        SetStage(active);
        detailDone.Text = "Hotovo / právě: " + TestStateMachine.CurrentText(phase);
        detailNext.Text = "Následuje: " + TestStateMachine.NextText(phase);
    }

    void ResetWorkerUi(int workerCount)
    {
        workerPanel.Controls.Clear();
        for (var i = 1; i <= workerCount; i++)
        {
            workerPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(2),
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Gray,
                Text = $"W{i} · čeká"
            });
        }
        lastFailure.Text = "Poslední chyba: —";
        lastFailure.ForeColor = Color.Firebrick;
        UpdateMessageChecklist(MessageStep.Queued);
    }

    void UpdateWorkerBadge(int workerId, int workerCount, int? messageIndex, MessageStep? step)
    {
        while (workerPanel.Controls.Count < workerCount)
        {
            var label = new Label
            {
                AutoSize = true,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(2),
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Gray
            };
            workerPanel.Controls.Add(label);
        }
        for (var i = 0; i < workerPanel.Controls.Count; i++)
        {
            var l = (Label)workerPanel.Controls[i];
            if (i == workerId - 1)
            {
                var text = step.HasValue ? TestStateMachine.MessageText(step.Value) : "—";
                l.Text = $"W{workerId} · #{messageIndex?.ToString() ?? "—"} · {text}";
                l.BackColor = step is MessageStep.FailedFinal ? Color.FromArgb(255, 220, 220)
                    : step is MessageStep.Succeeded ? Color.FromArgb(198, 239, 206)
                    : Color.FromArgb(255, 243, 176);
                l.ForeColor = step is MessageStep.FailedFinal ? Color.DarkRed
                    : step is MessageStep.Succeeded ? Color.DarkGreen
                    : Color.DarkOrange;
            }
        }
    }

    void UpdateMessageChecklist(MessageStep? step)
    {
        var current = step;
        var index = current switch
        {
            MessageStep.Queued => 0,
            MessageStep.WaitingRateLimit => 1,
            MessageStep.RentingConnection => 2,
            MessageStep.BuildingMime => 3,
            MessageStep.SmtpSend or MessageStep.FailedTransient => 4,
            MessageStep.Succeeded => 5,
            MessageStep.FailedFinal or MessageStep.Cancelled => 4,
            _ => -1
        };
        for (var i = 0; i < messageChecklist.Length; i++)
        {
            var done = index >= 0 && i < index;
            var active = i == index;
            var text = messageChecklist[i].Text;
            var label = text.Length > 2 ? text[2..] : text;
            messageChecklist[i].Text = (done ? "✓ " : active ? "● " : "○ ") + label;
            messageChecklist[i].ForeColor = done ? Color.DarkGreen : active ? Color.DarkOrange : Color.Gray;
        }
        if (step is MessageStep.FailedFinal or MessageStep.Cancelled)
        {
            messageChecklist[4].Text = (step == MessageStep.Cancelled ? "✕ " : "✗ ") + "SMTP SEND";
            messageChecklist[4].ForeColor = Color.DarkRed;
        }
    }

    void Log(string text)
    {
        if (IsDisposed) return;
        if (log.InvokeRequired)
        {
            log.BeginInvoke(() => Log(text));
            return;
        }
        log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}");
    }
}
