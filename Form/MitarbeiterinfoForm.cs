using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaMi_Automatenclient.UI.Layout;

namespace TaMi_Automatenclient
{
    public class MitarbeiterinfoForm : KassenclientBaseForm
    {
        private Panel pnlNew;
        private RadioButton rbAll;
        private RadioButton rbEmployee;
        private ComboBox cboEmployee;
        private CheckBox chkGueltigBis;
        private DateTimePicker dtpGueltigBis;
        private TextBox txtInfo;
        private ModernGradientButton btnAdd;
        private ModernGradientButton btnArchive;
        private DataGridView gv;
        private CheckBox chkShowArchived;
        private readonly HashSet<string> _expandedGroups = new HashSet<string>(StringComparer.Ordinal);

        private Panel footerPanel;

        private const byte RelTypMitarbeiterinfo = 12;

        [Flags]
        private enum NotizFlags : short
        {
            KEINE = 0,
            ARCHIVED = 0x01,
            DELETED = 0x02,
            READ = 0x04,
            EDITADMINONLY = 0x08,
            EDITSUEONLY = 0x10,
            SHOWDASHBOARD = 0x40
        }

        public MitarbeiterinfoForm()
        {
            SetupDefaultForm("MitarbeiterinfoForm", "Mitarbeiterinfo", new Size(980, 720));
            AddHeaderPanel(Text, true, true, true);
            BuildUi();
            Shown += async (s, e) => await LoadAsync();
        }

