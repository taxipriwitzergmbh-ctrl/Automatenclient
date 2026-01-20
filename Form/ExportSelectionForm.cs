using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TaMi_Kassenclient.Export;

namespace TaMi_Kassenclient
{
    public class ExportSelectionForm : Form
    {
        private readonly int _firmenId;
        private readonly string _kassenName;
        private readonly string _automatenName;

        private Panel _headerPanel;
        private Label _lblTitle;
        private Button _btnClose;
        private Point _mouseDownLocation;

        private DateTimePicker _dtFrom;
        private DateTimePicker _dtTo;
        private ComboBox _cmbTemplate;
        private Button _btnCancel;
        private Button _btnExport;

        public ExportSelectionForm(int firmenId, string kassenName, string automatenName)
        {
            _firmenId = firmenId;
            _kassenName = kassenName;
            _automatenName = automatenName;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 280);
            BackColor = Color.White;
            DoubleBuffered = true;
            try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16)); } catch { }

            BuildHeader();
            BuildUi();
        }

        private void BuildHeader()
        {
            _headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _headerPanel.Paint += HeaderPanel_Paint;
            _headerPanel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _mouseDownLocation = e.Location; };
            _headerPanel.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Left += e.X - _mouseDownLocation.X;
                    Top += e.Y - _mouseDownLocation.Y;
                }
            };
            Controls.Add(_headerPanel);

            _lblTitle = new Label
            {
                Text = "Export – Auswahl",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Variable", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 0),
                Size = new Size(400, 56),
                BackColor = Color.Transparent
            };
            _headerPanel.Controls.Add(_lblTitle);

            _btnClose = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI Symbol", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(44, 44),
                Location = new Point(ClientSize.Width - 52, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80);
            _btnClose.Click += (s, e) => Close();
            _headerPanel.Controls.Add(_btnClose);
        }

        private void BuildUi()
        {
            var lblRange = new Label
            {
                Text = "Zeitraum:",
                Left = 24,
                Top = 72,
                Width = 110,
                Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41)
            };
            Controls.Add(lblRange);

            _dtFrom = new DateTimePicker
            {
                Left = 140,
                Top = 68,
                Width = 180,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy",
                Font = new Font("Segoe UI Variable", 12F),
                Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };
            Controls.Add(_dtFrom);

            _dtTo = new DateTimePicker
            {
                Left = 330,
                Top = 68,
                Width = 180,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy",
                Font = new Font("Segoe UI Variable", 12F),
                Value = DateTime.Today
            };
            Controls.Add(_dtTo);

            var lblTpl = new Label
            {
                Text = "Vorlage:",
                Left = 24,
                Top = 120,
                Width = 110,
                Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41)
            };
            Controls.Add(lblTpl);

            _cmbTemplate = new ComboBox
            {
                Left = 140,
                Top = 116,
                Width = 370,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI Variable", 12F)
            };
            _cmbTemplate.Items.Add(new TemplateItem { Text = "DATEV CSV (Standard VorzBetrag)", Kind = TemplateKind.DatevStandardVorz });
            _cmbTemplate.SelectedIndex = 0;
            Controls.Add(_cmbTemplate);

            _btnCancel = MakeButton("Abbrechen", new Point(264, 200), new Size(130, 44), Color.FromArgb(158, 158, 158));
            _btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_btnCancel);

            _btnExport = MakeButton("Export", new Point(410, 200), new Size(130, 44), Color.FromArgb(33, 150, 243));
            _btnExport.Click += async (s, e) => await DoExportAsync();
            Controls.Add(_btnExport);
        }

        private Button MakeButton(string text, Point location, Size size, Color backColor)
        {
            var b = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            try { b.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, b.Width, b.Height, 10, 10)); } catch { }
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Max(backColor.R - 10, 0), Math.Max(backColor.G - 10, 0), Math.Max(backColor.B - 10, 0));
            return b;
        }

        private async Task DoExportAsync()
        {
            if (!(_cmbTemplate.SelectedItem is TemplateItem sel)) return;
            var from = _dtFrom.Value.Date;
            var to = _dtTo.Value.Date;
            if (to < from)
            {
                MessageBox.Show(this, "Das Enddatum darf nicht vor dem Startdatum liegen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog
            {
                Title = "Export speichern",
                Filter = "CSV-Datei (*.csv)|*.csv",
                FileName = $"Kasse_{_kassenName}_{from:yyyy-MM-dd}_bis_{to:yyyy-MM-dd}.csv",
                OverwritePrompt = true
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    switch (sel.Kind)
                    {
                        case TemplateKind.DatevStandardVorz:
                            await ExportDatevStandardVorzAsync(from, to, sfd.FileName);
                            break;
                    }

                    MessageBox.Show(this, "Export wurde erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Fehler beim Export:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ExportDatevStandardVorzAsync(DateTime from, DateTime to, string path)
        {
            var rows = new List<DatevKasseCsvRow>();
            var de = CultureInfo.GetCultureInfo("de-DE");

            using (var db = new DatabaseHelperKassen())
            {
                DataTable dt = await db.GetKassenEintraegeAsync(_firmenId, _automatenName, from, to);
                foreach (DataRow r in dt.Rows)
                {
                    // Alte Revisionen überspringen
                    bool isOld = r.Table.Columns.Contains("RevIsOld") && r["RevIsOld"] != DBNull.Value && Convert.ToInt32(r["RevIsOld"]) != 0;
                    if (isOld) continue;

                    DateTime belegDatum = r.Field<DateTime>("ErfasstAm");

                    decimal b19 = r.Table.Columns.Contains("Betrag19") && r["Betrag19"] != DBNull.Value ? Convert.ToDecimal(r["Betrag19"]) : 0m;
                    decimal b7 = r.Table.Columns.Contains("Betrag7") && r["Betrag7"] != DBNull.Value ? Convert.ToDecimal(r["Betrag7"]) : 0m;
                    decimal b0 = r.Table.Columns.Contains("Betrag0") && r["Betrag0"] != DBNull.Value ? Convert.ToDecimal(r["Betrag0"]) : 0m;
                    decimal betrag = b19 + b7 + b0;

                    string mwst = b19 != 0 ? "19" : b7 != 0 ? "7" : b0 != 0 ? "0" : string.Empty;

                    string beleg = Convert.ToString(r["Belegnummer"]);

                    string rechNr = beleg;
                    try
                    {
                        string schId = r.Table.Columns.Contains("SchichtId") && r["SchichtId"] != DBNull.Value ? Convert.ToString(r["SchichtId"]) : null;
                        string fhzId = r.Table.Columns.Contains("FhzId") && r["FhzId"] != DBNull.Value ? Convert.ToString(r["FhzId"]) : null;
                        if (!string.IsNullOrWhiteSpace(fhzId) && !string.IsNullOrWhiteSpace(schId)) rechNr = fhzId + "-" + schId;
                    }
                    catch { }

                    var row = new DatevKasseCsvRow
                    {
                        Belegdatum = belegDatum,
                        Belegnummer = beleg,
                        RechNr = rechNr,
                        Buchungstext = Convert.ToString(r["Buchungstext"]) ?? string.Empty,
                        BetragSigned = betrag,
                        Steuersatz = mwst,
                        Gegenkonto = Convert.ToString(r["Konto"]) ?? string.Empty,
                        Kostenstelle1 = Convert.ToString(r["Kost1"]) ?? string.Empty,
                        Kostenstelle2 = Convert.ToString(r["Kost2"]) ?? string.Empty,
                        Waehrung = "EUR",
                        BU = string.Empty,
                        Kostmenge = string.Empty,
                        Skonto = string.Empty,
                        Nachricht = null
                    };
                    rows.Add(row);
                }
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

            await DatevKasseCsvExporter.ExportAsync(rows, path, opts);
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(_headerPanel.ClientRectangle,
                Color.FromArgb(33, 150, 243), Color.FromArgb(33, 203, 243), 0f))
            {
                e.Graphics.FillRectangle(brush, _headerPanel.ClientRectangle);
            }
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private class TemplateItem
        {
            public string Text { get; set; }
            public TemplateKind Kind { get; set; }
            public override string ToString() => Text;
        }

        private enum TemplateKind
        {
            DatevStandardVorz
        }
    }
}
