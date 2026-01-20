using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
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
            ClientSize = new Size(640, 320);
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
                Size = new Size(460, 56),
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
                Width = 200,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy",
                Font = new Font("Segoe UI Variable", 12F),
                Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };
            Controls.Add(_dtFrom);

            _dtTo = new DateTimePicker
            {
                Left = 350,
                Top = 68,
                Width = 200,
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
                Width = 410,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI Variable", 12F)
            };
            _cmbTemplate.Items.Add(new TemplateItem { Text = "DATEV CSV ", Kind = TemplateKind.DatevStandardVorz });
            _cmbTemplate.Items.Add(new TemplateItem { Text = "Kassenbericht drucken", Kind = TemplateKind.Kassenbericht });
            _cmbTemplate.SelectedIndex = 0;
            Controls.Add(_cmbTemplate);

            _btnCancel = MakeButton("Abbrechen", new Point(294, 232), new Size(140, 44), Color.FromArgb(158, 158, 158));
            _btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_btnCancel);

            _btnExport = MakeButton("Ausführen", new Point(444, 232), new Size(140, 44), Color.FromArgb(33, 150, 243));
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

            try
            {
                switch (sel.Kind)
                {
                    case TemplateKind.DatevStandardVorz:
                        await DoCsvExportAsync(from, to);
                        break;
                    case TemplateKind.Kassenbericht:
                        await DoPrintReportAsync(from, to);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DoCsvExportAsync(DateTime from, DateTime to)
        {
            using (var sfd = new SaveFileDialog
            {
                Title = "Export speichern",
                Filter = "CSV-Datei (*.csv)|*.csv",
                FileName = $"Kasse_{_kassenName}_{from:yyyy-MM-dd}_bis_{to:yyyy-MM-dd}.csv",
                OverwritePrompt = true
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                await ExportDatevStandardVorzAsync(from, to, sfd.FileName);
                MessageBox.Show(this, "Export wurde erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private async Task DoPrintReportAsync(DateTime from, DateTime to)
        {
            var data = await LoadKassenDataAsync(from, to);
            var doc = new System.Drawing.Printing.PrintDocument();
            doc.DocumentName = $"Kassenbericht_{_kassenName}_{from:yyyy-MM-dd}_bis_{to:yyyy-MM-dd}";

            // kleinste Ränder, kein unterer Rand
            doc.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(10, 40, 20, 0);

            int currentIndex = 0;
            bool printedTotals = false;
            int pageNo = 0;
            var rows = data.Rows;
            var de = CultureInfo.GetCultureInfo("de-DE");

            // Bei jedem Drucklauf neu beginnen
            doc.BeginPrint += (s, e) => { currentIndex = 0; printedTotals = false; pageNo = 0; };

            var totals = rows.Cast<DataRow>()
                .Where(r => !IsOld(r))
                .GroupBy(r => new { Konto = SafeString(r, "Konto"), Mwst = MwstText(r) })
                .Select(g => new { Konto = g.Key.Konto, Mwst = g.Key.Mwst, Betrag = g.Sum(r => BetragSigned(r)) })
                .OrderBy(t => t.Konto).ThenBy(t => t.Mwst).ToList();

            var kontoTotals = rows.Cast<DataRow>()
                .Where(r => !IsOld(r))
                .GroupBy(r => SafeString(r, "Konto"))
                .Select(g => new { Konto = g.Key, Betrag = g.Sum(r => BetragSigned(r)) })
                .OrderBy(t => t.Konto).ToList();

            decimal anfangsbestand = await GetAnfangsbestandAsync(from);
            decimal endbestand = await GetEndbestandAsync(to);

            Func<DataRow, string> getBeleg = r =>
            {
                var b = SafeString(r, "KassenBelegnummer");
                if (string.IsNullOrWhiteSpace(b)) b = SafeString(r, "Belegnummer");
                return b;
            };
            var orderedValid = rows.Cast<DataRow>()
                .Where(r => !IsOld(r))
                .OrderBy(r => r.Table.Columns.Contains("ErfasstAm") && r["ErfasstAm"] != DBNull.Value ? r.Field<DateTime>("ErfasstAm") : DateTime.MinValue)
                .ToList();
            string anfangsBeleg = orderedValid.Count > 0 ? getBeleg(orderedValid.First()) : string.Empty;
            string endBeleg = orderedValid.Count > 0 ? getBeleg(orderedValid.Last()) : string.Empty;
            string erstelltAm = DateTime.Now.ToString("dd.MM.yyyy", de);

            doc.PrintPage += (s, e) =>
            {
                pageNo++;
                var g = e.Graphics;
                var black = Brushes.Black;
                var titleFont = new Font("Times New Roman", 14f, FontStyle.Bold);
                var normal = new Font("Times New Roman", 12f, FontStyle.Regular);
                var smallBold = new Font("Times New Roman", 12f, FontStyle.Bold);

                float y = e.MarginBounds.Top - 10f;
                float left = e.MarginBounds.Left;
                float right = e.MarginBounds.Right;
                float pageWidth = e.MarginBounds.Width;

                // Kopf
                if (pageNo == 1)
                {
                    string headerTitle = string.IsNullOrWhiteSpace(_automatenName) ? _kassenName : ($"{_kassenName} – {_automatenName}");
                    g.DrawString(headerTitle, titleFont, black, left, y); y += 28f;
                    g.DrawString($"Kassenbericht vom:  {to:dd.MM.yyyy} – Zeitraum: {from:dd.MM.yyyy} bis {to:dd.MM.yyyy}", normal, black, left, y); y += 20f;
                    string info = $"Anfangsbestand: {anfangsbestand.ToString("C", de)}   Endbestand: {endbestand.ToString("C", de)}   Anfangsbeleg: {anfangsBeleg}   Endbeleg: {endBeleg}";
                    g.DrawString(info, normal, black, left, y); y += 18f;
                    using (var p = new Pen(Color.Black, 1f)) { g.DrawLine(p, left, y, right, y); }
                    y += 6f;
                }
                else
                {
                    string headerTitle = string.IsNullOrWhiteSpace(_automatenName) ? _kassenName : ($"{_kassenName} – {_automatenName}");
                    g.DrawString(headerTitle, smallBold, black, left, y);
                    g.DrawString($"   Kassenbericht vom {erstelltAm}", normal, black, left + 220f, y);
                    y += 16f;
                    using (var p = new Pen(Color.Black, 1f)) { g.DrawLine(p, left, y, right, y); }
                    y += 4f;
                }

                // Spaltenbreiten
                float gap = 8f, gapNarrow = 3f;
                float wBeleg = 60f, wKonto = 60f, wKost = 52f, wMwst = 42f, wBetrag = 96f;
                float fixedWidth = wBeleg + wKonto + (wKost * 2) + wMwst + wBetrag + (gap * 3) + (gapNarrow * 3);
                float wText = Math.Max(220f, pageWidth - fixedWidth);
                float xBeleg = left;
                float xText = xBeleg + wBeleg + gap;
                float xKonto = xText + wText + gap;
                float xKost1 = xKonto + wKonto + gapNarrow;
                float xKost2 = xKost1 + wKost + gapNarrow;
                float xMwst = xKost2 + wKost + gapNarrow;
                float xBetrag = xMwst + wMwst + gap;

                var sfWrap = new StringFormat(StringFormatFlags.LineLimit) { Trimming = StringTrimming.EllipsisWord };
                var sfRight = new StringFormat { Alignment = StringAlignment.Far };
                float lineH = normal.GetHeight(g);
                float maxTextHeight = lineH * 2f + 2f;

                bool hasDetailOnThisPage = false;

                // Kopfzeile nur wenn Details folgen
                if (currentIndex < rows.Count)
                {
                    g.DrawString("Beleg", smallBold, black, xBeleg, y);
                    g.DrawString("Buchung", smallBold, black, xText, y);
                    g.DrawString("Konto", smallBold, black, xKonto, y);
                    g.DrawString("Kost1", smallBold, black, xKost1, y);
                    g.DrawString("Kost2", smallBold, black, xKost2, y);
                    g.DrawString("%", smallBold, black, xMwst, y);
                    g.DrawString("Betrag", smallBold, black, xBetrag, y);
                    y += 22f;
                }

                // Detailzeilen
                while (currentIndex < rows.Count)
                {
                    var r = rows[currentIndex]; currentIndex++;
                    if (IsOld(r)) continue;
                    hasDetailOnThisPage = true;

                    string beleg = SafeString(r, "KassenBelegnummer");
                    if (string.IsNullOrWhiteSpace(beleg)) beleg = SafeString(r, "Belegnummer");
                    string typText = SafeString(r, "Buchungstext");
                    string konto = SafeString(r, "Konto");
                    string kost1 = SafeString(r, "Kost1");
                    string kost2 = SafeString(r, "Kost2");
                    string mwst = MwstText(r);
                    decimal betrag = BetragSigned(r);

                    float yStart = y;
                    g.DrawString(beleg, normal, black, new RectangleF(xBeleg, yStart, wBeleg, maxTextHeight), null);
                    var rectText = new RectangleF(xText, yStart, wText, maxTextHeight);
                    g.DrawString(typText, normal, black, rectText, sfWrap);
                    g.DrawString(konto, normal, black, new RectangleF(xKonto, yStart, wKonto, maxTextHeight), null);
                    g.DrawString(kost1, normal, black, new RectangleF(xKost1, yStart, wKost, maxTextHeight), null);
                    g.DrawString(kost2, normal, black, new RectangleF(xKost2, yStart, wKost, maxTextHeight), null);
                    g.DrawString(mwst, normal, black, new RectangleF(xMwst, yStart, wMwst, maxTextHeight), null);
                    g.DrawString(betrag.ToString("C", de), normal, black, new RectangleF(xBetrag, yStart, wBetrag, maxTextHeight), sfRight);

                    var measured = g.MeasureString(typText, normal, new SizeF(wText, 1000f), sfWrap);
                    float used = Math.Min(maxTextHeight, measured.Height);
                    y += Math.Max(lineH, used) + 2f;

                    // mehr Inhalte am Seitenende zulassen
                    if (y > e.MarginBounds.Bottom - 40)
                    {
                        e.HasMorePages = true;
                        return;
                    }
                }

                // Summen
                if (!printedTotals)
                {
                    float requiredSpace = 12f + (totals.Count + kontoTotals.Count + 10) * 16f;
                    if (y + requiredSpace > e.MarginBounds.Bottom)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    if (hasDetailOnThisPage)
                    {
                        y += 12f;
                        g.DrawString("Summen nach Konto und %", smallBold, black, left, y); y += 8f;
                        g.DrawString("Konto", smallBold, black, xKonto, y);
                        g.DrawString("%", smallBold, black, xMwst, y);
                        g.DrawString("Betrag", smallBold, black, xBetrag, y);
                        y += 20f;
                    }
                    else
                    {
                        // Kein Detailbereich auf dieser Seite: keine Spaltenüberschriften
                        y += 8f;
                        g.DrawString("Summen nach Konto und %", smallBold, black, left, y); y += 12f;
                    }

                    foreach (var t in totals)
                    {
                        g.DrawString(t.Konto, normal, black, xKonto, y);
                        g.DrawString(t.Mwst, normal, black, xMwst, y);
                        g.DrawString(t.Betrag.ToString("C", de), normal, black, new RectangleF(xBetrag, y, wBetrag, lineH + 4f), sfRight);
                        y += 16f;
                    }

                    y += 6f;
                    g.DrawString("Summen nach Konto", smallBold, black, left, y); y += 12f;
                    foreach (var t in kontoTotals)
                    {
                        g.DrawString(t.Konto, normal, black, xKonto, y);
                        g.DrawString(t.Betrag.ToString("C", de), normal, black, new RectangleF(xBetrag, y, wBetrag, lineH + 4f), sfRight);
                        y += 16f;
                    }

                    printedTotals = true;
                }

                e.HasMorePages = currentIndex < rows.Count || !printedTotals;
            };

            using (var dlg = new PrintDialog())
            {
                dlg.UseEXDialog = true;
                dlg.AllowPrintToFile = true;
                dlg.Document = doc;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    doc.PrinterSettings = dlg.PrinterSettings;
                    doc.Print();
                }
            }
        }

        private async Task<DataTable> LoadKassenDataAsync(DateTime from, DateTime to)
        {
            using (var db = new DatabaseHelperKassen())
            {
                return await db.GetKassenEintraegeAsync(_firmenId, _automatenName, from, to);
            }
        }
        private async Task<decimal> GetAnfangsbestandAsync(DateTime from)
        {
            using (var db = new DatabaseHelperKassen())
            {
                return await db.GetAnfangsbestandAsync(_firmenId, _automatenName, from);
            }
        }
        private async Task<decimal> GetEndbestandAsync(DateTime to)
        {
            using (var db = new DatabaseHelperKassen())
            {
                return await db.GetEndbestandAsync(_firmenId, _automatenName, to);
            }
        }

        private static bool IsOld(DataRow r)
        {
            try { return r.Table.Columns.Contains("RevIsOld") && r["RevIsOld"] != DBNull.Value && Convert.ToInt32(r["RevIsOld"]) != 0; } catch { return false; }
        }
        private static string SafeString(DataRow r, string col)
        {
            try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToString(r[col]) : string.Empty; } catch { return string.Empty; }
        }
        private static string MwstText(DataRow r)
        {
            decimal b19 = r.Table.Columns.Contains("Betrag19") && r["Betrag19"] != DBNull.Value ? Convert.ToDecimal(r["Betrag19"]) : 0m;
            decimal b7 = r.Table.Columns.Contains("Betrag7") && r["Betrag7"] != DBNull.Value ? Convert.ToDecimal(r["Betrag7"]) : 0m;
            decimal b0 = r.Table.Columns.Contains("Betrag0") && r["Betrag0"] != DBNull.Value ? Convert.ToDecimal(r["Betrag0"]) : 0m;
            return b19 != 0 ? "19" : b7 != 0 ? "7" : b0 != 0 ? "0" : string.Empty;
        }
        private static decimal BetragSigned(DataRow r)
        {
            decimal b19 = r.Table.Columns.Contains("Betrag19") && r["Betrag19"] != DBNull.Value ? Convert.ToDecimal(r["Betrag19"]) : 0m;
            decimal b7 = r.Table.Columns.Contains("Betrag7") && r["Betrag7"] != DBNull.Value ? Convert.ToDecimal(r["Betrag7"]) : 0m;
            decimal b0 = r.Table.Columns.Contains("Betrag0") && r["Betrag0"] != DBNull.Value ? Convert.ToDecimal(r["Betrag0"]) : 0m;
            return b19 + b7 + b0;
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
            DatevStandardVorz,
            Kassenbericht
        }
    }
}