        private void BuildUi()
        {
            pnlNew = new Panel
            {
                Left = 12,
                Top = UiTheme.HeaderHeight + 12,
                Width = ClientSize.Width - 24,
                Height = 140,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };

            var lblNew = new Label
            {
                Text = "Neue Mitarbeiterinfo",
                Left = 0,
                Top = 0,
                Width = 240,
                Height = 20,
                Font = new Font("Segoe UI Variable", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            rbAll = new RadioButton { Text = "Für alle", Left = 0, Top = 30, AutoSize = true, Checked = true };
            rbEmployee = new RadioButton { Text = "Für Mitarbeiter:", Left = 90, Top = 30, AutoSize = true };
            cboEmployee = new ComboBox { Left = 210, Top = 28, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };

            chkGueltigBis = new CheckBox { Text = "Bestätigung erforderlich", Left = 550, Top = 30, AutoSize = true };
            var lblGueltigBis = new Label { Text = "Gültig bis:", Left = 735, Top = 31, Width = 65, Height = 22 };
            dtpGueltigBis = new DateTimePicker { Left = 800, Top = 27, Width = 115, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Enabled = true };
            chkGueltigBis.CheckedChanged += (s, e) => { try { dtpGueltigBis.Enabled = !chkGueltigBis.Checked; } catch { } };

            rbAll.CheckedChanged += (s, e) => { try { cboEmployee.Enabled = rbEmployee.Checked; } catch { } };
            rbEmployee.CheckedChanged += (s, e) => { try { cboEmployee.Enabled = rbEmployee.Checked; } catch { } };

            txtInfo = new TextBox
            {
                Left = 0,
                Top = 62,
                Width = pnlNew.Width - 140,
                Height = 70,
                Multiline = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ScrollBars = ScrollBars.Vertical
            };

            btnAdd = new ModernGradientButton
            {
                Text = "Anlegen",
                Width = 120,
                Height = 42,
                Left = pnlNew.Width - 120,
                Top = 90,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                GradientStart = UiTheme.SuccessStart,
                GradientEnd = UiTheme.SuccessEnd,
                Font = new Font("Segoe UI Variable", 10F, FontStyle.Bold)
            };
            btnAdd.Click += async (s, e) => await AddInfoAsync();

            pnlNew.Controls.Add(lblNew);
            pnlNew.Controls.Add(rbAll);
            pnlNew.Controls.Add(rbEmployee);
            pnlNew.Controls.Add(cboEmployee);
            pnlNew.Controls.Add(chkGueltigBis);
            pnlNew.Controls.Add(lblGueltigBis);
            pnlNew.Controls.Add(dtpGueltigBis);
            pnlNew.Controls.Add(txtInfo);
            pnlNew.Controls.Add(btnAdd);
            Controls.Add(pnlNew);

            gv = new DataGridView
            {
                Left = 12,
                Top = pnlNew.Bottom + 12,
                Width = ClientSize.Width - 24,
                Height = ClientSize.Height - (pnlNew.Bottom + 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            gv.EnableHeadersVisualStyles = true;
            gv.RowHeadersVisible = false;
            gv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            gv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            gv.ColumnHeadersHeight = 30;
            gv.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 2, 0, 2);
            gv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            gv.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            gv.RowTemplate.Height = 22;
            gv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            gv.DefaultCellStyle.BackColor = Color.White;
            gv.DefaultCellStyle.ForeColor = Color.Black;
            gv.DefaultCellStyle.SelectionBackColor = UiTheme.PrimaryStart;
            gv.DefaultCellStyle.SelectionForeColor = Color.White;
            gv.AlternatingRowsDefaultCellStyle.SelectionBackColor = UiTheme.PrimaryStart;
            gv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            gv.GridColor = Color.FromArgb(220, 225, 230);

            AddCol("Expand", "", 32);
            AddCol("Datum", "Datum", 130);
            AddCol("Mitarbeiter", "Mitarbeiter", 200);
            AddCol("GueltigBis", "Gültig bis", 110);
            AddCol("Text", "Text", 0, true);

            gv.CellFormatting += Gv_CellFormatting;
            gv.CellClick += Gv_CellClick;
            gv.SelectionChanged += (s, e) => UpdateArchiveButtonState();

            Controls.Add(gv);

            BuildFooter();
        }

        private void BuildFooter()
        {
            footerPanel = new Panel
            {
                Left = 0,
                Top = ClientSize.Height - 56,
                Width = ClientSize.Width,
                Height = 56,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Color.FromArgb(245, 248, 255)
            };
            Controls.Add(footerPanel);

            btnArchive = new ModernGradientButton
            {
                Text = "Archivieren",
                Width = 160,
                Height = 40,
                Left = 24,
                Top = 8,
                GradientStart = UiTheme.SecondaryStart,
                GradientEnd = UiTheme.SecondaryEnd,
                Font = new Font("Segoe UI Variable", 10F, FontStyle.Bold),
                Enabled = false
            };
            btnArchive.Click += async (s, e) => await ArchiveSelectedAsync();
            footerPanel.Controls.Add(btnArchive);

            chkShowArchived = new CheckBox
            {
                Text = "Archiviert/abgelaufen anzeigen",
                AutoSize = true,
                Left = btnArchive.Right + 18,
                Top = 18,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            chkShowArchived.CheckedChanged += async (s, e) => await LoadAsync();
            footerPanel.Controls.Add(chkShowArchived);

            try
            {
                gv.Height = ClientSize.Height - (footerPanel.Height + 24 + gv.Top);
                Resize += (s, e) =>
                {
                    try
                    {
                        footerPanel.Top = ClientSize.Height - footerPanel.Height;
                        gv.Height = ClientSize.Height - (footerPanel.Height + 24 + gv.Top);
                    }
                    catch { }
                };
            }
            catch { }
        }

        private void AddCol(string prop, string header, int width, bool fill = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                Name = prop,
                HeaderText = header,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                Width = fill ? 200 : width
            };
            gv.Columns.Add(col);
        }

        private async Task LoadAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    await LoadEmployeesAsync(db);
                    var dt = await LoadNotizenAsync(db, chkShowArchived != null && chkShowArchived.Checked);
                    gv.DataSource = BuildView(dt);
                }

                TryApplyColumnLayout();
                UpdateArchiveButtonState();
            }
            catch
            {
                gv.DataSource = null;
            }
        }

