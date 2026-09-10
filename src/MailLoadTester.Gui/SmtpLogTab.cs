using System.ComponentModel;
using System.Reflection;

namespace MailLoadTester.Gui;

/// <summary>
/// Výkonný log SMTP odpovědí v DataGridView.
/// - Thread-safe: lze volat z worker vláken (BeginInvoke).
/// - Limit 1000 řádků – starší se odstraňují (ochrana paměti při load testu).
/// - DoubleBuffered – méně blikání při rychlém přírůstku.
/// - Batching: při velmi rychlém logování se UI nezahlcuje každým řádkem zvlášť.
/// </summary>
public sealed class SmtpLogTab : UserControl
{
    private readonly DataGridView _grid;
    private readonly object _queueLock = new();
    private readonly Queue<LogItem> _pending = new();
    private readonly System.Windows.Forms.Timer _flushTimer;
    private const int MaxRows = 1000;
    private const int MaxBatchPerTick = 50;

    private readonly record struct LogItem(string Time, string Email, string Status, string Message, Color StatusColor);

    public SmtpLogTab()
    {
        Dock = DockStyle.Fill;
        _grid = CreateGrid();
        Controls.Add(_grid);

        // Dávkové vykreslení ~20×/s místo tisíců Invoke za sekundu
        _flushTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _flushTimer.Tick += (_, _) => FlushPending();
        _flushTimer.Start();

        // Uvolnění při dispose
        Disposed += (_, _) =>
        {
            _flushTimer.Stop();
            _flushTimer.Dispose();
        };
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(224, 224, 224),
            EnableHeadersVisualStyles = false,
            RowTemplate = { Height = 24 },
            // VirtualMode = false – pro max 1000 řádků stačí standardní režim
        };

        // Double buffering přes reflexi (veřejná vlastnost není na DataGridView)
        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
            null, grid, new object[] { true });

        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 45, 48);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 32;

        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 232, 255);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Time", HeaderText = "Čas", Width = 72,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Email", HeaderText = "Cíl", Width = 160 });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status", HeaderText = "Status", Width = 90,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Message", HeaderText = "Odpověď serveru",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        return grid;
    }

    /// <summary>
    /// Thread-safe zápis z libovolného vlákna. Neblokuje worker (fronta + timer).
    /// </summary>
    public void AddLogEntry(string email, string status, string message, Color statusColor)
    {
        var item = new LogItem(
            DateTime.Now.ToString("HH:mm:ss"),
            email ?? "",
            status ?? "",
            Truncate(message, 500),
            statusColor);

        lock (_queueLock)
        {
            // Ochrana: při extrémní rychlosti nepěstovat neomezenou frontu
            if (_pending.Count > 2000)
            {
                // Zahodit nejstarší čekající
                while (_pending.Count > 1000)
                    _pending.Dequeue();
            }
            _pending.Enqueue(item);
        }
    }

    /// <summary>Smazat všechny řádky (např. při startu nového testu).</summary>
    public void ClearLog()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(ClearLog);
            return;
        }
        lock (_queueLock) _pending.Clear();
        _grid.Rows.Clear();
    }

    private void FlushPending()
    {
        if (IsDisposed || !_grid.IsHandleCreated) return;

        List<LogItem> batch;
        lock (_queueLock)
        {
            if (_pending.Count == 0) return;
            batch = new List<LogItem>(MaxBatchPerTick);
            while (batch.Count < MaxBatchPerTick && _pending.Count > 0)
                batch.Add(_pending.Dequeue());
        }

        // Suspend layout při hromadném insertu
        _grid.SuspendLayout();
        try
        {
            foreach (var item in batch)
            {
                if (_grid.Rows.Count >= MaxRows)
                    _grid.Rows.RemoveAt(0);

                var idx = _grid.Rows.Add(item.Time, item.Email, item.Status, item.Message);
                var cell = _grid.Rows[idx].Cells["Status"];
                cell.Style.ForeColor = item.StatusColor;
                cell.Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }

            if (_grid.RowCount > 0)
            {
                try { _grid.FirstDisplayedScrollingRowIndex = _grid.RowCount - 1; }
                catch { /* grid může být v přechodném stavu */ }
            }
        }
        finally
        {
            _grid.ResumeLayout();
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\r', ' ').Replace('\n', ' ');
        return s.Length <= max ? s : s[..max] + "…";
    }
}
