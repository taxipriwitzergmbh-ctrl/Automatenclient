using System;
using System.Data;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Data.SqlClient;
using System.IO; // NEW
using System.Text; // NEW
using System.Collections.Generic; // NEW
using TaMi_Kassenclient.Export; // NEW

namespace TaMi_Kassenclient
{
    public class DayViewForm : Form
    {
        private class EntryMeta
        {
            public string Belegnummer { get; set; }
            public decimal Betrag19 { get; set; }
            public decimal Betrag7 { get; set; }
            public decimal Betrag0 { get; set; }
            public string Kost1 { get; set; }
            public string Kost2 { get; set; }
            public string Konto { get; set; }
            public bool IsOld { get; set; }
            public bool IsFestgeschrieben { get; set; } // NEW
        }

        private readonly int _firmenId;
        private readonly string _kassenName;
        private readonly string _automatenName;

        private Panel headerPanel;
        private Button btnClose;
        private Label lblTitle;
        private Point _mouseDownLocation;

        private DateTimePicker dtpTag;
        private Button btnPrev;
        private Button btnNext;
        private Button btnToday;
        private Label lblDatum;
        private Button btnLockDay;
        private bool _dayLocked;

        private Label lblAnfang;
        private Label lblEnde;

        private ListView lvEintraege;

        private Panel footerPanel;
        private Button btnBearbeiten;
        private Button btnSplitten;
        private Button btnExportCsv; // NEW

        public DayViewForm(int firmenId, string kassenName, string automatenName)
        {
            this.Icon = Program.AppIcon;

            _firmenId = firmenId;
            _kassenName = kassenName;
            _automatenName = automatenName;
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1800, 900);
            BackColor = Color.White;
            DoubleBuffered = true;

            headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            headerPanel.Paint += HeaderPanel_Paint;
            headerPanel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _mouseDownLocation = e.Location; };
            headerPanel.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Left += e.X - _mouseDownLocation.X;
                    Top += e.Y - _mouseDownLocation.Y;
                }
            };
            Controls.Add(headerPanel);

            lblTitle = new Label
            {
                Text = $"Kasse: {_kassenName} (FID: {_firmenId})",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Variable", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 0),
                Size = new Size(1200, 60),
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(lblTitle);

            btnClose = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(48, 48),
                Location = new Point(ClientSize.Width - 56, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80);
            btnClose.Click += (s, e) => Close();
            headerPanel.Controls.Add(btnClose);

            btnLockDay = new Button
            {
                Text = "Tag festschreiben",
                Location = new Point(ClientSize.Width - 220, 12),
                Size = new Size(150, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(0, 172, 193),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLockDay.FlatAppearance.BorderSize = 0;
            btnLockDay.Click += async (s, e) => await DoLockDayAsync();
            headerPanel.Controls.Add(btnLockDay);

            try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 18, 18)); } catch { }

            dtpTag = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dddd, dd.MM.yyyy",
                Location = new Point(24, 80),
                Size = new Size(320, 32),
                Font = new Font("Segoe UI Variable", 12F),
                Value = DateTime.Today,
                Visible = false
            };
            dtpTag.ValueChanged += async (s, e) => { UpdateDateLabel(); await RefreshDayAsync(); };
            Controls.Add(dtpTag);

            lblDatum = new Label
            {
                Text = "",
                Location = new Point(24, 82),
                Size = new Size(320, 28),
                Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold)
            };
            Controls.Add(lblDatum);
            UpdateDateLabel();

            btnPrev = new Button
            {
                Text = "<",
                Location = new Point(360, 78),
                Size = new Size(40, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White
            };
            btnPrev.FlatAppearance.BorderSize = 0;
            btnPrev.Click += (s, e) => { dtpTag.Value = dtpTag.Value.AddDays(-1); };
            Controls.Add(btnPrev);

            btnNext = new Button
            {
                Text = ">",
                Location = new Point(406, 78),
                Size = new Size(40, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White
            };
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Click += (s, e) => { dtpTag.Value = dtpTag.Value.AddDays(1); };
            Controls.Add(btnNext);

            btnToday = new Button
            {
                Text = "Heute",
                Location = new Point(456, 78),
                Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White
            };
            btnToday.FlatAppearance.BorderSize = 0;
            btnToday.Click += (s, e) => { dtpTag.Value = DateTime.Today; };
            Controls.Add(btnToday);

            lblAnfang = new Label
            {
                Text = "Anfangsbestand: 0,00 €",
                Location = new Point(24, 130),
                Size = new Size(600, 32),
                Font = new Font("Segoe UI Variable", 16F, FontStyle.Bold)
            };
            Controls.Add(lblAnfang);

            lblEnde = new Label
            {
                Text = "Endbestand: 0,00 €",
                Location = new Point(24, 170),
                Size = new Size(600, 32),
                Font = new Font("Segoe UI Variable", 16F, FontStyle.Bold)
            };
            Controls.Add(lblEnde);

            lvEintraege = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Location = new Point(24, 220),
                Size = new Size(1720, 600),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI Variable", 12F, FontStyle.Regular)
            };
            lvEintraege.Columns.Add("Zeit", 180, HorizontalAlignment.Left);
            lvEintraege.Columns.Add("Typ", 180, HorizontalAlignment.Left);
            lvEintraege.Columns.Add("Buchungstext", 500, HorizontalAlignment.Left);
            lvEintraege.Columns.Add("Betrag", 180, HorizontalAlignment.Right);
            lvEintraege.Columns.Add("MwSt", 80, HorizontalAlignment.Center);
            lvEintraege.Columns.Add("Kost1", 100, HorizontalAlignment.Left);
            lvEintraege.Columns.Add("Kost2", 100, HorizontalAlignment.Left);
            lvEintraege.Columns.Add("Konto", 120, HorizontalAlignment.Left);
            lvEintraege.Columns.Add("Kassenbestand", 180, HorizontalAlignment.Right);
            lvEintraege.DoubleClick += LvEintraege_DoubleClick;
            Controls.Add(lvEintraege);

            InitializeFooter();

            Shown += async (s, e) => await RefreshDayAsync();
        }

        private void InitializeFooter()
        {
            footerPanel = new Panel
            {
                Location = new Point(0, ClientSize.Height - 64),
                Size = new Size(ClientSize.Width, 64),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Color.FromArgb(245, 248, 255)
            };
            Controls.Add(footerPanel);

            btnBearbeiten = new Button
            {
                Text = "Bearbeiten",
                Left = 24,
                Top = 12,
                Width = 160,
                Height = 40,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBearbeiten.FlatAppearance.BorderSize = 0;
            btnBearbeiten.Click += BtnBearbeiten_Click;
            footerPanel.Controls.Add(btnBearbeiten);

            btnSplitten = new Button
            {
                Text = "Splitten",
                Left = 200,
                Top = 12,
                Width = 160,
                Height = 40,
                BackColor = Color.FromArgb(0, 172, 193),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSplitten.FlatAppearance.BorderSize = 0;
            btnSplitten.Click += BtnSplitten_Click;
            footerPanel.Controls.Add(btnSplitten);

            // NEW: CSV Export button
            btnExportCsv = new Button
            {
                Text = "CSV-Export",
                Left = 376,
                Top = 12,
                Width = 160,
                Height = 40,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExportCsv.FlatAppearance.BorderSize = 0;
            btnExportCsv.Click += async (s, e) => await ExportCsvAsync();
            footerPanel.Controls.Add(btnExportCsv);
        }

        private void UpdateDateLabel()
        {
            lblDatum.Text = dtpTag.Value.ToString("dddd, dd.MM.yyyy", CultureInfo.CurrentCulture);
        }

        private async Task DoLockDayAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var affected = await db.LockDayAsync(_firmenId, _automatenName, dtpTag.Value.Date);
                    if (affected > 0)
                    {
                        MessageBox.Show(this, $"{affected} Buchung(en) festgeschrieben.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                _dayLocked = true; // lokal sperren (nicht rückgängig machbar)
                btnLockDay.Enabled = false;
                btnLockDay.Text = "Tag festgeschrieben";
                btnBearbeiten.Enabled = false;
                btnSplitten.Enabled = false;
                await RefreshDayAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Festschreiben: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetColumnValue(DataRow row, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                foreach (DataColumn col in row.Table.Columns)
                {
                    if (string.Equals(col.ColumnName, name, StringComparison.OrdinalIgnoreCase) && row[col] != DBNull.Value)
                        return row[col].ToString();
                }
            }
            return string.Empty;
        }

        private async Task<(string fahrer, string kennzeichen, int? fhzId)> ResolveFahrerUndKennzeichenAsync(string schichtIdText, object persIdObj)
        {
            string fahrer = string.Empty;
            string kennz = string.Empty;
            int? fhzId = null;
            try
            {
                using (var conn = new SqlConnection(DatabaseHelperKassen.GetConnectionString()))
                {
                    await conn.OpenAsync();

                    if (persIdObj != null && persIdObj != DBNull.Value)
                    {
                        int pid;
                        if (int.TryParse(Convert.ToString(persIdObj), out pid))
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT TOP 1 Vorname, Name FROM TPersonal WITH (NOLOCK) WHERE PID = @pid";
                                cmd.Parameters.AddWithValue("@pid", pid);
                                using (var rdr = await cmd.ExecuteReaderAsync())
                                {
                                    if (await rdr.ReadAsync())
                                    {
                                        var vor = rdr["Vorname"] as string ?? string.Empty;
                                        var nach = rdr["Name"] as string ?? string.Empty;
                                        fahrer = string.Concat(vor, string.IsNullOrEmpty(vor) || string.IsNullOrEmpty(nach) ? "" : " ", nach);
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(schichtIdText))
                    {
                        int schichtId;
                        if (int.TryParse(schichtIdText, out schichtId))
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT TOP 1 FhzId FROM TSchichten WITH (NOLOCK) WHERE SchichtId = @sid";
                                cmd.Parameters.AddWithValue("@sid", schichtId);
                                var o = await cmd.ExecuteScalarAsync();
                                if (o != null && o != DBNull.Value) fhzId = Convert.ToInt32(o);
                            }
                            if (fhzId.HasValue)
                            {
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = "SELECT TOP 1 Kennzeichen FROM TFahrzeuge WITH (NOLOCK) WHERE FID = @fid";
                                    cmd.Parameters.AddWithValue("@fid", fhzId.Value);
                                    var o = await cmd.ExecuteScalarAsync();
                                    if (o != null && o != DBNull.Value) kennz = Convert.ToString(o);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return (fahrer, kennz, fhzId);
        }

        private async Task RefreshDayAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    _dayLocked = await db.IsLockedUntilAsync(_firmenId, _automatenName, dtpTag.Value.Date);
                    btnLockDay.Enabled = !_dayLocked;
                    btnLockDay.Text = _dayLocked ? "Tag festgeschrieben" : "Tag festschreiben";
                    btnBearbeiten.Enabled = !_dayLocked;
                    btnSplitten.Enabled = !_dayLocked;
                    // Liste nicht deaktivieren, damit Farb-/Schriftunterschiede für alte Revisionen sichtbar bleiben
                    // lvEintraege.Enabled = !_dayLocked; // entfernt

                    var start = await db.GetAnfangsbestandAsync(_firmenId, _automatenName, dtpTag.Value.Date);
                    var end = await db.GetEndbestandAsync(_firmenId, _automatenName, dtpTag.Value.Date);
                    lblAnfang.Text = $"Anfangsbestand: {start:C2}";
                    lblEnde.Text = $"Endbestand: {end:C2}";

                    lvEintraege.Items.Clear();
                    var dt = await db.GetKassenTagEintraegeAsync(_firmenId, _automatenName, dtpTag.Value.Date);
                    foreach (DataRow row in dt.Rows)
                    {
                        DateTime ts = row.Field<DateTime>("ErfasstAm");
                        string typ = MapTypCodeToText(row.Table.Columns.Contains("Typ") ? row["Typ"] : null);
                        string txt = row["Buchungstext"] as string ?? "";
                        decimal betrag = row["Betrag"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Betrag"]);
                        decimal kb = row["Kassenbestand"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Kassenbestand"]);
                        decimal v19 = dt.Columns.Contains("Betrag19") && row["Betrag19"] != DBNull.Value ? Convert.ToDecimal(row["Betrag19"]) : 0m;
                        decimal v7 = dt.Columns.Contains("Betrag7") && row["Betrag7"] != DBNull.Value ? Convert.ToDecimal(row["Betrag7"]) : 0m;
                        decimal v0 = dt.Columns.Contains("Betrag0") && row["Betrag0"] != DBNull.Value ? Convert.ToDecimal(row["Betrag0"]) : 0m;
                        string beleg = dt.Columns.Contains("Belegnummer") ? row["Belegnummer"].ToString() : null;
                        bool isOld = dt.Columns.Contains("RevIsOld") && row["RevIsOld"] != DBNull.Value && Convert.ToInt32(row["RevIsOld"]) != 0;
                        bool isFest = dt.Columns.Contains("Festgeschrieben") && row["Festgeschrieben"] != DBNull.Value && Convert.ToInt32(row["Festgeschrieben"]) != 0; // NEW

                        string mwst = v19 != 0 ? "19%" : v7 != 0 ? "7%" : v0 != 0 ? "0%" : "-";
                        string kost1 = GetColumnValue(row, "Kost1", "Kost");
                        string kost2 = GetColumnValue(row, "Kost2");
                        string konto = GetColumnValue(row, "Konto");

                        var item = new ListViewItem(ts.ToString("dd.MM.yyyy HH:mm"));
                        item.SubItems.Add(typ);
                        item.SubItems.Add(txt);
                        item.SubItems.Add(betrag.ToString("C2"));
                        item.SubItems.Add(mwst);
                        item.SubItems.Add(kost1);
                        item.SubItems.Add(kost2);
                        item.SubItems.Add(konto);
                        item.SubItems.Add(kb.ToString("C2"));
                        item.Tag = new EntryMeta
                        {
                            Belegnummer = beleg,
                            Betrag19 = v19,
                            Betrag7 = v7,
                            Betrag0 = v0,
                            Kost1 = kost1,
                            Kost2 = kost2,
                            Konto = konto,
                            IsOld = isOld,
                            IsFestgeschrieben = isFest
                        };
                        if (isOld)
                        {
                            try
                            {
                                // Visuelle Hervorhebung alter Revisionen: kursiv + durchgestrichen + grauer Text
                                item.ForeColor = Color.DimGray;
                                var baseFont = lvEintraege.Font;
                                item.Font = new Font(baseFont, FontStyle.Italic | FontStyle.Strikeout);
                            }
                            catch { item.ForeColor = Color.Gray; }
                            item.SubItems[2].Text = "(Ersetzt durch nächste Buchung) " + (item.SubItems[2].Text ?? "");
                        }
                        // Hervorhebung festgeschriebener (gültiger) Einträge: hellgraue Hinterlegung
                        if (isFest)
                        {
                            try { item.BackColor = Color.FromArgb(245, 245, 245); } catch { item.BackColor = Color.Gainsboro; }
                        }
                        lvEintraege.Items.Add(item);
                    }

                    if (dt.Rows.Count == 0)
                    {
                        var empty = new ListViewItem(new[] { "", "Keine Buchungen am ausgewählten Tag.", "", "", "", "", "", "", "" }) { ForeColor = Color.DimGray };
                        lvEintraege.Items.Add(empty);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Laden der Bestände/Buchungen:\r\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(headerPanel.ClientRectangle,
                Color.FromArgb(33, 150, 243), Color.FromArgb(33, 203, 243), 0f))
            {
                e.Graphics.FillRectangle(brush, headerPanel.ClientRectangle);
            }
        }

        private async void LvEintraege_DoubleClick(object sender, EventArgs e)
        {
            await DoBearbeitenAsync();
        }

        private async void BtnBearbeiten_Click(object sender, EventArgs e)
        {
            if (lvEintraege.SelectedItems.Count == 0)
                return;
            await DoBearbeitenAsync();
        }

        private async Task DoBearbeitenAsync()
        {
            if (_dayLocked) { MessageBox.Show(this, "Tag ist festgeschrieben."); return; }
            if (lvEintraege.SelectedItems.Count == 0) { MessageBox.Show(this, "Bitte Eintrag auswählen."); return; }
            var item = lvEintraege.SelectedItems[0];
            var meta = item.Tag as EntryMeta; if (meta == null || string.IsNullOrEmpty(meta.Belegnummer)) return;

            using (var db = new DatabaseHelperKassen())
            {
                var row = await db.GetEintragByBelegnummerAsync(meta.Belegnummer);
                string schichtId = row?.Table.Columns.Contains("SchichtId") == true ? Convert.ToString(row["SchichtId"]) : string.Empty;
                object persIdObj = row?.Table.Columns.Contains("PersId") == true ? row["PersId"] : null;
                var info = await ResolveFahrerUndKennzeichenAsync(schichtId, persIdObj);

                using (var dlg = new EntryEditForm(item.SubItems[0].Text, item.SubItems[1].Text, item.SubItems[2].Text, item.SubItems[3].Text,
                    meta.Betrag19, meta.Betrag7, meta.Betrag0,
                    meta.Kost1, meta.Kost2, meta.Konto,
                    schichtId, info.kennzeichen, info.fahrer, _firmenId))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            // Regeln anwenden (nur Standardwerte setzen, falls leer)
                            var rules = await RulesEngine.LoadRulesAsync();
                            var ctx = new RulesEngine.RuleContext { FirmenId = _firmenId, PersId = TryParseIntSafe(persIdObj), Typ = item.SubItems.Count > 1 ? item.SubItems[1].Text : null, FhzId = info.fhzId };
                            int? k1 = dlg.Kost1, k2 = dlg.Kost2, kto = dlg.Konto; string txt = dlg.Buchungstext;
                            RulesEngine.ApplyForEdit(rules, ctx, dlg.Betrag19, dlg.Betrag7, dlg.Betrag0, ref k1, ref k2, ref kto, ref txt);

                            await db.ReviseSingleAsync(meta.Belegnummer, txt, k1, k2, kto,
                                dlg.Betrag19, dlg.Betrag7, dlg.Betrag0);
                            await RefreshDayAsync();
                        }
                        catch (System.Exception ex)
                        {
                            MessageBox.Show(this, $"Fehler beim Bearbeiten: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private async void BtnSplitten_Click(object sender, System.EventArgs e)
        {
            if (_dayLocked) { MessageBox.Show(this, "Tag ist festgeschrieben."); return; }
            if (lvEintraege.SelectedItems.Count == 0) { MessageBox.Show(this, "Bitte Eintrag auswählen."); return; }
            var item = lvEintraege.SelectedItems[0];
            var meta = item.Tag as EntryMeta; if (meta == null || string.IsNullOrEmpty(meta.Belegnummer)) return;

            decimal.TryParse(item.SubItems[3].Text, NumberStyles.Currency, CultureInfo.CurrentCulture, out var gesamt);
            var typ = item.SubItems.Count > 1 ? item.SubItems[1].Text : null;

            // SchichtId/PersId laden, um FhzId ermitteln zu können
            string schichtId = null; object persIdObj = null; int? fhzId = null;
            using (var db = new DatabaseHelperKassen())
            {
                var row = await db.GetEintragByBelegnummerAsync(meta.Belegnummer);
                schichtId = row?.Table.Columns.Contains("SchichtId") == true ? Convert.ToString(row["SchichtId"]) : string.Empty;
                persIdObj = row?.Table.Columns.Contains("PersId") == true ? row["PersId"] : null;
                var info = await ResolveFahrerUndKennzeichenAsync(schichtId, persIdObj);
                fhzId = info.fhzId;
            }

            using (var split = new EntrySplitForm(gesamt, meta.Betrag19, meta.Betrag7, meta.Betrag0,
                                                  startK1: TryParseInt(meta.Kost1), startK2: TryParseInt(meta.Kost2), startKto: TryParseInt(meta.Konto),
                                                  standardText: item.SubItems[2].Text,
                                                  firmenId: _firmenId, typ: typ, fhzId: fhzId))
            {
                if (split.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        // Regeln anwenden je MwSt-Teil
                        var rules = await RulesEngine.LoadRulesAsync();
                        var ctx = new RulesEngine.RuleContext { FirmenId = _firmenId, PersId = null, Typ = typ, FhzId = fhzId };

                        int? k1_19 = split.K1_19, k2_19 = split.K2_19, kto_19 = split.Kto_19; string t19 = split.Text19;
                        int? k1_7 = split.K1_7, k2_7 = split.K2_7, kto_7 = split.Kto_7; string t7 = split.Text7;
                        int? k1_0 = split.K1_0, k2_0 = split.K2_0, kto_0 = split.Kto_0; string t0 = split.Text0;

                        // 19%
                        RulesEngine.ApplyForEdit(rules, ctx, split.Betrag19, 0m, 0m, ref k1_19, ref k2_19, ref kto_19, ref t19);
                        // 7%
                        RulesEngine.ApplyForEdit(rules, ctx, 0m, split.Betrag7, 0m, ref k1_7, ref k2_7, ref kto_7, ref t7);
                        // 0%
                        RulesEngine.ApplyForEdit(rules, ctx, 0m, 0m, split.Betrag0, ref k1_0, ref k2_0, ref kto_0, ref t0);

                        using (var db = new DatabaseHelperKassen())
                        {
                            await db.SplitByVatAsync(meta.Belegnummer, item.SubItems[2].Text,
                                split.Betrag19, k1_19, k2_19, kto_19,
                                split.Betrag7,  k1_7,  k2_7,  kto_7,
                                split.Betrag0,  k1_0,  k2_0,  kto_0);
                        }
                        await RefreshDayAsync();
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show(this, $"Fehler beim Splitten: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async Task ExportCsvAsync()
        {
            try
            {
                if (lvEintraege.Items.Count == 0)
                {
                    MessageBox.Show(this, "Keine Buchungen zum Export vorhanden.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var sfd = new SaveFileDialog
                {
                    Title = "CSV-Export (DATEV – Standardformat VorzBetrag)",
                    Filter = "CSV-Datei (*.csv)|*.csv",
                    FileName = $"Kasse_{_kassenName}_{dtpTag.Value:yyyy-MM-dd}.csv",
                    OverwritePrompt = true
                })
                {
                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        return;

                    var rows = new List<DatevKasseCsvRow>();
                    var de = CultureInfo.GetCultureInfo("de-DE");

                    foreach (ListViewItem it in lvEintraege.Items)
                    {
                        if (it.SubItems.Count < 9) continue; // placeholder row
                        var meta = it.Tag as EntryMeta; if (meta == null) continue; // need metadata
                        if (meta.IsOld) continue; // skip replaced entries

                        DateTime belegDatum;
                        if (!DateTime.TryParseExact(it.SubItems[0].Text, "dd.MM.yyyy HH:mm", de, DateTimeStyles.None, out belegDatum))
                            belegDatum = dtpTag.Value.Date;

                        decimal betrag;
                        if (!decimal.TryParse(it.SubItems[3].Text, NumberStyles.Currency, de, out betrag))
                            betrag = meta.Betrag19 + meta.Betrag7 + meta.Betrag0;

                        string steuersatz = it.SubItems[4].Text ?? string.Empty;
                        if (steuersatz.EndsWith("%")) steuersatz = steuersatz.TrimEnd('%');

                        // RechNr aus TKassenbuch: FhzId-SchichtId falls beide vorhanden, sonst Belegnummer
                        string rechNr = meta.Belegnummer; // fallback
                        try
                        {
                            // Wir holen die Zeile erneut um FhzId/SchichtId sicher zu lesen (Columns jetzt vorhanden)
                            // (alternativ könnte Meta um diese Felder erweitert werden)
                            using (var db2 = new DatabaseHelperKassen())
                            {
                                var row = await db2.GetEintragByBelegnummerAsync(meta.Belegnummer);
                                if (row != null && row.Table != null)
                                {
                                    string schId = row.Table.Columns.Contains("SchichtId") && row["SchichtId"] != DBNull.Value ? Convert.ToString(row["SchichtId"]) : null;
                                    string fhzId = row.Table.Columns.Contains("FhzId") && row["FhzId"] != DBNull.Value ? Convert.ToString(row["FhzId"]) : null;
                                    if (!string.IsNullOrWhiteSpace(fhzId) && !string.IsNullOrWhiteSpace(schId))
                                        rechNr = fhzId + "-" + schId;
                                }
                            }
                        }
                        catch { }

                        rows.Add(new DatevKasseCsvRow
                        {
                            Belegdatum = belegDatum,
                            Belegnummer = meta.Belegnummer,
                            RechNr = rechNr,
                            Buchungstext = it.SubItems[2].Text,
                            BetragSigned = betrag, // mit Vorzeichen
                            Steuersatz = steuersatz,
                            Gegenkonto = meta.Konto,
                            Kostenstelle1 = meta.Kost1,
                            Kostenstelle2 = meta.Kost2,
                            Waehrung = "EUR",
                            BU = string.Empty,
                            Kostmenge = string.Empty,
                            Skonto = string.Empty,
                            Nachricht = null
                        });
                    }

                    var opts = new DatevKasseCsvExporterOptions
                    {
                        IncludeHeader = true,
                        Delimiter = ";",
                        Culture = de,
                        Utf8Bom = true,
                        Format = DatevCsvFormat.StandardVorzBetrag,
                        UseDayMonthOnly = true,
                        DefaultNachricht = "Kasse Import Standardformat"
                    };

                    await DatevKasseCsvExporter.ExportAsync(rows, sfd.FileName, opts);

                    MessageBox.Show(this, "CSV-Export wurde erstellt.", "Erfolg",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim CSV-Export:\r\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static int? TryParseInt(string s)
        {
            return int.TryParse((s ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var v) ? (int?)v : null;
        }

        private static int? TryParseIntSafe(object o)
        {
            if (o == null || o == System.DBNull.Value) return null;
            int v; return int.TryParse(o.ToString(), out v) ? (int?)v : null;
        }

        private static string MapTypCodeToText(object val)
        {
            if (val == null || val == DBNull.Value) return string.Empty;
            // Already a descriptive string?
            var s = val as string;
            if (!string.IsNullOrWhiteSpace(s))
            {
                // If it's a number as string, convert; otherwise assume already mapped
                byte bNum;
                if (!byte.TryParse(s, out bNum)) return s; // e.g. "Einzahlung"
                val = bNum; // fall through to numeric mapping
            }
            try
            {
                var code = Convert.ToInt32(val);
                switch (code)
                {
                    case 1: return "Anfangsbestand";
                    case 2: return "Einzahlung";
                    case 3: return "Auszahlung";
                    case 4: return "Schichtabrechnung";
                    case 5: return "Personalguthaben";
                    case 6: return "Trinkgeld Auszahlung";
                    default: return code.ToString();
                }
            }
            catch { return string.Empty; }
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
    }
}