using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Data.SqlClient; // FhzId-Lookup für Schicht
using TaMi_Automatenclient.UI.Layout;

namespace TaMi_Automatenclient
{
    public class EntryEditForm : KassenclientBaseForm
    {
        public string Buchungstext { get; private set; }
        public int? Kost1 { get; private set; }
        public int? Kost2 { get; private set; }
        public int? Konto { get; private set; }
        public decimal Betrag19 { get; private set; }
        public decimal Betrag7 { get; private set; }
        public decimal Betrag0 { get; private set; }
        public bool DirectSaved { get; private set; }

        private const int HeaderHeight = UiTheme.HeaderHeight;

        private Label _lblZeit, _lblSchichtId, _lblFahrer, _lblKennzeichen, _lblGesamt;
        private Label _lblBelegnummer, _lblKassenBelegnummer;
        private TextBox _txtBuchungstext, _txtKost1, _txtKost2, _txtKonto;
        private ComboBox _cboMwst;

        private Panel _footer;
        private ModernGradientButton _btnSave, _btnCancel;

        private readonly decimal _total;
        private readonly int _firmenId;
        private readonly string _typ;
        private readonly string _belegnummer;
        private readonly string _kassenBelegnummer;

        // Zusatz: Schicht/Fahrzeug-Kontext für Rules
        private int? _schichtId;
        private int? _fhzId; // wird lazy geladen

        // Flags zum Steuern der Regelüberschreibung und zur Erkennung von Benutzereingaben
        private bool _applyingRules;
        private bool _dirtyK1, _dirtyK2, _dirtyKto, _dirtyTxt;

