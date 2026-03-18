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

            chkGueltigBis = new CheckBox { Text = "Gültig bis:", Left = 550, Top = 30, AutoSize = true };
            dtpGueltigBis = new DateTimePicker { Left = 635, Top = 27, Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Enabled = false };
            chkGueltigBis.CheckedChanged += (s, e) => { try { dtpGueltigBis.Enabled = chkGueltigBis.Checked; } catch { } };

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

            AddCol("Datum", "Datum", 130);
            AddCol("Mitarbeiter", "Mitarbeiter", 200);
            AddCol("GueltigBis", "Gültig bis", 110);
            AddCol("Text", "Text", 0, true);

            gv.CellFormatting += Gv_CellFormatting;
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
            view.Columns.Add("NotizID", typeof(int));
            view.Columns.Add("Datum", typeof(DateTime));
            view.Columns.Add("Mitarbeiter", typeof(string));
            view.Columns.Add("GueltigBis", typeof(DateTime));
            view.Columns.Add("IsArchived", typeof(bool));
            view.Columns.Add("IsRead", typeof(bool));
            view.Columns.Add("Text", typeof(string));

            if (raw == null) return view;

            foreach (DataRow r in raw.Rows)
            {
                int id = 0;
                try { if (raw.Columns.Contains("NotizID") && r["NotizID"] != DBNull.Value) id = Convert.ToInt32(r["NotizID"]); } catch { id = 0; }
                DateTime datum = DateTime.MinValue;
                try { if (raw.Columns.Contains("Datum") && r["Datum"] != DBNull.Value) datum = Convert.ToDateTime(r["Datum"]); } catch { datum = DateTime.MinValue; }
                string relId = string.Empty;
                try { if (raw.Columns.Contains("RelID") && r["RelID"] != DBNull.Value) relId = Convert.ToString(r["RelID"]) ?? string.Empty; } catch { relId = string.Empty; }
                string text = string.Empty;
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
                    if (!string.IsNullOrEmpty(mitarbeiter) && !mitarbeiter.StartsWith("✓ ", StringComparison.Ordinal))
                        mitarbeiter = "✓ " + mitarbeiter;
                    else if (string.IsNullOrEmpty(mitarbeiter))
                        mitarbeiter = "✓";
                }
                view.Rows.Add(id, datum, mitarbeiter, (object)gb ?? DBNull.Value, isArchived, isRead, text);
            }
            return view;
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

                using (var db = new DatabaseHelperKassen())
                {
                    DateTime? gb = null;
                    try { if (chkGueltigBis != null && chkGueltigBis.Checked) gb = dtpGueltigBis.Value.Date; } catch { gb = null; }
                    await db.InsertNotizAsync(RelTypMitarbeiterinfo, relId, DateTime.Now, text, 0, gb);
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
                if (!row.Table.Columns.Contains("NotizID")) return;
                int id = 0;
                try { if (row["NotizID"] != DBNull.Value) id = Convert.ToInt32(row["NotizID"]); } catch { id = 0; }
                if (id <= 0) return;

                if (MessageBox.Show(this, "Nachricht wirklich archivieren?", "Bestätigung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                using (var db = new DatabaseHelperKassen())
                {
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
