using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace TaMi_Automatenclient
{
    public class AbrechnungsBedingungEditorForm : Form
    {
        public class Kondition { public string Feld { get; set; } public string Operator { get; set; } public string Wert { get; set; } }
        public class Ergebnis { public string Feld { get; set; } public string Wert { get; set; } }

        private const int HeaderHeight = 56;
        private static readonly Color Accent = Color.FromArgb(33, 150, 243);
        private static readonly Color Accent2 = Color.FromArgb(33, 203, 243);

        public List<Kondition> Bedingungen { get; private set; } = new List<Kondition>();
        public string Verknuepfung { get; private set; } = "AND";
        public List<Ergebnis> Ergebnisse { get; private set; } = new List<Ergebnis>();
        public bool IsDefault { get; private set; }
        public int Priority { get; private set; } = 100;
        public string RuleName { get; private set; }

        private Panel header;
        private FlowLayoutPanel pnlBedingungen;
        private FlowLayoutPanel pnlErgebnisse;
        private ComboBox cboJoin;
        private CheckBox chkDefault;
        private NumericUpDown nudPriority;
        private TextBox txtName;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            var rect = new Rectangle(0, 0, ClientSize.Width, HeaderHeight);
            using (var br = new LinearGradientBrush(rect, Accent, Accent2, 0f))
                e.Graphics.FillRectangle(br, rect);
        }

        public AbrechnungsBedingungEditorForm() 
        {
            this.Icon = Program.AppIcon;
            BuildUi(); 
        }

        // L�dt eine bestehende Regel in die UI
        public void LoadFromRule(AbrechnungsRegel r)
        {
            if (r == null) return;

            try { if (txtName != null) txtName.Text = r.Name ?? string.Empty; } catch { }

            // Join, Default, Priority
            try
            {
                var join = string.IsNullOrWhiteSpace(r.JoinKind) ? "AND" : r.JoinKind.ToUpperInvariant();
                if (cboJoin.Items.Contains(join)) cboJoin.SelectedItem = join; else cboJoin.SelectedIndex = 0;
            }
            catch { }

            try { chkDefault.Checked = r.IsDefault; } catch { }
            try
            {
                var v = Math.Max((decimal)nudPriority.Minimum, Math.Min((decimal)nudPriority.Maximum, r.Priority));
                nudPriority.Value = v;
            }
            catch { }

            // Bedingungen zur�cksetzen und neu aufbauen
            try { pnlBedingungen.SuspendLayout(); } catch { }
            pnlBedingungen.Controls.Clear();

            // Hilfsfunktion zum Anlegen einer Bedingungszeile
            Panel AddCond(string feld, string op, string val)
            {
                var row = (Panel)CreateConditionRow();
                var combos = row.Controls.OfType<ComboBox>().ToList();
                var txt = row.Controls.OfType<TextBox>().FirstOrDefault();
                if (combos.Count >= 2)
                {
                    var cbField = combos[0]; var cbOp = combos[1];
                    if (!cbField.Items.Contains(feld)) cbField.Items.Add(feld);
                    cbField.SelectedItem = feld;
                    if (!cbOp.Items.Contains(op)) cbOp.Items.Add(op);
                    cbOp.SelectedItem = op;
                }
                if (txt != null)
                {
                    txt.ForeColor = Color.Black; // sicher, dass Placeholder nicht sichtbar ist
                    txt.Text = val ?? string.Empty;
                }
                pnlBedingungen.Controls.Add(row);
                return row;
            }

            // 1) Aus expliziten Feldern (ManId/PersId/FhzIds)
            if (r.ManId.HasValue) AddCond("ManID", "=", r.ManId.Value.ToString());
            if (r.PersId.HasValue) AddCond("PersId", "=", r.PersId.Value.ToString());
            if (r.FhzIds != null && r.FhzIds.Count > 0) AddCond("FhzId", "IN", string.Join(";", r.FhzIds));

            // 2) Aus Clauses
            if (r.Clauses != null)
            {
                // Gruppen nicht zwingend visualisiert; linear hinzuf�gen
                foreach (var c in r.Clauses)
                {
                    string feld = c.Field ?? string.Empty;
                    string op = string.IsNullOrWhiteSpace(c.Operator) ? "=" : c.Operator;
                    string val = c.Value ?? string.Empty;
                    AddCond(feld, op, val);
                }
            }

            if (pnlBedingungen.Controls.Count == 0)
            {
                // Mindestens eine Zeile anzeigen
                pnlBedingungen.Controls.Add(CreateConditionRow());
            }
            try { pnlBedingungen.ResumeLayout(); } catch { }

            // Ergebnisse zur�cksetzen und neu aufbauen
            try { pnlErgebnisse.SuspendLayout(); } catch { }
            pnlErgebnisse.Controls.Clear();

            void AddResult(string feld, string wert)
            {
                var row = (Panel)CreateResultRow();
                var cb = row.Controls.OfType<ComboBox>().FirstOrDefault();
                var txt = row.Controls.OfType<TextBox>().FirstOrDefault();
                if (cb != null)
                {
                    if (!cb.Items.Contains(feld)) cb.Items.Add(feld);
                    cb.SelectedItem = feld;
                }
                if (txt != null) txt.Text = wert ?? string.Empty;
                pnlErgebnisse.Controls.Add(row);
            }

            if (r.ResultKost1.HasValue) AddResult("Kost1", r.ResultKost1.Value.ToString());
            if (r.ResultKost2.HasValue) AddResult("Kost2", r.ResultKost2.Value.ToString());
            if (r.ResultKonto.HasValue) AddResult("Konto", r.ResultKonto.Value.ToString());
            if (!string.IsNullOrWhiteSpace(r.ResultBuchungstext)) AddResult("Buchungstext", r.ResultBuchungstext);

            if (pnlErgebnisse.Controls.Count == 0)
            {
                pnlErgebnisse.Controls.Add(CreateResultRow());
            }
            try { pnlErgebnisse.ResumeLayout(); } catch { }
        }

        private void BuildUi()
        {
            Text = "Regel bearbeiten"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; 
            // Gr��e an Arbeitsbereich anpassen, damit unten die Buttons sichtbar bleiben
            var wa = Screen.PrimaryScreen.WorkingArea;
            int targetW = Math.Min(1200, Math.Max(900, wa.Width - 200));
            int targetH = Math.Min(1000, Math.Max(800, wa.Height - 200));
            ClientSize = new Size(targetW, targetH);
            MinimumSize = new Size(900, 760);
            MaximizeBox = true; MinimizeBox = true; DoubleBuffered = true; Font = new Font("Segoe UI Variable", 10F);

            header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = Color.Transparent };
            var lbl = new Label { Text = "Bedingungen und Ergebnisse", Left = 16, Top = 0, Width = 640, Height = HeaderHeight, ForeColor = Color.White, Font = new Font("Segoe UI Variable", 14F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            header.Controls.Add(lbl); Controls.Add(header);

            var lblName = new Label { Text = "Regelname:", Left = 16, Top = HeaderHeight + 8, Width = 100, Height = 28 };
            txtName = new TextBox { Left = 120, Top = HeaderHeight + 6, Width = 520 };
            Controls.Add(lblName); Controls.Add(txtName);

            var grpTop = new GroupBox { Text = "Bedingungen (TSchichten/TKassenbuch)", Left = 12, Top = HeaderHeight + 48, Width = ClientSize.Width - 24, Height = 320, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            grpTop.Font = new Font("Segoe UI Variable", 10F, FontStyle.Bold);
            pnlBedingungen = new FlowLayoutPanel { Left = 10, Top = 24, Width = grpTop.Width - 24, Height = 240, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            pnlBedingungen.Controls.Add(CreateConditionRow());
            var btnAddCond = new Button { Text = "+", Width = 36, Height = 30, Left = 10, Top = pnlBedingungen.Bottom + 6, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAddCond.FlatAppearance.BorderSize = 0; btnAddCond.Click += (s, e) => { AddJoinIfNeeded(); pnlBedingungen.Controls.Add(CreateConditionRow()); UpdateJoinIndicators(); };
            cboJoin = new ComboBox { Left = 56, Top = pnlBedingungen.Bottom + 6, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cboJoin.Items.AddRange(new object[] { "AND", "OR" }); cboJoin.SelectedIndex = 0; cboJoin.SelectedIndexChanged += (s, e) => UpdateJoinIndicators();
            grpTop.Controls.AddRange(new Control[] { pnlBedingungen, btnAddCond, cboJoin });
            Controls.Add(grpTop);

            var grpBottom = new GroupBox { Text = "Ergebnisse (TKassenbuch)", Left = 12, Top = grpTop.Bottom + 8, Width = ClientSize.Width - 24, Height = 220, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            grpBottom.Font = new Font("Segoe UI Variable", 10F, FontStyle.Bold);
            pnlErgebnisse = new FlowLayoutPanel { Left = 10, Top = 24, Width = grpBottom.Width - 24, Height = 140, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            pnlErgebnisse.Controls.Add(CreateResultRow());
            var btnAddRes = new Button { Text = "+", Width = 36, Height = 30, Left = 10, Top = pnlErgebnisse.Bottom + 6, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; btnAddRes.FlatAppearance.BorderSize = 0; btnAddRes.Click += (s, e) => pnlErgebnisse.Controls.Add(CreateResultRow());
            grpBottom.Controls.AddRange(new Control[] { pnlErgebnisse, btnAddRes }); Controls.Add(grpBottom);

            chkDefault = new CheckBox { Text = "Standard-Regel (Fallback)", Left = 12, Top = grpBottom.Bottom + 6, Width = 280 };
            nudPriority = new NumericUpDown { Left = chkDefault.Right + 16, Top = grpBottom.Bottom + 4, Width = 120, Minimum = 0, Maximum = 10000, Value = 100 };
            Controls.Add(chkDefault); Controls.Add(nudPriority);

            var btnOk = new Button { Text = "OK", Width = 160, Height = 40, Left = ClientSize.Width - 340, Top = ClientSize.Height - 56, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0; btnOk.Click += (s, e) => { Collect(); DialogResult = DialogResult.OK; };
            var btnCancel = new Button { Text = "Abbrechen", Width = 160, Height = 40, Left = ClientSize.Width - 170, Top = ClientSize.Height - 56, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, BackColor = Color.Gainsboro, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0; btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            Controls.Add(btnOk); Controls.Add(btnCancel);
        }

        private Control CreateConditionRow()
        {
            var panel = new Panel { Width = 752, Height = 34, BackColor = Color.FromArgb(248, 250, 255) };
            var cboField = new ComboBox { Left = 4, Top = 4, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            cboField.Items.AddRange(new object[] { "ManID", "FhzId", "PersId", "StartZeit", "EndZeit", "EinnahmenBar", "EinnahmenBar1", "EinnahmenBar2", "EinnahmenBar3", "EinnahmenUnbar", "AusgabenBar", "AusgabenBar1", "AusgabenBar2", "AusgabenBar3", "AusgabenUnbar", "EinnahmenTaxameter", "EinzahlungFahrer", "EinzahlungFahrer1", "EinzahlungFahrer2", "EinzahlungFahrer3", "Bemerkung", "MwSt", "Typ" });
            cboField.SelectedIndex = 0;
            var cboOp = new ComboBox { Left = 270, Top = 4, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            cboOp.Items.AddRange(new object[] { "=", "<>", "!=", ">", ">=", "<", "<=", "IN" }); cboOp.SelectedIndex = 0;
            var txtVal = new TextBox { Left = 366, Top = 4, Width = 314 };
            var help = "Wert (bei MwSt: 19|7|0, IN: 1;2;3)";
            txtVal.ForeColor = Color.DarkGray; txtVal.Text = help;
            txtVal.GotFocus += (s, e) => { if (txtVal.Text == help) { txtVal.Text = string.Empty; txtVal.ForeColor = Color.Black; } };
            txtVal.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtVal.Text)) { txtVal.Text = help; txtVal.ForeColor = Color.DarkGray; } };
            var tool = new ToolTip(); tool.SetToolTip(txtVal, help);

            var btnDel = new Button { Text = "-", Left = 684, Top = 3, Width = 30, Height = 28, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDel.FlatAppearance.BorderSize = 0; btnDel.Click += (s, e) => RemoveConditionRow(panel);
            panel.Controls.Add(cboField); panel.Controls.Add(cboOp); panel.Controls.Add(txtVal); panel.Controls.Add(btnDel);
            return panel;
        }

        private void RemoveConditionRow(Panel row)
        {
            int idx = pnlBedingungen.Controls.GetChildIndex(row, false);
            if (idx > 0)
            {
                var prev = pnlBedingungen.Controls[idx - 1];
                if (prev is Label lbl && (string)lbl.Tag == "join") pnlBedingungen.Controls.Remove(prev);
            }
            pnlBedingungen.Controls.Remove(row);
            if (pnlBedingungen.Controls.Count > 0)
            {
                var first = pnlBedingungen.Controls[0] as Label;
                if (first != null && (string)first.Tag == "join") pnlBedingungen.Controls.Remove(first);
            }
            UpdateJoinIndicators();
        }

        private Control CreateResultRow()
        {
            var panel = new Panel { Width = 752, Height = 34, BackColor = Color.FromArgb(248, 250, 255) };
            var cboField = new ComboBox { Left = 4, Top = 4, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            cboField.Items.AddRange(new object[] { "Kost1", "Kost2", "Konto", "Buchungstext" }); cboField.SelectedIndex = 0;
            var txtVal = new TextBox { Left = 270, Top = 4, Width = 380 };
            var btnDel = new Button { Text = "-", Left = 654, Top = 3, Width = 30, Height = 28, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDel.FlatAppearance.BorderSize = 0; btnDel.Click += (s, e) => pnlErgebnisse.Controls.Remove(panel);
            panel.Controls.Add(cboField); panel.Controls.Add(txtVal); panel.Controls.Add(btnDel);
            return panel;
        }

        private void AddJoinIfNeeded()
        {
            if (pnlBedingungen.Controls.Count > 0)
            {
                var lbl = new Label { Text = (cboJoin.SelectedItem as string) ?? "AND", AutoSize = false, Width = 752, Height = 22, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray, Tag = "join" };
                pnlBedingungen.Controls.Add(lbl);
            }
        }

        private void UpdateJoinIndicators()
        {
            string txt = (cboJoin.SelectedItem as string) ?? "AND";
            foreach (Control c in pnlBedingungen.Controls)
            {
                var lbl = c as Label; if (lbl != null && (string)lbl.Tag == "join") lbl.Text = txt;
            }
        }

        private void Collect()
        {
            Bedingungen.Clear(); Ergebnisse.Clear();
            Verknuepfung = (cboJoin.SelectedItem as string) ?? "AND";
            IsDefault = chkDefault.Checked; Priority = (int)nudPriority.Value;
            RuleName = (txtName?.Text ?? string.Empty).Trim();
            foreach (Control c in pnlBedingungen.Controls)
            {
                var row = c as Panel; if (row == null) continue;
                var combos = row.Controls.OfType<ComboBox>().ToList();
                var txt = row.Controls.OfType<TextBox>().FirstOrDefault();
                if (combos.Count >= 2 && txt != null)
                {
                    var feld = combos[0].SelectedItem as string ?? string.Empty;
                    var op = combos[1].SelectedItem as string ?? string.Empty;
                    var val = txt.Text ?? string.Empty;
                    // Placeholder nicht speichern
                    if (txt.ForeColor == Color.DarkGray)
                        val = string.Empty;
                    Bedingungen.Add(new Kondition { Feld = feld, Operator = op, Wert = val });
                }
            }
            foreach (Panel row in pnlErgebnisse.Controls.OfType<Panel>())
            {
                var cbo = row.Controls.OfType<ComboBox>().First();
                var txt = row.Controls.OfType<TextBox>().First();
                var feld = cbo.SelectedItem as string ?? string.Empty;
                var wert = txt.Text ?? string.Empty;
                Ergebnisse.Add(new Ergebnis { Feld = feld, Wert = wert });
            }
        }
    }
}