        private DataTable BuildView(DataTable raw)
        {
            var view = new DataTable();
            view.Columns.Add("Expand", typeof(string));
            view.Columns.Add("NotizID", typeof(int));
            view.Columns.Add("NotizIDs", typeof(string));
            view.Columns.Add("Datum", typeof(DateTime));
            view.Columns.Add("Mitarbeiter", typeof(string));
            view.Columns.Add("GueltigBis", typeof(DateTime));
            view.Columns.Add("IsArchived", typeof(bool));
            view.Columns.Add("IsRead", typeof(bool));
            view.Columns.Add("IsGroup", typeof(bool));
            view.Columns.Add("GroupKey", typeof(string));
            view.Columns.Add("Text", typeof(string));

            if (raw == null) return view;

            var groups = new Dictionary<string, List<DataRow>>(StringComparer.Ordinal);

            foreach (DataRow r in raw.Rows)
            {
                string text = string.Empty;
                try { if (raw.Columns.Contains("Text") && r["Text"] != DBNull.Value) text = Convert.ToString(r["Text"]) ?? string.Empty; } catch { text = string.Empty; }

                string key = text.Trim();
                if (string.IsNullOrEmpty(key)) key = "__EMPTY__";

                if (!groups.ContainsKey(key)) groups[key] = new List<DataRow>();
                groups[key].Add(r);
            }

            foreach (var g in groups)
            {
                var rows = g.Value;
                rows.Sort((a, b) =>
                {
                    DateTime da = DateTime.MinValue;
                    DateTime db = DateTime.MinValue;
                    try { if (raw.Columns.Contains("Datum") && a["Datum"] != DBNull.Value) da = Convert.ToDateTime(a["Datum"]); } catch { }
                    try { if (raw.Columns.Contains("Datum") && b["Datum"] != DBNull.Value) db = Convert.ToDateTime(b["Datum"]); } catch { }
                    return db.CompareTo(da);
                });

                bool makeGroup = rows.Count > 1;
                if (makeGroup)
                {
                    var first = rows[0];
                    int firstId = 0;
                    DateTime datum = DateTime.MinValue;
                    string text = string.Empty;

                    try { if (raw.Columns.Contains("NotizID") && first["NotizID"] != DBNull.Value) firstId = Convert.ToInt32(first["NotizID"]); } catch { }
                    try { if (raw.Columns.Contains("Datum") && first["Datum"] != DBNull.Value) datum = Convert.ToDateTime(first["Datum"]); } catch { }
                    try { if (raw.Columns.Contains("Text") && first["Text"] != DBNull.Value) text = Convert.ToString(first["Text"]) ?? string.Empty; } catch { }

                    var ids = new List<string>();
                    bool groupArchived = true;
                    bool groupRead = true;

                    foreach (var r in rows)
                    {
                        try { if (raw.Columns.Contains("NotizID") && r["NotizID"] != DBNull.Value) ids.Add(Convert.ToString(r["NotizID"])); } catch { }

                        bool isArchived = false;
                        bool isRead = false;
                        try
                        {
                            if (raw.Columns.Contains("Flags") && r["Flags"] != DBNull.Value)
                            {
                                short flags = Convert.ToInt16(r["Flags"]);
                                var ff = (NotizFlags)flags;
                                isArchived = ((ff & NotizFlags.ARCHIVED) == NotizFlags.ARCHIVED);
                                isRead = ((ff & NotizFlags.READ) == NotizFlags.READ);
                            }
                        }
                        catch { }

                        groupArchived = groupArchived && isArchived;
                        groupRead = groupRead && isRead;
                    }

                    bool expanded = _expandedGroups.Contains(g.Key);
                    view.Rows.Add(expanded ? "▼" : "▶", firstId, string.Join(",", ids), datum, "Alle (" + rows.Count + ")", DBNull.Value, groupArchived, groupRead, true, g.Key, text);

                    if (!expanded) continue;
                }

                foreach (var r in rows)
                {
                    int id = 0;
                    DateTime datum = DateTime.MinValue;
                    string relId = string.Empty;
                    string text = string.Empty;

                    try { if (raw.Columns.Contains("NotizID") && r["NotizID"] != DBNull.Value) id = Convert.ToInt32(r["NotizID"]); } catch { id = 0; }
                    try { if (raw.Columns.Contains("Datum") && r["Datum"] != DBNull.Value) datum = Convert.ToDateTime(r["Datum"]); } catch { datum = DateTime.MinValue; }
                    try { if (raw.Columns.Contains("RelID") && r["RelID"] != DBNull.Value) relId = Convert.ToString(r["RelID"]) ?? string.Empty; } catch { relId = string.Empty; }
                    try { if (raw.Columns.Contains("Text") && r["Text"] != DBNull.Value) text = Convert.ToString(r["Text"]) ?? string.Empty; } catch { text = string.Empty; }

                    DateTime? gb = null;
                    try
                    {
                        if (raw.Columns.Contains("GueltigBis") && r["GueltigBis"] != DBNull.Value)
                        {
                            var d = Convert.ToDateTime(r["GueltigBis"]);
                            if (d.Date > new DateTime(1900, 1, 1)) gb = d;
                        }
                    }
                    catch { gb = null; }

                    bool isArchived = false;
                    bool isRead = false;
                    try
                    {
                        if (raw.Columns.Contains("Flags") && r["Flags"] != DBNull.Value)
                        {
                            short flags = 0;
                            try { flags = Convert.ToInt16(r["Flags"]); } catch { flags = 0; }
                            var ff = (NotizFlags)flags;
                            isArchived = ((ff & NotizFlags.ARCHIVED) == NotizFlags.ARCHIVED);
                            isRead = ((ff & NotizFlags.READ) == NotizFlags.READ);
                        }
                    }
                    catch { isArchived = false; isRead = false; }

                    string mitarbeiter = ResolveRelIdToName(relId);
                    if (isRead)
                    {
                        if (!string.IsNullOrEmpty(mitarbeiter) && !mitarbeiter.StartsWith("✓ ", StringComparison.Ordinal)) mitarbeiter = "✓ " + mitarbeiter;
                        else if (string.IsNullOrEmpty(mitarbeiter)) mitarbeiter = "✓";
                    }

                    view.Rows.Add("", id, id.ToString(), datum, mitarbeiter, (object)gb ?? DBNull.Value, isArchived, isRead, false, g.Key, text);
                }
            }

            return view;
        }