        public EntryEditForm(string zeit, string typ, string buchungstext, string betragGesamt,
            decimal v19, decimal v7, decimal v0,
            string vorhandenKost1 = "", string vorhandenKost2 = "", string vorhandenKonto = "",
            string schichtId = "", string kennzeichen = "", string fahrerName = "",
            int firmenId = 0,
            string belegnummer = null,
            string kassenBelegnummer = null)
        {
            this.Icon = Program.AppIcon;

            _total = ParseMoney(betragGesamt);
            if (v19 != 0) { Betrag19 = v19; Betrag7 = 0; Betrag0 = 0; }
            else if (v7 != 0) { Betrag19 = 0; Betrag7 = v7; Betrag0 = 0; }
            else if (v0 != 0) { Betrag19 = 0; Betrag7 = 0; Betrag0 = v0; }
            else { Betrag19 = _total; Betrag7 = 0; Betrag0 = 0; }

            _firmenId = firmenId;
            _typ = typ ?? string.Empty;
            _belegnummer = belegnummer ?? string.Empty;
            _kassenBelegnummer = kassenBelegnummer ?? string.Empty;
            // SchichtId ggf. merken (für FhzId-Auflösung)
            if (int.TryParse((schichtId ?? string.Empty).Trim(), out var sid) && sid > 0) _schichtId = sid; else _schichtId = null;

            Text = "Buchung bearbeiten";
            SetupDefaultForm("EntryEditForm", Text, new Size(820, 480));
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            AddHeaderPanel(Text, true, false, true);
            BuildContent(zeit, schichtId, fahrerName, kennzeichen, buchungstext, vorhandenKost1, vorhandenKost2, vorhandenKonto);
            BuildFooter();

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
            KeyPreview = true;
            this.KeyDown += async (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    // Direktes Bearbeiten in der Datenbank ausführen
                    if (TryCommit())
                    {
                        try
                        {
                            using (var db = new DatabaseHelperKassen())
                            {
                                string bn = _belegnummer;
                                if (string.IsNullOrWhiteSpace(bn) && !string.IsNullOrWhiteSpace(_kassenBelegnummer))
                                    bn = await db.ResolveBelegnummerByKassenBelegAsync(_kassenBelegnummer);

                                if (string.IsNullOrWhiteSpace(bn))
                                    throw new InvalidOperationException("Belegnummer konnte nicht ermittelt werden.");

                                // Direkte Aktualisierung nur per Belegnummer
                                int affected = await db.UpdateEntryDirectAsync(bn, null, Buchungstext, Kost1, Kost2, Konto, Betrag19, Betrag7, Betrag0);
                                if (affected <= 0)
                                    throw new InvalidOperationException("Kein Eintrag aktualisiert (Belegnummer nicht gefunden).");
                            }
                            DirectSaved = true;
                            DialogResult = DialogResult.OK;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "Fehler beim direkten Speichern: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    e.Handled = true;
                }
            };

            // Erste Regelanwendung (auto-fill) mit FhzId-Kontext
            _ = LoadContextAndApplyRulesAsync();
        }

        

        private void BuildContent(string zeit, string schichtId, string fahrerName, string kennzeichen, string buchungstext, string vorhandenKost1, string vorhandenKost2, string vorhandenKonto)
        {
            int left = 24, top = HeaderHeight + 20, gapY = 30, labelW = 160;

            Controls.Add(new Label { Text = "Datum:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblZeit = new Label { Text = zeit, Left = left + labelW + 10, Top = top, Width = 580 }; top += gapY;
            Controls.Add(_lblZeit);

            Controls.Add(new Label { Text = "Schicht:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblSchichtId = new Label { Text = schichtId, Left = left + labelW + 10, Top = top, Width = 580 }; top += gapY;
            Controls.Add(_lblSchichtId);

            Controls.Add(new Label { Text = "Fahrer:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblFahrer = new Label { Text = fahrerName, Left = left + labelW + 10, Top = top, Width = 580 }; top += gapY;
            Controls.Add(_lblFahrer);

            Controls.Add(new Label { Text = "Kennzeichen:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblKennzeichen = new Label { Text = kennzeichen, Left = left + labelW + 10, Top = top, Width = 580 }; top += gapY;
            Controls.Add(_lblKennzeichen);

            // Belegnummern anzeigen
            Controls.Add(new Label { Text = "Belegnummer:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblBelegnummer = new Label { Text = _belegnummer, Left = left + labelW + 10, Top = top, Width = 280 }; top += gapY;
            Controls.Add(_lblBelegnummer);

            Controls.Add(new Label { Text = "KassenBelegnummer:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblKassenBelegnummer = new Label { Text = _kassenBelegnummer, Left = left + labelW + 10, Top = top, Width = 280 }; top += gapY;
            Controls.Add(_lblKassenBelegnummer);

            Controls.Add(new Label { Text = "Gesamtbetrag:", Left = left, Top = top, Width = labelW, ForeColor = Color.DimGray });
            _lblGesamt = new Label { Text = _total.ToString("C2"), Left = left + labelW + 10, Top = top, Width = 200, Font = new Font("Segoe UI Variable", 11F, FontStyle.Bold) }; top += gapY + 8;
            Controls.Add(_lblGesamt);

            Controls.Add(new Label { Text = "Buchungstext:", Left = left, Top = top, Width = labelW });
            _txtBuchungstext = new TextBox { Left = left + labelW + 10, Top = top - 4, Width = 560, Text = buchungstext ?? string.Empty, TabIndex = 0 };
            Controls.Add(_txtBuchungstext); top += gapY;

            Controls.Add(new Label { Text = "Kost1:", Left = left, Top = top, Width = labelW });
            _txtKost1 = new TextBox { Left = left + labelW + 10, Top = top - 4, Width = 140, Text = vorhandenKost1 ?? string.Empty, TabIndex = 1 };
            Controls.Add(_txtKost1); top += gapY;

            Controls.Add(new Label { Text = "Kost2:", Left = left, Top = top, Width = labelW });
            _txtKost2 = new TextBox { Left = left + labelW + 10, Top = top - 4, Width = 140, Text = vorhandenKost2 ?? string.Empty, TabIndex = 2 };
            Controls.Add(_txtKost2); top += gapY;

            Controls.Add(new Label { Text = "Konto:", Left = left, Top = top, Width = labelW });
            _txtKonto = new TextBox { Left = left + labelW + 10, Top = top - 4, Width = 140, Text = vorhandenKonto ?? string.Empty, TabIndex = 3 };
            Controls.Add(_txtKonto); top += gapY;

            // Dirty-Flags setzen, aber nur wenn der Nutzer tippt (nicht während ApplyRulesAsync)
            _txtBuchungstext.TextChanged += (s, e) => { if (!_applyingRules) _dirtyTxt = true; };
            _txtKost1.TextChanged += (s, e) => { if (!_applyingRules) _dirtyK1 = true; };
            _txtKost2.TextChanged += (s, e) => { if (!_applyingRules) _dirtyK2 = true; };
            _txtKonto.TextChanged += (s, e) => { if (!_applyingRules) _dirtyKto = true; };

            Controls.Add(new Label { Text = "MwSt:", Left = left, Top = top, Width = labelW });
            _cboMwst = new ComboBox { Left = left + labelW + 10, Top = top - 4, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, TabIndex = 4 };
            _cboMwst.Items.AddRange(new object[] { "19%", "7%", "0%" });
            _cboMwst.SelectedItem = Betrag19 != 0 ? "19%" : Betrag7 != 0 ? "7%" : "0%";
            _cboMwst.SelectedIndexChanged += async (s, e) =>
            {
                ApplyVatSelection();
                // VAT-Wechsel: Kontext (FhzId) sicherstellen, dann Regeln erzwingen
                await EnsureFhzIdLoadedAsync();
                await ApplyRulesAsync(forceOverwrite: true);
            };
            Controls.Add(_cboMwst);
        }

        private void BuildFooter()
        {
            _footer = new Panel { Left = 0, Top = ClientSize.Height - 64, Width = ClientSize.Width, Height = 64, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.FromArgb(245, 248, 255) };
            Controls.Add(_footer);

            _btnSave = new ModernGradientButton { Text = "Speichern", Width = 160, Height = 44, Left = _footer.Width - 340, Top = 10, Anchor = AnchorStyles.Right | AnchorStyles.Top, GradientStart = UiTheme.PrimaryStart, GradientEnd = UiTheme.PrimaryEnd, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold) };
            _btnCancel = new ModernGradientButton { Text = "Abbrechen", Width = 160, Height = 44, Left = _footer.Width - 170, Top = 10, Anchor = AnchorStyles.Right | AnchorStyles.Top, GradientStart = UiTheme.SecondaryStart, GradientEnd = UiTheme.SecondaryEnd, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold) };
            _btnSave.Click += (s, e) => { if (!TryCommit()) return; DialogResult = DialogResult.OK; };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            _footer.Controls.AddRange(new Control[] { _btnSave, _btnCancel });
        }

        // Lädt ggf. FhzId und wendet danach Regeln an
        private async Task LoadContextAndApplyRulesAsync()
        {
            await EnsureFhzIdLoadedAsync();
            await ApplyRulesAsync();
        }

        // FhzId lazy aus TSchichten ermitteln, wenn SchichtId bekannt
        private async Task EnsureFhzIdLoadedAsync()
        {
            if (_fhzId.HasValue || !_schichtId.HasValue) return;
            try
            {
                using (var conn = new SqlConnection(DatabaseHelperKassen.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT TOP 1 FhzId FROM TSchichten WITH (NOLOCK) WHERE SchichtId = @sid";
                        cmd.Parameters.AddWithValue("@sid", _schichtId.Value);
                        var o = await cmd.ExecuteScalarAsync();
                        if (o != null && o != DBNull.Value)
                            _fhzId = Convert.ToInt32(o);
                    }
                }
            }
            catch { }
        }

        private async Task ApplyRulesAsync(bool forceOverwrite = false)
        {
            try
            {
                _applyingRules = true;

                var rules = await RulesEngine.LoadRulesAsync();
                var ctx = new RulesEngine.RuleContext
                {
                    FirmenId = _firmenId,
                    Typ = _typ,
                    PersId = null,
                    FhzId = _fhzId, // wichtig f�r FHZ-basierte Regeln
                    Betrag19 = this.Betrag19,
                    Betrag7 = this.Betrag7,
                    Betrag0 = this.Betrag0
                };

                // Ausgangswerte aus der UI merken
                int? currentK1 = TryParseInt(_txtKost1.Text);
                int? currentK2 = TryParseInt(_txtKost2.Text);
                int? currentKto = TryParseInt(_txtKonto.Text);
                string currentTxt = _txtBuchungstext.Text ?? string.Empty;

                // F�r forceOverwrite die Eingangswerte f�r die Regelberechnung auf leer setzen,
                // damit RulesEngine neue Werte liefert (sie �berschreibt nur bei null/leer)
                int? k1 = forceOverwrite ? (int?)null : currentK1;
                int? k2 = forceOverwrite ? (int?)null : currentK2;
                int? kto = forceOverwrite ? (int?)null : currentKto;
                string txt = forceOverwrite ? string.Empty : currentTxt;

                RulesEngine.ApplyForEdit(rules, ctx, ref k1, ref k2, ref kto, ref txt);

                // �bernahme in UI: bei forceOverwrite bevorzugt neue Regelwerte; wenn keine Regel liefert, bleibt alter Wert
                if (forceOverwrite)
                {
                    if (k1.HasValue) _txtKost1.Text = k1.Value.ToString();
                    if (k2.HasValue) _txtKost2.Text = k2.Value.ToString();
                    if (kto.HasValue) _txtKonto.Text = kto.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(txt)) _txtBuchungstext.Text = txt;
                }
                else
                {
                    if (!_dirtyK1 && k1.HasValue) _txtKost1.Text = k1.Value.ToString();
                    if (!_dirtyK2 && k2.HasValue) _txtKost2.Text = k2.Value.ToString();
                    if (!_dirtyKto && kto.HasValue) _txtKonto.Text = kto.Value.ToString();
                    if (!_dirtyTxt && !string.IsNullOrWhiteSpace(txt)) _txtBuchungstext.Text = txt;
                }
            }
            catch { }
            finally
            {
                _applyingRules = false;
            }
        }

        private void ApplyVatSelection()
        {
            var sel = (_cboMwst.SelectedItem as string) ?? "19%";
            if (sel == "19%") { Betrag19 = _total; Betrag7 = 0; Betrag0 = 0; }
            else if (sel == "7%") { Betrag19 = 0; Betrag7 = _total; Betrag0 = 0; }
            else { Betrag19 = 0; Betrag7 = 0; Betrag0 = _total; }
        }

        private bool TryCommit()
        {
            Buchungstext = _txtBuchungstext.Text ?? string.Empty;
            Kost1 = TryParseInt(_txtKost1.Text);
            Kost2 = TryParseInt(_txtKost2.Text);
            Konto = TryParseInt(_txtKonto.Text);
            ApplyVatSelection();
            return true;
        }

        private static int? TryParseInt(string s)
        {
            return int.TryParse((s ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var v) ? (int?)v : null;
        }

        private static decimal ParseMoney(string s)
        {
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out var v) ? v : 0m;
        }

        
    }
}