using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace TaMi_Kassenclient
{
    public class EntrySplitForm : Form
    {
        private const int HeaderHeight = 60;

        // Rückgaben
        public decimal Betrag19 => nud19.Value;
        public decimal Betrag7  => nud7.Value;
        public decimal Betrag0  => nud0.Value;
        public int? K1_19 => TryParseInt(txt19_K1.Text);
        public int? K2_19 => TryParseInt(txt19_K2.Text);
        public int? Kto_19 => TryParseInt(txt19_Kto.Text);
        public string Text19 => txt19_Text.Text?.Trim() ?? string.Empty;
        public int? K1_7  => TryParseInt(txt7_K1.Text);
        public int? K2_7  => TryParseInt(txt7_K2.Text);
        public int? Kto_7  => TryParseInt(txt7_Kto.Text);
        public string Text7 => txt7_Text.Text?.Trim() ?? string.Empty;
        public int? K1_0  => TryParseInt(txt0_K1.Text);
        public int? K2_0  => TryParseInt(txt0_K2.Text);
        public int? Kto_0  => TryParseInt(txt0_Kto.Text);
        public string Text0 => txt0_Text.Text?.Trim() ?? string.Empty;

        private readonly decimal _originalSumme;
        private bool _updating;

        private readonly int _firmenId;
        private readonly string _typ;
        private readonly int? _fhzId;

        // Header/Footer
        private Panel headerPanel, footerPanel;
        private Label lblTitle, lblSumInfo;
        private Button btnClose, btnSave, btnCancel;

        // Eingaben
        private NumericUpDown nud19, nud7, nud0;
        private TextBox txt19_K1, txt19_K2, txt19_Kto, txt19_Text;
        private TextBox txt7_K1, txt7_K2, txt7_Kto, txt7_Text;
        private TextBox txt0_K1, txt0_K2, txt0_Kto, txt0_Text;

        // Dirty-Flags pro MwSt-Teil, damit Benutzerwerte nicht überschrieben werden
        private bool _applyingRules;
        private bool _d19K1, _d19K2, _d19Kto, _d19Txt;
        private bool _d7K1, _d7K2, _d7Kto, _d7Txt;
        private bool _d0K1, _d0K2, _d0Kto, _d0Txt;

        public EntrySplitForm(decimal originalSumme, decimal vorhand19, decimal vorhand7, decimal vorhand0,
            int? startK1, int? startK2, int? startKto, string standardText,
            int firmenId, string typ, int? fhzId)
        {
            _originalSumme = originalSumme;
            _firmenId = firmenId;
            _typ = typ ?? string.Empty;
            _fhzId = fhzId;
            BuildChrome();
            BuildContent();

            // Vorbelegung
            nud19.Value = ClampToMoney(vorhand19);
            nud7.Value  = ClampToMoney(vorhand7);
            nud0.Value  = ClampToMoney(vorhand0);
            txt19_Text.Text = standardText ?? string.Empty;
            txt7_Text.Text  = standardText ?? string.Empty;
            txt0_Text.Text  = standardText ?? string.Empty;
            if (startK1.HasValue) { txt19_K1.Text = txt7_K1.Text = txt0_K1.Text = startK1.Value.ToString(CultureInfo.InvariantCulture); }
            if (startK2.HasValue) { txt19_K2.Text = txt7_K2.Text = txt0_K2.Text = startK2.Value.ToString(CultureInfo.InvariantCulture); }
            if (startKto.HasValue){ txt19_Kto.Text= txt7_Kto.Text= txt0_Kto.Text= startKto.Value.ToString(CultureInfo.InvariantCulture); }

            // Header garantiert nach vorn
            headerPanel.BringToFront();
            btnClose.BringToFront();
            try { Controls.SetChildIndex(headerPanel, 0); } catch { }

            // Beim Öffnen passende Regeln anwenden (pro MwSt-Teil) und vorhandene Werte überschreiben
            Shown += async (s, e) => await ApplyRulesForAllVatsAsync(forceOverwrite: true);

            UpdateSumInfo();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = new Rectangle(0, 0, ClientSize.Width, HeaderHeight);
            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(33, 150, 243), Color.FromArgb(33, 203, 243), 0f))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderHeight));
        }

        private void BuildChrome()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 520);
            BackColor = Color.White;
            Font = new Font("Segoe UI Variable", 10F);
            DoubleBuffered = true;
            try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16)); } catch { }

            headerPanel = new Panel { Left = 0, Top = 0, Width = ClientSize.Width, Height = HeaderHeight, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Dock = DockStyle.Top, BackColor = Color.Transparent };
            lblTitle = new Label { Text = "Buchung aufteilen", Left = 20, Top = 0, Width = 600, Height = HeaderHeight, ForeColor = Color.White, Font = new Font("Segoe UI Variable", 16F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            btnClose = new Button { Text = "\u2715", Left = headerPanel.Width - 52, Top = 8, Width = 44, Height = 44, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Transparent, TabStop = false };
            btnClose.FlatAppearance.BorderSize = 0; btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80);
            btnClose.Click += (s, e) => Close();
            headerPanel.Controls.Add(lblTitle); headerPanel.Controls.Add(btnClose); Controls.Add(headerPanel);

            footerPanel = new Panel { Left = 0, Top = ClientSize.Height - 64, Width = ClientSize.Width, Height = 64, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.FromArgb(245, 248, 255) };
            btnSave = new Button { Text = "Speichern", Width = 160, Height = 40, Left = footerPanel.Width - 340, Top = 12, Anchor = AnchorStyles.Right | AnchorStyles.Top, BackColor = Color.FromArgb(33, 150, 243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSave.FlatAppearance.BorderSize = 0; btnSave.Enabled = false; btnSave.Click += (s, e) => { DialogResult = DialogResult.OK; };
            btnCancel = new Button { Text = "Abbrechen", Width = 160, Height = 40, Left = footerPanel.Width - 170, Top = 12, Anchor = AnchorStyles.Right | AnchorStyles.Top, BackColor = Color.Gainsboro, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0; btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            footerPanel.Controls.AddRange(new Control[] { btnSave, btnCancel }); Controls.Add(footerPanel);
        }

        private void BuildContent()
        {
            int leftLbl = 24, leftAmt = 90, leftK1 = 220, leftK2 = 320, leftKto = 420, leftText = 520, top = HeaderHeight + 24, gap = 36;

            Controls.Add(new Label { Text = "%", Left = leftLbl, Top = top - 28, Width = 40, ForeColor = Color.DimGray });
            Controls.Add(new Label { Text = "Betrag", Left = leftAmt, Top = top - 28, Width = 120, ForeColor = Color.DimGray });
            Controls.Add(new Label { Text = "Kost1", Left = leftK1, Top = top - 28, Width = 80, ForeColor = Color.DimGray });
            Controls.Add(new Label { Text = "Kost2", Left = leftK2, Top = top - 28, Width = 80, ForeColor = Color.DimGray });
            Controls.Add(new Label { Text = "Konto", Left = leftKto, Top = top - 28, Width = 80, ForeColor = Color.DimGray });
            Controls.Add(new Label { Text = "Buchungstext", Left = leftText, Top = top - 28, Width = 320, ForeColor = Color.DimGray });

            Controls.Add(new Label { Text = "19%", Left = leftLbl, Top = top + 4, Width = 40 });
            nud19 = CreateMoneyUpDown(leftAmt, top);
            txt19_K1 = CreateSmallBox(leftK1, top); txt19_K2 = CreateSmallBox(leftK2, top); txt19_Kto = CreateSmallBox(leftKto, top);
            txt19_Text = CreateTextBox(leftText, top);
            Controls.AddRange(new Control[] { nud19, txt19_K1, txt19_K2, txt19_Kto, txt19_Text });
            top += gap;

            Controls.Add(new Label { Text = "7%", Left = leftLbl, Top = top + 4, Width = 40 });
            nud7 = CreateMoneyUpDown(leftAmt, top);
            txt7_K1 = CreateSmallBox(leftK1, top); txt7_K2 = CreateSmallBox(leftK2, top); txt7_Kto = CreateSmallBox(leftKto, top);
            txt7_Text = CreateTextBox(leftText, top);
            Controls.AddRange(new Control[] { nud7, txt7_K1, txt7_K2, txt7_Kto, txt7_Text });
            top += gap;

            Controls.Add(new Label { Text = "0%", Left = leftLbl, Top = top + 4, Width = 40 });
            nud0 = CreateMoneyUpDown(leftAmt, top);
            txt0_K1 = CreateSmallBox(leftK1, top); txt0_K2 = CreateSmallBox(leftK2, top); txt0_Kto = CreateSmallBox(leftKto, top);
            txt0_Text = CreateTextBox(leftText, top);
            Controls.AddRange(new Control[] { nud0, txt0_K1, txt0_K2, txt0_Kto, txt0_Text });

            lblSumInfo = new Label { Left = leftAmt, Top = top + 50, Width = 600, Height = 24, ForeColor = Color.DimGray };
            Controls.Add(lblSumInfo);

            // Dirty-Flags setzen, wenn der Benutzer die Felder ändert
            txt19_K1.TextChanged += (s, e) => { if (!_applyingRules) _d19K1 = true; };
            txt19_K2.TextChanged += (s, e) => { if (!_applyingRules) _d19K2 = true; };
            txt19_Kto.TextChanged += (s, e) => { if (!_applyingRules) _d19Kto = true; };
            txt19_Text.TextChanged += (s, e) => { if (!_applyingRules) _d19Txt = true; };

            txt7_K1.TextChanged += (s, e) => { if (!_applyingRules) _d7K1 = true; };
            txt7_K2.TextChanged += (s, e) => { if (!_applyingRules) _d7K2 = true; };
            txt7_Kto.TextChanged += (s, e) => { if (!_applyingRules) _d7Kto = true; };
            txt7_Text.TextChanged += (s, e) => { if (!_applyingRules) _d7Txt = true; };

            txt0_K1.TextChanged += (s, e) => { if (!_applyingRules) _d0K1 = true; };
            txt0_K2.TextChanged += (s, e) => { if (!_applyingRules) _d0K2 = true; };
            txt0_Kto.TextChanged += (s, e) => { if (!_applyingRules) _d0Kto = true; };
            txt0_Text.TextChanged += (s, e) => { if (!_applyingRules) _d0Txt = true; };

            nud19.ValueChanged += async (s, e) =>
            {
                AdjustOthers("19");
                await ApplyRulesForGroupAsync("19");
                // 19% verändert 7% im UI -> auch für 7% neu anwenden, wenn nicht manuell überschrieben
                await ApplyRulesForGroupAsync("7");
            };
            nud7.ValueChanged  += async (s, e) =>
            {
                AdjustOthers("7");
                await ApplyRulesForGroupAsync("7");
                await ApplyRulesForGroupAsync("19");
            };
            nud0.ValueChanged  += async (s, e) =>
            {
                AdjustOthers("0");
                await ApplyRulesForGroupAsync("0");
                await ApplyRulesForGroupAsync("7");
            };
        }

        private NumericUpDown CreateMoneyUpDown(int left, int top)
        {
            return new NumericUpDown
            {
                Left = left,
                Top = top,
                Width = 110,
                DecimalPlaces = 2,
                Maximum = 100000000,
                Minimum = -100000000,
                Increment = 0.10M,
                ThousandsSeparator = true
            };
        }

        private TextBox CreateSmallBox(int left, int top) => new TextBox { Left = left, Top = top, Width = 80 };
        private TextBox CreateTextBox(int left, int top) => new TextBox { Left = left, Top = top, Width = 340 };

        private void AdjustOthers(string changed)
        {
            if (_updating) return;
            try
            {
                _updating = true;
                decimal v19 = nud19.Value, v7 = nud7.Value, v0 = nud0.Value;
                if (changed == "19")
                {
                    nud7.Value = ClampToMoney(_originalSumme - v19 - v0);
                }
                else if (changed == "7")
                {
                    nud19.Value = ClampToMoney(_originalSumme - v7 - v0);
                }
                else
                {
                    nud7.Value = ClampToMoney(_originalSumme - v19 - v0);
                }
            }
            finally
            {
                _updating = false;
                UpdateSumInfo();
            }
        }

        private void UpdateSumInfo()
        {
            var sum = nud19.Value + nud7.Value + nud0.Value;
            lblSumInfo.Text = $"Summe: {sum:C2}  (Soll: {_originalSumme:C2})";
            bool ok = sum == _originalSumme;
            lblSumInfo.ForeColor = ok ? Color.Green : Color.Red;
            btnSave.Enabled = ok;
        }

        private async Task ApplyRulesForAllVatsAsync(bool forceOverwrite = false)
        {
            await ApplyRulesForGroupAsync("19", forceOverwrite);
            await ApplyRulesForGroupAsync("7", forceOverwrite);
            await ApplyRulesForGroupAsync("0", forceOverwrite);
        }

        private async Task ApplyRulesForGroupAsync(string group, bool forceOverwrite = false)
        {
            try
            {
                var rules = await RulesEngine.LoadRulesAsync();
                var ctx = new RulesEngine.RuleContext { FirmenId = _firmenId, Typ = _typ, FhzId = _fhzId };

                int? k1 = null, k2 = null, kto = null; string txt = string.Empty;
                decimal b19 = 0m, b7 = 0m, b0 = 0m;
                if (group == "19")
                {
                    var amt = Betrag19;
                    if (amt <= 0m) amt = 0.01m; // Minimalwert, damit Regeln mit "> 0" matchen (nur für Matching)
                    b19 = amt;
                }
                else if (group == "7")
                {
                    var amt = Betrag7;
                    if (amt <= 0m) amt = 0.01m;
                    b7 = amt;
                }
                else
                {
                    var amt = Betrag0;
                    if (amt <= 0m) amt = 0.01m;
                    b0 = amt;
                }

                RulesEngine.ApplyForEdit(rules, ctx, b19, b7, b0, ref k1, ref k2, ref kto, ref txt);

                _applyingRules = true;
                if (group == "19")
                {
                    if ((forceOverwrite || !_d19K1) && k1.HasValue) txt19_K1.Text = k1.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d19K2) && k2.HasValue) txt19_K2.Text = k2.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d19Kto) && kto.HasValue) txt19_Kto.Text = kto.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d19Txt) && !string.IsNullOrWhiteSpace(txt)) txt19_Text.Text = txt;
                }
                else if (group == "7")
                {
                    if ((forceOverwrite || !_d7K1) && k1.HasValue) txt7_K1.Text = k1.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d7K2) && k2.HasValue) txt7_K2.Text = k2.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d7Kto) && kto.HasValue) txt7_Kto.Text = kto.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d7Txt) && !string.IsNullOrWhiteSpace(txt)) txt7_Text.Text = txt;
                }
                else // "0"
                {
                    if ((forceOverwrite || !_d0K1) && k1.HasValue) txt0_K1.Text = k1.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d0K2) && k2.HasValue) txt0_K2.Text = k2.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d0Kto) && kto.HasValue) txt0_Kto.Text = kto.Value.ToString(CultureInfo.InvariantCulture);
                    if ((forceOverwrite || !_d0Txt) && !string.IsNullOrWhiteSpace(txt)) txt0_Text.Text = txt;
                }
            }
            catch { }
            finally
            {
                _applyingRules = false;
            }
        }

        private static int? TryParseInt(string s)
        {
            return int.TryParse((s ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var v) ? (int?)v : null;
        }

        private static decimal ClampToMoney(decimal v)
        {
            return Math.Min(100000000M, Math.Max(-100000000M, decimal.Round(v, 2)));
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
    }
}
