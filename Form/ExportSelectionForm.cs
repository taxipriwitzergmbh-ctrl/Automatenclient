using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaMi_Kassenclient.Export;

namespace TaMi_Kassenclient
{
    public class ExportSelectionForm : Form
    {
        private readonly int _firmenId;
        private readonly string _kassenName;
        private readonly string _automatenName;

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

            Text = "Export";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 250);
            MaximizeBox = false;
            MinimizeBox = false;

            BuildUi();
        }

        private void BuildUi()
        {
            var lblRange = new Label { Text = "Zeitraum:", Left = 16, Top = 20, Width = 120 };
            _dtFrom = new DateTimePicker { Left = 140, Top = 16, Width = 160, Format = DateTimePickerFormat.Short, Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) };
            _dtTo = new DateTimePicker { Left = 310, Top = 16, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today };

            var lblTpl = new Label { Text = "Vorlage:", Left = 16, Top = 66, Width = 120 };
            _cmbTemplate = new ComboBox { Left = 140, Top = 62, Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTemplate.Items.Add(new TemplateItem { Text = "DATEV CSV (Standard VorzBetrag)", Kind = TemplateKind.DatevStandardVorz });
            _cmbTemplate.SelectedIndex = 0;

            _btnCancel = new Button { Text = "Abbrechen", Left = 284, Top = 160, Width = 90, DialogResult = DialogResult.Cancel };
            _btnExport = new Button { Text = "Export", Left = 380, Top = 160, Width = 90 };
            _btnExport.Click += async (s, e) => await DoExportAsync();

            Controls.Add(lblRange);
            Controls.Add(_dtFrom);
            Controls.Add(_dtTo);
            Controls.Add(lblTpl);
            Controls.Add(_cmbTemplate);
            Controls.Add(_btnCancel);
            Controls.Add(_btnExport);
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