        private async void Gv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (gv == null || !string.Equals(gv.Columns[e.ColumnIndex].Name, "Expand", StringComparison.OrdinalIgnoreCase)) return;

                var drv = gv.Rows[e.RowIndex].DataBoundItem as DataRowView;
                if (drv == null) return;

                bool isGroup = drv.Row.Table.Columns.Contains("IsGroup") && drv.Row["IsGroup"] != DBNull.Value && Convert.ToBoolean(drv.Row["IsGroup"]);
                if (!isGroup) return;

                string key = Convert.ToString(drv.Row["GroupKey"]) ?? string.Empty;
                if (_expandedGroups.Contains(key)) _expandedGroups.Remove(key);
                else _expandedGroups.Add(key);

                await LoadAsync();
            }
            catch { }
        }

        private void UpdateArchiveButtonState()
        {
            try { if (btnArchive != null) btnArchive.Enabled = gv != null && gv.CurrentRow != null && gv.CurrentRow.DataBoundItem != null; } catch { }
        }

        private string ResolveRelIdToName(string relId)
        {
            if (string.IsNullOrWhiteSpace(relId)) return string.Empty;
            relId = relId.Trim();
            if (relId == "-1") return "Alle";
            int pid;
            if (!int.TryParse(relId, out pid) || pid <= 0) return relId;

            try
            {
                if (cboEmployee != null && cboEmployee.DataSource is DataTable dt)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        try
                        {
                            if (Convert.ToInt32(r["PID"]) == pid)
                                return Convert.ToString(r["Name"]) ?? ("PID " + pid);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return "PID " + pid;
        }

        private async Task LoadEmployeesAsync(DatabaseHelperKassen db)
        {
            if (cboEmployee == null) return;
            try
            {
                var dt = await db.GetActivePersonalAsync();
                cboEmployee.DisplayMember = "Name";
                cboEmployee.ValueMember = "PID";
                cboEmployee.DataSource = dt;
                if (cboEmployee.Items.Count > 0) cboEmployee.SelectedIndex = 0;
            }
            catch
            {
                try
                {
                    cboEmployee.DataSource = null;
                    cboEmployee.Items.Clear();
                    cboEmployee.Items.Add("— Laden fehlgeschlagen —");
                    cboEmployee.SelectedIndex = 0;
                }
                catch { }
            }
        }

        private async Task AddInfoAsync()
        {
            try
            {
                var text = txtInfo != null ? (txtInfo.Text ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show(this, "Bitte Text eingeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool bestaetigungErforderlich = chkGueltigBis != null && chkGueltigBis.Checked;

                using (var db = new DatabaseHelperKassen())
                {
                    DateTime? gb = null;
                    if (!bestaetigungErforderlich)
                    {
                        try { gb = dtpGueltigBis.Value.Date; } catch { gb = null; }
                    }

                    if (rbAll != null && rbAll.Checked && bestaetigungErforderlich)
                    {
                        int anzahl = 0;
                        try
                        {
                            var personal = await db.GetActivePersonalAsync();
                            anzahl = personal == null ? 0 : personal.Rows.Count;
                        }
                        catch { anzahl = 0; }

                        var confirm = MessageBox.Show(
                            this,
                            "Wollen Sie wirklich für alle Mitarbeiter (" + anzahl + " Mitarbeiter) einen Eintrag erstellen?\r\n\r\nAchtung: Dies erzeugt große Datenmengen in der Datenbank.",
                            "Bestätigung",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (confirm != DialogResult.Yes) return;

                        await db.InsertNotizFuerAlleAktiveMitarbeiterAsync(RelTypMitarbeiterinfo, DateTime.Now, text, 0);
                    }
                    else
                    {
                        string relId;
                        if (rbEmployee != null && rbEmployee.Checked)
                        {
                            int pid = 0;
                            try { if (cboEmployee != null && cboEmployee.SelectedValue != null) pid = Convert.ToInt32(cboEmployee.SelectedValue); } catch { pid = 0; }
                            if (pid <= 0)
                            {
                                MessageBox.Show(this, "Bitte Mitarbeiter auswählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }
                            relId = pid.ToString();
                        }
                        else
                        {
                            relId = "-1";
                        }

                        await db.InsertNotizAsync(RelTypMitarbeiterinfo, relId, DateTime.Now, text, 0, gb);
                    }
                }

                try { txtInfo.Clear(); } catch { }
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler beim Anlegen:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static async Task<DataTable> LoadNotizenAsync(DatabaseHelperKassen db, bool includeArchived)
        {
            var result = await db.GetNotizenByRelTypAsync(RelTypMitarbeiterinfo, includeArchived);
            return FilterAndSort(result, includeArchived);
        }

        private static DataTable FilterAndSort(DataTable dt, bool includeArchived)
        {
            if (dt == null) dt = new DataTable();

            if (!dt.Columns.Contains("Datum")) dt.Columns.Add("Datum", typeof(DateTime));
            if (!dt.Columns.Contains("RelID")) dt.Columns.Add("RelID", typeof(string));
            if (!dt.Columns.Contains("Text")) dt.Columns.Add("Text", typeof(string));

            // Filter archived/deleted
            try
            {
                if (dt.Columns.Contains("Flags"))
                {
                    for (int i = dt.Rows.Count - 1; i >= 0; i--)
                    {
                        var r = dt.Rows[i];
                        short flags = 0;
                        try { if (r["Flags"] != DBNull.Value) flags = Convert.ToInt16(r["Flags"]); } catch { flags = 0; }
                        var f = (NotizFlags)flags;
                        if (!includeArchived && (f & NotizFlags.ARCHIVED) == NotizFlags.ARCHIVED) { dt.Rows.RemoveAt(i); continue; }
                        if ((f & NotizFlags.DELETED) == NotizFlags.DELETED) { dt.Rows.RemoveAt(i); continue; }
                        if (!includeArchived && (f & NotizFlags.READ) == NotizFlags.READ) { dt.Rows.RemoveAt(i); continue; }

                        if (!includeArchived)
                        {
                            try
                            {
                                if (dt.Columns.Contains("GueltigBis") && r["GueltigBis"] != DBNull.Value)
                                {
                                    var gb = Convert.ToDateTime(r["GueltigBis"]).Date;
                                    if (gb > new DateTime(1900, 1, 1) && gb < DateTime.Today)
                                    {
                                        dt.Rows.RemoveAt(i);
                                        continue;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // Sort newest first
            try
            {
                var dv = dt.DefaultView;
                dv.Sort = "Datum DESC";
                return dv.ToTable();
            }
            catch { return dt; }
        }

        private void Gv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;

                var col = grid.Columns[e.ColumnIndex];
                var name = col.DataPropertyName ?? col.Name;

                try
                {
                    if (grid.Rows[e.RowIndex].DataBoundItem is DataRowView drvStyle && drvStyle.Row.Table.Columns.Contains("IsGroup") && drvStyle.Row["IsGroup"] != DBNull.Value && Convert.ToBoolean(drvStyle.Row["IsGroup"]))
                        grid.Rows[e.RowIndex].DefaultCellStyle.Font = new Font(grid.DefaultCellStyle.Font, FontStyle.Bold);
                }
                catch { }

                if (string.Equals(name, "Datum", StringComparison.OrdinalIgnoreCase))
                {
                    if (e.Value == null || e.Value == DBNull.Value) { e.Value = string.Empty; e.FormattingApplied = true; return; }
                    DateTime d;
                    if (DateTime.TryParse(Convert.ToString(e.Value), out d))
                    {
                        e.Value = d.ToString("dd.MM.yyyy HH:mm");
                        e.FormattingApplied = true;
                    }
                }

                if (string.Equals(name, "GueltigBis", StringComparison.OrdinalIgnoreCase))
                {
                    var isArchived = false;
                    var isRead = false;
                    try
                    {
                        if (grid.Rows[e.RowIndex].DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("IsArchived"))
                            isArchived = drv.Row["IsArchived"] != DBNull.Value && Convert.ToBoolean(drv.Row["IsArchived"]);
                        if (grid.Rows[e.RowIndex].DataBoundItem is DataRowView drv2 && drv2.Row.Table.Columns.Contains("IsRead"))
                            isRead = drv2.Row["IsRead"] != DBNull.Value && Convert.ToBoolean(drv2.Row["IsRead"]);
                    }
                    catch { isArchived = false; isRead = false; }

                    var suffix = (isArchived ? " (Archiviert)" : string.Empty) + (isRead ? " (Gelesen)" : string.Empty);

                    if (e.Value == null || e.Value == DBNull.Value)
                    {
                        e.Value = "-" + suffix;
                        e.FormattingApplied = true;
                        return;
                    }
                    DateTime d;
                    if (DateTime.TryParse(Convert.ToString(e.Value), out d))
                    {
                        if (d.Date <= new DateTime(1900, 1, 1)) e.Value = "-" + suffix;
                        else e.Value = d.ToString("dd.MM.yyyy") + suffix;
                        e.FormattingApplied = true;
                    }
                }
            }
            catch { }
        }

        private async Task ArchiveSelectedAsync()
        {
            try
            {
                if (gv == null || gv.CurrentRow == null) return;
                var drv = gv.CurrentRow.DataBoundItem as DataRowView;
                if (drv == null) return;
                var row = drv.Row;

                var ids = new List<int>();
                bool isGroup = false;
                try { isGroup = row.Table.Columns.Contains("IsGroup") && row["IsGroup"] != DBNull.Value && Convert.ToBoolean(row["IsGroup"]); } catch { isGroup = false; }

                if (isGroup && row.Table.Columns.Contains("NotizIDs"))
                {
                    var rawIds = Convert.ToString(row["NotizIDs"]) ?? string.Empty;
                    foreach (var part in rawIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int parsed;
                        if (int.TryParse(part.Trim(), out parsed) && parsed > 0) ids.Add(parsed);
                    }
                }
                else
                {
                    if (!row.Table.Columns.Contains("NotizID")) return;
                    int id = 0;
                    try { if (row["NotizID"] != DBNull.Value) id = Convert.ToInt32(row["NotizID"]); } catch { id = 0; }
                    if (id > 0) ids.Add(id);
                }

                if (ids.Count == 0) return;

                string msg = ids.Count == 1 ? "Nachricht wirklich archivieren?" : "Gruppe mit " + ids.Count + " Nachrichten wirklich archivieren?";
                if (MessageBox.Show(this, msg, "Bestätigung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                using (var db = new DatabaseHelperKassen())
                {
                    foreach (var id in ids)
                        await db.ArchiveNotizAsync(id);
                }
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler beim Archivieren:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // === INI Persistenz für Spaltenbreiten + Anzeigeindex ===
        private string GetIniPath()
        {
            try
            {
                var path = AppSettings.IniPath;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }
            catch { }
            try
            {
                var exe = Application.ExecutablePath;
                var dir = Path.GetDirectoryName(exe);
                return Path.Combine(dir ?? ".", "TaMi-Automatenclient.ini");
            }
            catch { return "TaMi-Automatenclient.ini"; }
        }

        private string GetIniKey()
        {
            return "MitarbeiterinfoForm.Columns";
        }

        private void TryApplyColumnLayout()
        {
            try
            {
                if (gv == null || gv.Columns == null || gv.Columns.Count == 0) return;

                foreach (var name in new[] { "NotizID", "NotizIDs", "IsArchived", "IsRead", "IsGroup", "GroupKey" })
                {
                    if (gv.Columns.Contains(name)) gv.Columns[name].Visible = false;
                }

                if (gv.Columns.Contains("Expand"))
                {
                    gv.Columns["Expand"].Width = 32;
                    gv.Columns["Expand"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    gv.Columns["Expand"].Resizable = DataGridViewTriState.False;
                }

                var ini = GetIniPath();
                if (!File.Exists(ini)) return;
                var key = GetIniKey();
                var lines = File.ReadAllLines(ini);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    if (!string.Equals(parts[0].Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;

                    var def = parts[1].Trim();
                    var cols = def.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var map = new Dictionary<string, (int displayIndex, int width)>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in cols)
                    {
                        // format: Name:DisplayIndex:Width
                        var seg = c.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (seg.Length < 3) continue;
                        int di, w;
                        if (!int.TryParse(seg[1], out di)) continue;
                        if (!int.TryParse(seg[2], out w)) continue;
                        map[seg[0]] = (di, w);
                    }

                    foreach (DataGridViewColumn col in gv.Columns)
                    {
                        if (col == null) continue;
                        if (map.TryGetValue(col.Name, out var v) || map.TryGetValue(col.DataPropertyName, out v))
                        {
                            try { col.DisplayIndex = Math.Max(0, v.displayIndex); } catch { }
                            try { col.Width = Math.Max(40, v.width); } catch { }
                        }
                    }

                    break;
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            TrySaveColumnLayout();
            base.OnFormClosing(e);
        }

        private void TrySaveColumnLayout()
        {
            try
            {
                if (gv == null || gv.Columns == null || gv.Columns.Count == 0) return;
                var parts = new List<string>();
                foreach (DataGridViewColumn col in gv.Columns)
                {
                    if (col == null) continue;
                    parts.Add(col.Name + ":" + col.DisplayIndex.ToString() + ":" + col.Width.ToString());
                }
                var line = GetIniKey() + "=" + string.Join(",", parts);
                var ini = GetIniPath();
                var lines = new List<string>();
                if (File.Exists(ini)) lines.AddRange(File.ReadAllLines(ini));
                bool replaced = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(GetIniKey() + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = line;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced) lines.Add(line);
                File.WriteAllLines(ini, lines);
            }
            catch { }
        }
    }
}
