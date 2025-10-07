using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace TaMi_Kassenclient
{
    public class PersonalForm : Form
    {
        private Panel headerPanel;
        private Button btnClose;
        private Button btnMinimize;
        private Label lblTitle;
        private Point _mouseDownLocation;

        // Linke Seite gruppiert
        private GroupBox grpMitarbeiter;
        private GroupBox grpStammdaten;

        private TextBox txtPid;
        private ComboBox cboPerson; // Dropdown mit aktiven Mitarbeitern
        private Label lblName;      // Wertanzeige
        private Label lblVorname;   // Wertanzeige
        private TextBox txtNfc;
        private TextBox txtFahrercode;
        private Button btnSave;
        private Button btnClearNfc;
        private Button btnShowHideCode;
        private Button btnClearCode;
        private Button btnNfcUebernehmen;
        private int _currentPid = 0;
        private bool _suppressEvents = false; // Neu: verhindert Initial-Handler

        // NEU: Mehrzeichen-Suchpuffer für cboPerson
        private string _personTypeBuffer = string.Empty;
        private DateTime _personTypeLastKey = DateTime.MinValue;
        private const int PersonTypeTimeoutMs = 1000; // 1s Timeout

        // Rechte Seite
        private GroupBox grpOpenShifts;
        private GroupBox grpOpenPayments;
        private DataGridView gvOpenShifts;
        private DataGridView gvOpenPayments;

        // NEU: Guthaben-Verlauf
        private GroupBox grpGuthabenHistory;
        private DataGridView gvGuthabenHistory;
        private Label lblSaldoAktuell; // Anzeige aktuelles Saldo

        // Neue Zahlung UI (links, schmal)
        private GroupBox grpNewPayment;
        private ComboBox cboPreset;      // Vorlagen-Auswahl
        private ComboBox cboNewType;     // Typ: Einzahlung/Auszahlung
        private ComboBox cboNewMwst;
        private ComboBox cboMandant;     // NEU: Kasse (Mandant)
        private TextBox txtNewPayText;
        private NumericUpDown nudNewAmount;
        private NumericUpDown nudNew19, nudNew7, nudNew0; // intern (versteckt)
        private TextBox txtNewK1, txtNewK2, txtNewKonto;
        private Button btnCreatePayment;
        private Button btnCreatePreset; // NEU

        // Overlay für neue Vorlage
        private Panel overlay;
        private ComboBox ovType;     // NEU: Typ in Vorlage
        private TextBox ovName;      // NEU: Vorlagenname
        private TextBox ovTxt, ovK1, ovK2, ovKto;
        private ComboBox ovMwst;     // NEU MwSt für Vorlage
        private Button ovSave, ovCancel;

        // Aktionen für offene Zahlungen
        private Button btnEditPayment;
        private Button btnDeletePayment;

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private bool _firstShown = true; // Erstes Anzeigen kontrollieren

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_COMPOSITED: 0x02000000 -> reduziert Flackern durch Compositing
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        public PersonalForm()
        {
            // Erst unsichtbar, um "unsauberes" Aufbauen zu vermeiden
            try { Opacity = 0; } catch { }
            InitUi();
        }

        private void EnableDoubleBuffer(Control ctl)
        {
            try
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                var pi = ctl.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue( ctl, true, null);
                foreach (Control c in ctl.Controls)
                {
                    EnableDoubleBuffer(c);
                }
            }
            catch { }
        }

        private void InitUi()
        {
            SuspendLayout();
            try
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(1320, 1040);
                AutoScroll = true;
                BackColor = Color.White;
                DoubleBuffered = true;
                KeyPreview = true;
                this.KeyDown += PersonalForm_KeyDown;
                this.KeyPress += PersonalForm_KeyPress;

                headerPanel = new Panel { Location = new Point(0, 0), Size = new Size(ClientSize.Width, 64), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                headerPanel.Paint += HeaderPanel_Paint;
                headerPanel.MouseDown += HeaderPanel_MouseDown;
                headerPanel.MouseMove += HeaderPanel_MouseMove;
                headerPanel.SuspendLayout();
                Controls.Add(headerPanel);

                lblTitle = new Label { Text = "Personal", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(24, 0), Size = new Size(400, 64), BackColor = Color.Transparent };
                headerPanel.Controls.Add(lblTitle);

                btnClose = new Button { Text = "\u2715", Font = new Font("Segoe UI Symbol", 16F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat, Size = new Size(48, 48), Location = new Point(ClientSize.Width - 56, 8), TabStop = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80);
                btnClose.Click += (s, e) => Close();
                headerPanel.Controls.Add(btnClose);

                btnMinimize = new Button { Text = "–", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat, Size = new Size(48, 48), Location = new Point(ClientSize.Width - 112, 8), TabStop = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                btnMinimize.FlatAppearance.BorderSize = 0;
                btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 150, 243, 80);
                btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;
                headerPanel.Controls.Add(btnMinimize);
                headerPanel.ResumeLayout();

                // Rundung später in OnShown setzen (vermeidet schwarzes Flackern beim Erzeugen)
                // try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20)); } catch { }

                // Linke Seite: strukturierte Gruppen
                BuildLeftGroups();

                // Rechte Seite: Gruppen ganz nach oben unter Header
                int rightTop = headerPanel.Bottom + 12;
                grpOpenShifts = new GroupBox { Text = "Offene Schichten", Location = new Point(540, rightTop), Size = new Size(ClientSize.Width - 564, 300), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                grpOpenShifts.SuspendLayout();
                gvOpenShifts = new DataGridView { Location = new Point(10, 24), Size = new Size(grpOpenShifts.Width - 20, grpOpenShifts.Height - 34), ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                StyleGrid(gvOpenShifts); EnableDgvDoubleBuffer(gvOpenShifts);
                grpOpenShifts.Controls.Add(gvOpenShifts);
                grpOpenShifts.ResumeLayout();
                Controls.Add(grpOpenShifts);

                grpOpenPayments = new GroupBox { Text = "Offene Zahlungen", Location = new Point(540, grpOpenShifts.Bottom + 12), Size = new Size(ClientSize.Width - 564, 280), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                grpOpenPayments.SuspendLayout();
                gvOpenPayments = new DataGridView { Location = new Point(10, 24), Size = new Size(grpOpenPayments.Width - 20, grpOpenPayments.Height - 34), ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                StyleGrid(gvOpenPayments); EnableDgvDoubleBuffer(gvOpenPayments);
                gvOpenPayments.SelectionChanged += GvOpenPayments_SelectionChanged;
                gvOpenPayments.CellDoubleClick += (s, e) => LoadSelectedPaymentIntoFields();
                grpOpenPayments.Controls.Add(gvOpenPayments);
                grpOpenPayments.ResumeLayout();
                Controls.Add(grpOpenPayments);

                // Edit/Löschen Buttons unter dem Grid für Offene Zahlungen
                btnEditPayment = new Button { Text = "Auswahl ändern", Location = new Point(540, 0), Size = new Size(140, 28), BackColor = Color.FromArgb(3,155,229), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnEditPayment.FlatAppearance.BorderSize = 0; btnEditPayment.Click += async (s, e) => await EditSelectedPaymentAsync();
                btnDeletePayment = new Button { Text = "Auswahl löschen", Location = new Point(688, 0), Size = new Size(140, 28), BackColor = Color.FromArgb(229,57,53), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnDeletePayment.FlatAppearance.BorderSize = 0; btnDeletePayment.Click += async (s, e) => await DeleteSelectedPaymentAsync();
                btnEditPayment.Enabled = false;
                btnDeletePayment.Enabled = false;
                Controls.Add(btnEditPayment);
                Controls.Add(btnDeletePayment);

                // NEU: Personalguthaben-Verlauf unter den Buttons
                grpGuthabenHistory = new GroupBox { Text = "Personalguthaben – Verlauf", Location = new Point(540, grpOpenPayments.Bottom + 48), Size = new Size(ClientSize.Width - 564, 280), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                grpGuthabenHistory.SuspendLayout();
                gvGuthabenHistory = new DataGridView { Location = new Point(10, 24), Size = new Size(grpGuthabenHistory.Width - 20, grpGuthabenHistory.Height - 34), ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                StyleGrid(gvGuthabenHistory); EnableDgvDoubleBuffer(gvGuthabenHistory);
                grpGuthabenHistory.Controls.Add(gvGuthabenHistory);
                grpGuthabenHistory.ResumeLayout();
                Controls.Add(grpGuthabenHistory);

                // Saldo-Label rechts oben neben dem Verlauf
                lblSaldoAktuell = new Label { AutoSize = true, Text = "Saldo: 0,00 €", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(33,37,41), Location = new Point(grpGuthabenHistory.Left + 200, grpGuthabenHistory.Top - 18), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                Controls.Add(lblSaldoAktuell);

                // Rekursiv DoubleBuffer aktivieren
                EnableDoubleBuffer(this);

                // Typen initial laden (parallel)
                _ = LoadActivePersonalAsync();
                _ = LoadAccountingPresetsAsync();
                _ = LoadMandantenAsync();
            }
            finally { ResumeLayout(true); }
        }

        // Inkrementelle Mehrzeichen-Suche für cboPerson
        private void CboPerson_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (cboPerson == null || cboPerson.Items.Count == 0) return;
            if (char.IsControl(e.KeyChar))
            {
                if (e.KeyChar == (char)Keys.Back && _personTypeBuffer.Length > 0)
                {
                    _personTypeBuffer = _personTypeBuffer.Substring(0, _personTypeBuffer.Length - 1);
                    e.Handled = true;
                    PerformPersonSearch();
                }
                return;
            }
            var now = DateTime.UtcNow;
            if ((now - _personTypeLastKey).TotalMilliseconds > PersonTypeTimeoutMs)
                _personTypeBuffer = string.Empty;
            _personTypeLastKey = now;
            _personTypeBuffer += e.KeyChar.ToString();
            e.Handled = true;
            PerformPersonSearch();
        }

        private void PerformPersonSearch()
        {
            if (string.IsNullOrEmpty(_personTypeBuffer)) return;
            string search = _personTypeBuffer.ToLowerInvariant();
            for (int i = 0; i < cboPerson.Items.Count; i++)
            {
                if (cboPerson.Items[i] is DataRowView drv)
                {
                    string name = Convert.ToString(drv["Name"]) ?? string.Empty;
                    if (name.ToLowerInvariant().StartsWith(search))
                    {
                        cboPerson.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        // Umbenannt: frühere doppelte OnShown-Implementierung als Hilfsmethode
        private void OnShown_InitialLayout(EventArgs e)
        {
            try
            {
                if (grpOpenPayments != null && btnEditPayment != null && btnDeletePayment != null)
                {
                    btnEditPayment.Location = new Point(grpOpenPayments.Left, grpOpenPayments.Bottom + 8);
                    btnDeletePayment.Location = new Point(btnEditPayment.Right + 8, grpOpenPayments.Bottom + 8);

                    // Guthaben-Verlauf unter die Buttons setzen
                    if (grpGuthabenHistory != null)
                    {
                        grpGuthabenHistory.Location = new Point(grpOpenPayments.Left, btnDeletePayment.Bottom + 12);
                        grpGuthabenHistory.Width = grpOpenPayments.Width;
                        if (lblSaldoAktuell != null)
                        {
                            lblSaldoAktuell.Location = new Point(grpGuthabenHistory.Right - 220, grpGuthabenHistory.Top - 18);
                        }
                    }
                }

                if (_firstShown)
                {
                    _firstShown = false;
                    // Jetzt erst die abgerundete Region setzen und Form einblenden
                    try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20)); } catch { }
                    try { Opacity = 1; } catch { }
                }
            }
            catch { }
        }

        private void BuildLeftGroups()
        {
            try
            {
                int labelW = 120; int fieldX = 140; int rowH = 34; int startY;

                // Sicherheit: headerPanel kann (bei Designer-/Init-Race) noch null sein
                int headerBottom = 0;
                try { headerBottom = headerPanel != null ? headerPanel.Bottom : 0; } catch { headerBottom = 0; }

                // Gruppe Mitarbeiter
                if (grpMitarbeiter == null)
                    grpMitarbeiter = new GroupBox { Text = "Mitarbeiter" };
                grpMitarbeiter.Location = new Point(16, headerBottom + 12);
                grpMitarbeiter.Size = new Size(500, 150);
                if (!Controls.Contains(grpMitarbeiter)) Controls.Add(grpMitarbeiter);

                startY = 24;
                var lblPid = new Label { Text = "Personalnummer:", Location = new Point(12, startY + 6), AutoSize = true, Width = labelW };
                grpMitarbeiter.Controls.Add(lblPid);
                txtPid = new TextBox { Location = new Point(fieldX, startY), Width = 160, Font = new Font("Segoe UI", 14F), TextAlign = HorizontalAlignment.Center, MaxLength = 8 };
                grpMitarbeiter.Controls.Add(txtPid);
                var btnLoad = new Button { Text = "Laden", Location = new Point(fieldX + 170, startY - 2), Size = new Size(96, 32), BackColor = Color.FromArgb(33,150,243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                btnLoad.FlatAppearance.BorderSize = 0;
                btnLoad.Click += async (s,e) => await LoadPersonalAsync();
                grpMitarbeiter.Controls.Add(btnLoad);

                startY += rowH;
                var lblDrop = new Label { Text = "oder Mitarbeiter:", Location = new Point(12, startY + 6), AutoSize = true, Width = labelW };
                grpMitarbeiter.Controls.Add(lblDrop);
                cboPerson = new ComboBox { Location = new Point(fieldX, startY), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10.5F) };
                grpMitarbeiter.Controls.Add(cboPerson);
                cboPerson.SelectedIndexChanged += async (s, e) =>
                {
                    if (_suppressEvents) return;
                    if (cboPerson.SelectedItem is DataRowView drv)
                    {
                        txtPid.Text = drv["PID"].ToString();
                        await LoadPersonalAsync();
                    }
                };
                // NEU: KeyPress Handler für Mehrzeichen-Suche
                cboPerson.KeyPress += CboPerson_KeyPress;

                // Gruppe Stammdaten
                if (grpStammdaten == null)
                    grpStammdaten = new GroupBox { Text = "Stammdaten" };
                grpStammdaten.Location = new Point(16, grpMitarbeiter.Bottom + 10);
                grpStammdaten.Size = new Size(500, 250);
                if (!Controls.Contains(grpStammdaten)) Controls.Add(grpStammdaten);

                startY = 24;
                var lblNameCaption = new Label { Text = "Name:", Location = new Point(12, startY + 6), AutoSize = true, Width = labelW };
                grpStammdaten.Controls.Add(lblNameCaption);
                lblName = new Label { Text = string.Empty, Location = new Point(fieldX, startY + 6), AutoSize = true };
                grpStammdaten.Controls.Add(lblName);

                startY += rowH;
                var lblVornameCaption = new Label { Text = "Vorname:", Location = new Point(12, startY + 6), AutoSize = true, Width = labelW };
                grpStammdaten.Controls.Add(lblVornameCaption);
                lblVorname = new Label { Text = string.Empty, Location = new Point(fieldX, startY + 6), AutoSize = true };
                grpStammdaten.Controls.Add(lblVorname);

                startY += rowH;
                var lblNfc = new Label { Text = "NFC:", Location = new Point(12, startY + 6), AutoSize = true, Width = labelW };
                grpStammdaten.Controls.Add(lblNfc);
                txtNfc = new TextBox { Location = new Point(fieldX, startY), Width = 200 };
                grpStammdaten.Controls.Add(txtNfc);
                btnClearNfc = new Button { Text = "X", Size = new Size(32, 28), BackColor = Color.FromArgb(229,57,53), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(fieldX + 206, startY) };
                btnClearNfc.FlatAppearance.BorderSize = 0; btnClearNfc.Click += (s,e) => txtNfc.Text = string.Empty; grpStammdaten.Controls.Add(btnClearNfc);
                btnNfcUebernehmen = new Button { Text = "NFC", Size = new Size(72, 28), BackColor = Color.FromArgb(33,150,243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(fieldX + 244, startY) };
                btnNfcUebernehmen.FlatAppearance.BorderSize = 0; btnNfcUebernehmen.Click += (s,e) => TryTakeLastNfc(); grpStammdaten.Controls.Add(btnNfcUebernehmen);

                startY += rowH;
                var lblFcode = new Label { Text = "Fahrercode:", Location = new Point(12, startY + 6), AutoSize = true, Width = labelW };
                grpStammdaten.Controls.Add(lblFcode);
                txtFahrercode = new TextBox { Location = new Point(fieldX, startY), Width = 160, Font = new Font("Segoe UI", 14F), TextAlign = HorizontalAlignment.Center, UseSystemPasswordChar = true, MaxLength = 8 };
                grpStammdaten.Controls.Add(txtFahrercode);
                btnShowHideCode = new Button { Text = "??", Location = new Point(fieldX + 166, startY - 1), Size = new Size(36, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(245,247,250) };
                btnShowHideCode.FlatAppearance.BorderSize = 0; btnShowHideCode.Click += (s,e) => { txtFahrercode.UseSystemPasswordChar = !txtFahrercode.UseSystemPasswordChar; }; grpStammdaten.Controls.Add(btnShowHideCode);
                btnClearCode = new Button { Text = "Löschen", Location = new Point(fieldX + 206, startY - 1), Size = new Size(88, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(229,57,53), ForeColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                btnClearCode.FlatAppearance.BorderSize = 0; btnClearCode.Click += (s,e) => txtFahrercode.Text = string.Empty; grpStammdaten.Controls.Add(btnClearCode);

                startY += rowH + 6;
                btnSave = new Button { Text = "Speichern", Location = new Point(16, startY), Size = new Size(200, 40), BackColor = Color.FromArgb(33,150,243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
                btnSave.FlatAppearance.BorderSize = 0; btnSave.Click += async (s,e) => await SaveAsync(); grpStammdaten.Controls.Add(btnSave);

                // Neue Zahlung (schmal, keine Überlappung rechts)
                grpNewPayment = new GroupBox { Text = "Neue Zahlung", Location = new Point(16, grpStammdaten.Bottom + 12), Size = new Size(470, 320) };
                Controls.Add(grpNewPayment);

                int nzY = 26;
                var lblVorlage = new Label { Text = "Vorlage:", Location = new Point(12, nzY + 3), AutoSize = true };
                cboPreset = new ComboBox { Location = new Point(120, nzY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
                grpNewPayment.Controls.Add(lblVorlage); grpNewPayment.Controls.Add(cboPreset);
                cboPreset.SelectedIndexChanged += (s, e) => ApplyPresetToFields();

                // Button 'Vorlage anlegen' rechts neben Vorlage-Dropdown platzieren
                btnCreatePreset = new Button { Text = "Vorlage anlegen", Location = new Point(120 + 220 + 8, nzY - 1), Size = new Size(120, 26), BackColor = Color.FromArgb(3, 155, 229), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnCreatePreset.FlatAppearance.BorderSize = 0; btnCreatePreset.Click += (s, e) => ShowPresetOverlay();
                grpNewPayment.Controls.Add(btnCreatePreset);

                // NEU: Mandant/Kasse Dropdown zwischen Vorlage und Typ
                nzY += 30;
                var lblMandant = new Label { Text = "Kasse:", Location = new Point(12, nzY + 3), AutoSize = true };
                cboMandant = new ComboBox { Location = new Point(120, nzY), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
                grpNewPayment.Controls.Add(lblMandant); grpNewPayment.Controls.Add(cboMandant);

                nzY += 30;
                var lblTyp = new Label { Text = "Typ:", Location = new Point(12, nzY + 3), AutoSize = true };
                cboNewType = new ComboBox { Location = new Point(120, nzY), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
                cboNewType.Items.AddRange(new object[] { "Einzahlung", "Auszahlung" });
                // Keine Vorauswahl
                cboNewType.SelectedIndex = -1;
                grpNewPayment.Controls.Add(lblTyp); grpNewPayment.Controls.Add(cboNewType);

                nzY += 30;
                var lblMwst = new Label { Text = "MwSt:", Location = new Point(12, nzY + 3), AutoSize = true };
                cboNewMwst = new ComboBox { Location = new Point(120, nzY), Width = 72, DropDownStyle = ComboBoxStyle.DropDownList };
                var lblBetrag = new Label { Text = "Betrag:", Location = new Point(210, nzY + 3), AutoSize = true };
                nudNewAmount = new NumericUpDown { Location = new Point(266, nzY), Width = 80, DecimalPlaces = 2, Maximum = 1000000, Minimum = -1000000, Increment = 0.10M };
                grpNewPayment.Controls.Add(lblMwst); grpNewPayment.Controls.Add(cboNewMwst); grpNewPayment.Controls.Add(lblBetrag); grpNewPayment.Controls.Add(nudNewAmount);

                nzY += 30;
                var lblTxt = new Label { Text = "Buchungstext:", Location = new Point(12, nzY + 3), AutoSize = true };
                txtNewPayText = new TextBox { Location = new Point(120, nzY), Width = 326 };
                grpNewPayment.Controls.Add(lblTxt); grpNewPayment.Controls.Add(txtNewPayText);

                nzY += 30;
                var lblK1 = new Label { Text = "Kost1:", Location = new Point(12, nzY + 3), AutoSize = true };
                txtNewK1 = new TextBox { Location = new Point(120, nzY), Width = 70 };
                var lblK2 = new Label { Text = "Kost2:", Location = new Point(196, nzY + 3), AutoSize = true };
                txtNewK2 = new TextBox { Location = new Point(242, nzY), Width = 70 };
                var lblKto = new Label { Text = "Konto:", Location = new Point(318, nzY + 3), AutoSize = true };
                txtNewKonto = new TextBox { Location = new Point(362, nzY), Width = 84 };
                grpNewPayment.Controls.AddRange(new Control[] { lblK1, txtNewK1, lblK2, txtNewK2, lblKto, txtNewKonto });

                nzY += 32;
                btnCreatePayment = new Button { Text = "Zahlung anlegen", Location = new Point(120, nzY), Size = new Size(160, 28), BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnCreatePayment.FlatAppearance.BorderSize = 0; btnCreatePayment.Click += async (s, e) => await CreatePaymentWithPresetAsync();
                grpNewPayment.Controls.Add(btnCreatePayment);

                // MWSt-Auswahl befüllen (falls noch nicht)
                if (cboNewMwst != null && cboNewMwst.Items.Count == 0)
                {
                    cboNewMwst.Items.AddRange(new object[] { "19", "7", "0" });
                    // Keine Vorauswahl
                    cboNewMwst.SelectedIndex = -1;
                }
                nudNew19 = new NumericUpDown { Visible = false }; // intern
                nudNew7 = new NumericUpDown { Visible = false };
                nudNew0 = new NumericUpDown { Visible = false };
            }
            catch (Exception)
            {
                // UI notfalls minimal anzeigen, aber nicht crashen
                try
                {
                    if (grpMitarbeiter == null)
                    {
                        grpMitarbeiter = new GroupBox { Text = "Mitarbeiter", Location = new Point(16, 80), Size = new Size(500, 150) };
                        Controls.Add(grpMitarbeiter);
                    }
                }
                catch { }
            }
        }

        private void EnableDgvDoubleBuffer(DataGridView gv)
        {
            try
            {
                var prop = typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                prop?.SetValue(gv, true, null);
            }
            catch { }
        }

        private async System.Threading.Tasks.Task LoadMandantenAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.GetMandantenAsync();
                    _suppressEvents = true;
                    cboMandant.DisplayMember = "ManName";
                    cboMandant.ValueMember = "ManID";
                    cboMandant.DataSource = dt;
                    // Keine Vorauswahl
                    cboMandant.SelectedIndex = -1;
                }
            }
            catch { }
            finally { _suppressEvents = false; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            OnShown_InitialLayout(e);
        }

        private async void ShowPresetOverlay()
        {
            var dlg = new Form
            {
                Text = "Vorlagen verwalten",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(740, 500),
                BackColor = Color.White,
                ShowInTaskbar = false
            };
            Button btnSave = null;
            dlg.KeyPreview = true;
            dlg.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); }
                else if (e.KeyCode == Keys.Enter && btnSave != null && btnSave.Enabled) btnSave.PerformClick();
            };

            var header = new Panel { Left = 0, Top = 0, Width = dlg.ClientSize.Width, Height = 54, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            header.Paint += (s, e) =>
            {
                using (var lg = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(33,150,243), Color.FromArgb(25,118,210), 0f))
                    e.Graphics.FillRectangle(lg, header.ClientRectangle);
                using (var pen = new Pen(Color.FromArgb(13, 71, 161)))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            var hdrLabel = new Label { Text = "Vorlagen verwalten", AutoSize = false, Left = 20, Top = 0, Width = 400, Height = 54, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            var hdrClose = new Button { Text = "X", FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Size = new Size(48, 48), Location = new Point(header.Width - 56, 3), Anchor = AnchorStyles.Top | AnchorStyles.Right, TabStop = false };
            hdrClose.FlatAppearance.BorderSize = 0; hdrClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(229, 57, 53); hdrClose.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            header.Controls.Add(hdrLabel); header.Controls.Add(hdrClose); dlg.Controls.Add(header);

            var body = new Panel { Left = 0, Top = header.Bottom, Width = dlg.ClientSize.Width, Height = dlg.ClientSize.Height - header.Height, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.White };
            dlg.Controls.Add(body);

            string searchPlaceholder = "Suchen...";
            var txtSearch = new TextBox { Left = 24, Top = 12, Width = 220 };
            txtSearch.ForeColor = Color.Gray; txtSearch.Text = searchPlaceholder;
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == searchPlaceholder) { txtSearch.Text = string.Empty; txtSearch.ForeColor = Color.Black; } };
            txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = searchPlaceholder; txtSearch.ForeColor = Color.Gray; } };
            var lst = new ListBox { Left = 24, Top = txtSearch.Bottom + 6, Width = 220, Height = 300, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom, BorderStyle = BorderStyle.FixedSingle };
            body.Controls.Add(txtSearch); body.Controls.Add(lst);

            var btnNeu = new Button { Text = "Neu", Left = 24, Width = 80, Height = 34, Top = body.Height - 46, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = Color.FromArgb(33,150,243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNeu.FlatAppearance.BorderSize = 0;
            var btnDelete = new Button { Text = "Löschen", Left = btnNeu.Right + 8, Width = 90, Height = 34, Top = body.Height - 46, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = Color.FromArgb(229,57,53), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDelete.FlatAppearance.BorderSize = 0; body.Controls.Add(btnNeu); body.Controls.Add(btnDelete);

            int baseX = 260; int wLabel = 60; int curY = 14; int spacing = 30;
            Func<string, Label> makeLbl = t => new Label { Text = t, Left = baseX, Top = curY + 4, Width = wLabel, ForeColor = Color.FromArgb(55,71,79) };
            var cbTyp = new ComboBox { Left = baseX + wLabel + 4, Top = curY, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            cbTyp.Items.AddRange(new object[] { "Einzahlung", "Auszahlung" }); cbTyp.SelectedIndex = 0; body.Controls.Add(makeLbl("Typ:")); body.Controls.Add(cbTyp); curY += spacing;
            var tbName = new TextBox { Left = baseX + wLabel + 4, Top = curY, Width = 280 }; body.Controls.Add(makeLbl("Name:")); body.Controls.Add(tbName); curY += spacing;
            var cbMwst = new ComboBox { Left = baseX + wLabel + 4, Top = curY, Width = 60, DropDownStyle = ComboBoxStyle.DropDownList }; cbMwst.Items.AddRange(new object[] { "19", "7", "0" }); cbMwst.SelectedIndex = 0; body.Controls.Add(makeLbl("MwSt:")); body.Controls.Add(cbMwst); curY += spacing;
            var tbTxt = new TextBox { Left = baseX + wLabel + 4, Top = curY, Width = 340 }; body.Controls.Add(makeLbl("Text:")); body.Controls.Add(tbTxt); curY += spacing;
            var tbK1 = new TextBox { Left = baseX + wLabel + 4, Top = curY, Width = 70 }; var lblK2 = new Label { Text = "Kost2:", Left = tbK1.Right + 14, Top = curY + 4, Width = 45, ForeColor = Color.FromArgb(55,71,79) }; var tbK2 = new TextBox { Left = lblK2.Right + 4, Top = curY, Width = 70 }; var lblKto = new Label { Text = "Konto:", Left = tbK2.Right + 14, Top = curY + 4, Width = 50, ForeColor = Color.FromArgb(55,71,79) }; var tbKto = new TextBox { Left = lblKto.Right + 4, Top = curY, Width = 80 }; body.Controls.Add(makeLbl("Kost1:")); body.Controls.Add(tbK1); body.Controls.Add(lblK2); body.Controls.Add(tbK2); body.Controls.Add(lblKto); body.Controls.Add(tbKto); curY += spacing + 6;

            var btnCancel = new Button { Text = "Abbrechen", Left = baseX + wLabel + 4, Top = body.Height - 46, Width = 140, Height = 40, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.Gainsboro, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0; btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            btnSave = new Button { Text = "Speichern", Left = btnCancel.Right + 12, Top = body.Height - 46, Width = 160, Height = 40, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.FromArgb(46,125,50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
            btnSave.FlatAppearance.BorderSize = 0; body.Controls.Add(btnCancel); body.Controls.Add(btnSave);

            int? editBeleg = null; var allItems = new System.Collections.Generic.List<ComboItem>();
            Action applyFilter = () => { string f = (txtSearch.Text == searchPlaceholder ? string.Empty : txtSearch.Text).Trim().ToLowerInvariant(); lst.BeginUpdate(); lst.Items.Clear(); foreach (var item in allItems) if (f.Length == 0 || item.Text.ToLowerInvariant().Contains(f)) lst.Items.Add(item); lst.EndUpdate(); };
            Action clearFields = () => { editBeleg = null; cbTyp.SelectedIndex = 0; cbMwst.SelectedIndex = 0; tbName.Clear(); tbTxt.Clear(); tbK1.Clear(); tbK2.Clear(); tbKto.Clear(); lst.ClearSelected(); btnSave.Enabled = false; };

            async Task loadListAsync()
            {
                allItems.Clear();
                try
                {
                    using (var db = new DatabaseHelperKassen())
                    {
                        var dt = await db.LoadZahlungsVorlagenAsync();
                        foreach (DataRow r in dt.Rows)
                        {
                            string name = Convert.ToString(r["VorlagenName"]); if (string.IsNullOrWhiteSpace(name)) name = "(ohne Name)"; allItems.Add(new ComboItem { Text = name, Row = r });
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show(dlg, "Vorlagen konnten nicht geladen werden:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                applyFilter();
            }

            txtSearch.TextChanged += (s, e) => applyFilter();
            btnNeu.Click += (s, e) => clearFields();
            lst.SelectedIndexChanged += (s, e) =>
            {
                if (lst.SelectedItem is ComboItem ciSel && ciSel.Row != null)
                {
                    var r = ciSel.Row; editBeleg = r.Table.Columns.Contains("Belegnummer") ? (int?)Convert.ToInt32(r["Belegnummer"]) : null;
                    cbTyp.SelectedIndex = cbTyp.FindStringExact(Convert.ToString(r["Typ"]) ?? "Einzahlung");
                    tbName.Text = Convert.ToString(r["VorlagenName"]) ?? string.Empty;
                    tbTxt.Text = Convert.ToString(r["Buchungstext"]) ?? string.Empty;
                    tbK1.Text = r["Kost1"] == DBNull.Value ? string.Empty : Convert.ToString(r["Kost1"]);
                    tbK2.Text = r["Kost2"] == DBNull.Value ? string.Empty : Convert.ToString(r["Kost2"]);
                    tbKto.Text = r["Konto"] == DBNull.Value ? string.Empty : Convert.ToString(r["Konto"]);
                    string mw = "19";
                    try
                    {
                        decimal m19 = r["Betrag19"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Betrag19"]);
                        decimal m7 = r["Betrag7"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Betrag7"]);
                        decimal m0 = r["Betrag0"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Betrag0"]);
                        if (m7 == 1m && m19 == 0m && m0 == 0m) mw = "7"; else if (m0 == 1m && m19 == 0m && m7 == 0m) mw = "0";
                    }
                    catch { }
                    cbMwst.SelectedIndex = cbMwst.FindStringExact(mw);
                    btnSave.Enabled = !string.IsNullOrWhiteSpace(tbName.Text);
                }
            };
            btnDelete.Click += async (s, e) =>
            {
                if (!(lst.SelectedItem is ComboItem ciDel) || editBeleg == null) return;
                if (MessageBox.Show(dlg, "Vorlage wirklich löschen?", "Bestätigung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    using (var db = new DatabaseHelperKassen()) await db.UpdateZahlungsVorlageAsync(editBeleg.Value, "Einzahlung", "Vorlage", "", null, null, null, "19");
                }
                catch (Exception ex) { MessageBox.Show(dlg, "Löschen fehlgeschlagen: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                await loadListAsync(); clearFields();
            };
            tbName.TextChanged += (s, e) => btnSave.Enabled = !string.IsNullOrWhiteSpace(tbName.Text);
            btnSave.Click += async (s, e) =>
            {
                string typ = cbTyp.SelectedItem as string; string name = tbName.Text?.Trim(); if (string.IsNullOrWhiteSpace(name)) return; string mwst = cbMwst.SelectedItem as string; string txtVal = tbTxt.Text?.Trim(); string k1Val = string.IsNullOrWhiteSpace(tbK1.Text) ? null : tbK1.Text.Trim(); string k2Val = string.IsNullOrWhiteSpace(tbK2.Text) ? null : tbK2.Text.Trim(); string ktoVal = string.IsNullOrWhiteSpace(tbKto.Text) ? null : tbKto.Text.Trim();
                try
                {
                    using (var db = new DatabaseHelperKassen())
                    {
                        if (editBeleg.HasValue)
                            await db.UpdateZahlungsVorlageAsync(editBeleg.Value, typ, name, txtVal, k1Val, k2Val, ktoVal, mwst);
                        else
                            await db.InsertZahlungsVorlageAsync(typ, name, txtVal, k1Val, k2Val, ktoVal, mwst);
                    }
                    dlg.Tag = name; dlg.DialogResult = DialogResult.OK; dlg.Close();
                }
                catch (Exception ex2) { MessageBox.Show(dlg, "Fehler: " + ex2.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            await loadListAsync();
            dlg.Shown += (s, e) => { if (txtSearch.Text == searchPlaceholder) txtSearch.Select(0, 0); else txtSearch.Focus(); };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string lastName = dlg.Tag as string;
                _ = LoadAccountingPresetsAsync().ContinueWith(t => { try { if (InvokeRequired) BeginInvoke(new Action(() => SelectPresetByName(lastName))); else SelectPresetByName(lastName); } catch { } });
            }
            dlg.Dispose();
        }

        private void SelectPresetByName(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName) || cboPreset == null) return;
            for (int i = 0; i < cboPreset.Items.Count; i++)
            {
                if (cboPreset.Items[i] is ComboItem ci && string.Equals(ci.Text, presetName, StringComparison.OrdinalIgnoreCase))
                { cboPreset.SelectedIndex = i; break; }
            }
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            var rect = headerPanel.ClientRectangle;
            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(25, 118, 210), Color.FromArgb(21, 101, 192), 0f))
            { e.Graphics.FillRectangle(brush, rect); }
            using (var pen = new Pen(Color.FromArgb(13, 71, 161), 1))
            { e.Graphics.DrawLine(pen, 0, rect.Bottom - 1, rect.Right, rect.Bottom - 1); }
        }
        private void HeaderPanel_MouseDown(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) _mouseDownLocation = e.Location; }
        private void HeaderPanel_MouseMove(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Left += e.X - _mouseDownLocation.X; Top += e.Y - _mouseDownLocation.Y; } }

        private async System.Threading.Tasks.Task LoadPersonalAsync()
        {
            if (!int.TryParse(txtPid.Text.Trim(), out var pid) || pid <= 0)
            { MessageBox.Show(this, "Bitte gültige Personalnummer eingeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var db = new DatabaseHelperKassen())
            {
                var p = await db.GetPersonalInfoAsync(pid);
                if (p == null) { MessageBox.Show(this, "Personalnummer nicht gefunden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                _currentPid = p.PID; lblName.Text = $"Name: {p.Name}"; lblVorname.Text = $"Vorname: {p.Vorname}"; txtNfc.Text = p.NFC ?? string.Empty; txtFahrercode.Text = p.Fahrercode ?? string.Empty;
                gvOpenShifts.DataSource = await db.GetOpenShiftsListAsync(_currentPid);
                ApplyOpenShiftsGridFormatting();
                gvOpenPayments.DataSource = await db.GetOffeneAuszahlungenAsync(_currentPid);
                ApplyOpenPaymentsGridFormatting();
                await LoadGuthabenHistoryAsync();
                await UpdateSaldoLabelAsync(db);
                // Nach dem Laden Auswahl-Status aktualisieren
                GvOpenPayments_SelectionChanged(null, EventArgs.Empty);
            }
        }

        private async System.Threading.Tasks.Task UpdateSaldoLabelAsync(DatabaseHelperKassen db)
        {
            try
            {
                if (_currentPid <= 0) { lblSaldoAktuell.Text = "Saldo: 0,00 €"; return; }
                var saldo = await db.GetLastPersonalGuthabenSaldoAsync(_currentPid);
                lblSaldoAktuell.Text = $"Saldo: {saldo:0.00} €";
            }
            catch { try { lblSaldoAktuell.Text = "Saldo: –"; } catch { } }
        }

        private async System.Threading.Tasks.Task LoadGuthabenHistoryAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    System.Data.DataTable dt = null;
                    try
                    {
                        var t = db.GetType();
                        // Bevorzugt: async-Methode mit Task<DataTable>
                        var m1 = t.GetMethod("GetPersonalGuthabenVerlaufAsync");
                        if (m1 == null) m1 = t.GetMethod("GetGuthabenVerlaufAsync");
                        if (m1 != null)
                        {
                            var task = m1.Invoke(db, new object[] { _currentPid }) as System.Threading.Tasks.Task;
                            if (task != null)
                            {
                                await task.ConfigureAwait(true);
                                var prop = task.GetType().GetProperty("Result");
                                if (prop != null) dt = prop.GetValue(task, null) as System.Data.DataTable;
                            }
                        }
                        else
                        {
                            // Fallback: sync-Methoden zulassen, wenn vorhanden
                            var m2 = t.GetMethod("GetPersonalGuthabenVerlauf");
                            if (m2 == null) m2 = t.GetMethod("GetGuthabenVerlauf");
                            if (m2 != null)
                            {
                                dt = m2.Invoke(db, new object[] { _currentPid }) as System.Data.DataTable;
                            }
                        }
                    }
                    catch { dt = null; }

                    if (gvGuthabenHistory != null)
                    {
                        gvGuthabenHistory.DataSource = dt;
                        ApplyGuthabenGridFormatting();
                    }
                    await UpdateSaldoLabelAsync(db);
                }
            }
            catch { if (gvGuthabenHistory != null) gvGuthabenHistory.DataSource = null; }
        }

        private async System.Threading.Tasks.Task CreatePaymentWithPresetAsync()
        {
            if (_currentPid <= 0) { MessageBox.Show(this, "Bitte zuerst Mitarbeiter laden/auswählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var db = new DatabaseHelperKassen())
            {
                try
                {
                    string mwst = cboNewMwst.SelectedItem?.ToString();
                    decimal amount = nudNewAmount.Value;
                    decimal b19 = 0, b7 = 0, b0 = 0;
                    if (mwst == "19") b19 = amount; else if (mwst == "7") b7 = amount; else b0 = amount;

                    int? k1 = int.TryParse(txtNewK1.Text, out var vk1) ? (int?)vk1 : null;
                    int? k2 = int.TryParse(txtNewK2.Text, out var vk2) ? (int?)vk2 : null;
                    int? kto = int.TryParse(txtNewKonto.Text, out var vkto) ? (int?)vkto : null;
                    string text = txtNewPayText.Text?.Trim();
                    if (string.IsNullOrWhiteSpace(text)) { MessageBox.Show(this, "Bitte Buchungstext eingeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                    string typText = cboNewType.SelectedItem as string;
                    if (string.IsNullOrWhiteSpace(typText)) { MessageBox.Show(this, "Bitte Typ auswählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    string typCodeStr = typText.Equals("Einzahlung", StringComparison.OrdinalIgnoreCase) ? "2" :
                                        typText.Equals("Auszahlung", StringComparison.OrdinalIgnoreCase) ? "3" : typText;

                    int manId = 0; try { if (cboMandant?.SelectedValue != null) manId = Convert.ToInt32(cboMandant.SelectedValue); } catch { }

                    await db.InsertOffeneZahlungAsync(_currentPid, typCodeStr, text, b19, b7, b0, k1, k2, kto, manId);

                    gvOpenPayments.DataSource = await db.GetOffeneAuszahlungenAsync(_currentPid);
                    ApplyOpenPaymentsGridFormatting();
                    await LoadGuthabenHistoryAsync();
                    txtNewPayText.Clear(); nudNewAmount.Value = 0; cboNewMwst.SelectedIndex = 0; txtNewK1.Clear(); txtNewK2.Clear(); txtNewKonto.Clear();
                    MessageBox.Show(this, "Zahlung angelegt.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    GvOpenPayments_SelectionChanged(null, EventArgs.Empty);
                }
                catch (Exception ex) { MessageBox.Show(this, "Fehler beim Anlegen: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private async System.Threading.Tasks.Task EditSelectedPaymentAsync()
        {
            if (_currentPid <= 0) { MessageBox.Show(this, "Bitte zuerst Mitarbeiter laden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (gvOpenPayments.CurrentRow == null || gvOpenPayments.CurrentRow.DataBoundItem == null) { MessageBox.Show(this, "Bitte eine Zahlung auswählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var drv = gvOpenPayments.CurrentRow.DataBoundItem as DataRowView; if (drv == null) return; var row = drv.Row;
            if (!row.Table.Columns.Contains("Belegnummer")) { MessageBox.Show(this, "Belegnummer fehlt.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            int beleg = Convert.ToInt32(row["Belegnummer"]);

            string typText = cboNewType.SelectedItem as string ?? "Auszahlung";
            string typCodeStr = typText.Equals("Einzahlung", StringComparison.OrdinalIgnoreCase) ? "2" :
                                typText.Equals("Auszahlung", StringComparison.OrdinalIgnoreCase) ? "3" : typText;
            string txt = txtNewPayText.Text?.Trim();
            if (string.IsNullOrWhiteSpace(txt)) { MessageBox.Show(this, "Bitte Buchungstext eingeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string mwst = cboNewMwst.SelectedItem?.ToString();
            decimal amount = nudNewAmount.Value;
            decimal b19 = 0, b7 = 0, b0 = 0; if (mwst == "19") b19 = amount; else if (mwst == "7") b7 = amount; else b0 = amount;
            int? k1 = int.TryParse(txtNewK1.Text, out var vk1) ? (int?)vk1 : null;
            int? k2 = int.TryParse(txtNewK2.Text, out var vk2) ? (int?)vk2 : null;
            int? kto = int.TryParse(txtNewKonto.Text, out var vkto) ? (int?)vkto : null;

            using (var db = new DatabaseHelperKassen())
            {
                try
                {
                    int n = await db.UpdateOffeneZahlungAsync(beleg, typCodeStr, txt, b19, b7, b0, k1, k2, kto);
                    if (n <= 0) { MessageBox.Show(this, "Zahlung konnte nicht geändert werden (evtl. bereits verbucht).", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    gvOpenPayments.DataSource = await db.GetOffeneAuszahlungenAsync(_currentPid);
                    ApplyOpenPaymentsGridFormatting();
                    await LoadGuthabenHistoryAsync();
                    MessageBox.Show(this, "Zahlung geändert.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    GvOpenPayments_SelectionChanged(null, EventArgs.Empty);
                }
                catch (Exception ex) { MessageBox.Show(this, "Fehler beim Ändern: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private async System.Threading.Tasks.Task DeleteSelectedPaymentAsync()
        {
            if (_currentPid <= 0) { MessageBox.Show(this, "Bitte zuerst Mitarbeiter laden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (gvOpenPayments.CurrentRow == null || gvOpenPayments.CurrentRow.DataBoundItem == null) { MessageBox.Show(this, "Bitte eine Zahlung auswählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var drv = gvOpenPayments.CurrentRow.DataBoundItem as DataRowView; if (drv == null) return; var row = drv.Row;
            if (!row.Table.Columns.Contains("Belegnummer")) { MessageBox.Show(this, "Belegnummer fehlt.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            int beleg = Convert.ToInt32(row["Belegnummer"]);

            if (MessageBox.Show(this, $"Zahlung {beleg} wirklich löschen?", "Bestätigung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            using (var db = new DatabaseHelperKassen())
            {
                try
                {
                    int n = await db.DeleteOffeneZahlungAsync(beleg);
                    if (n <= 0) { MessageBox.Show(this, "Zahlung konnte nicht gelöscht werden (evtl. bereits verbucht).", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    gvOpenPayments.DataSource = await db.GetOffeneAuszahlungenAsync(_currentPid);
                    ApplyOpenPaymentsGridFormatting();
                    await LoadGuthabenHistoryAsync();
                    MessageBox.Show(this, "Zahlung gelöscht.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    GvOpenPayments_SelectionChanged(null, EventArgs.Empty);
                }
                catch (Exception ex) { MessageBox.Show(this, "Fehler beim Löschen: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private class ComboItem
        {
            public string Text { get; set; }
            public DataRow Row { get; set; }
            public override string ToString() => Text;
        }

        private void PersonalForm_KeyDown(object sender, KeyEventArgs e)
        { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return) { e.SuppressKeyPress = true; e.Handled = true; } }
        private void PersonalForm_KeyPress(object sender, KeyPressEventArgs e)
        { if (e.KeyChar == '\r' || e.KeyChar == '\n') { e.Handled = true; } }

        private void TryTakeLastNfc()
        {
            // Einfache Übernahme: NFC-Token aus Zwischenablage lesen (falls vorhanden)
            try
            {
                string token = Clipboard.ContainsText() ? (Clipboard.GetText() ?? string.Empty).Trim() : null;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    txtNfc.Text = token;
                }
                else
                {
                    MessageBox.Show(this, "Kein NFC-Wert verfügbar. Bitte manuell eingeben.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                try { MessageBox.Show(this, "NFC konnte nicht übernommen werden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
            }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (_currentPid <= 0)
            {
                MessageBox.Show(this, "Bitte zuerst Personal laden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var db = new DatabaseHelperKassen())
            {
                try
                {
                    await db.SetFahrercodeAsync(_currentPid, string.IsNullOrWhiteSpace(txtFahrercode.Text) ? (string)null : txtFahrercode.Text.Trim());
                    await db.SetNfcAsync(_currentPid, string.IsNullOrWhiteSpace(txtNfc.Text) ? (string)null : txtNfc.Text.Trim());
                    MessageBox.Show(this, "Gespeichert.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Fehler beim Speichern: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Neu: Auswahl-Handling und Übernahme der selektierten Zahlung in die Bearbeitungsfelder
        private void GvOpenPayments_SelectionChanged(object sender, EventArgs e)
        {
            bool hasSelection = gvOpenPayments != null && gvOpenPayments.CurrentRow != null && gvOpenPayments.CurrentRow.DataBoundItem != null;
            if (btnEditPayment != null) btnEditPayment.Enabled = hasSelection;
            if (btnDeletePayment != null) btnDeletePayment.Enabled = hasSelection;
        }

        private void LoadSelectedPaymentIntoFields()
        {
            if (gvOpenPayments == null || gvOpenPayments.CurrentRow == null) return;
            var drv = gvOpenPayments.CurrentRow.DataBoundItem as DataRowView; if (drv == null) return; var row = drv.Row;

            // Typ übernehmen
            if (row.Table.Columns.Contains("Typ"))
            {
                var typ = Convert.ToString(row["Typ"]);
                if (!string.IsNullOrWhiteSpace(typ))
                {
                    int idx = cboNewType.FindStringExact(typ);
                    if (idx >= 0) cboNewType.SelectedIndex = idx;
                }
            }

            // Text
            if (row.Table.Columns.Contains("Buchungstext"))
                txtNewPayText.Text = Convert.ToString(row["Buchungstext"]) ?? string.Empty;

            // Beträge/MwSt
            decimal b19 = 0m, b7 = 0m, b0 = 0m;
            try { if (row.Table.Columns.Contains("Betrag19")) b19 = row["Betrag19"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Betrag19"]); } catch { }
            try { if (row.Table.Columns.Contains("Betrag7")) b7 = row["Betrag7"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Betrag7"]); } catch { }
            try { if (row.Table.Columns.Contains("Betrag0")) b0 = row["Betrag0"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Betrag0"]); } catch { }

            string mwst = "19"; decimal amount = b19;
            if (b7 != 0m) { mwst = "7"; amount = b7; }
            else if (b0 != 0m) { mwst = "0"; amount = b0; }
            int mwstIdx = cboNewMwst.FindStringExact(mwst);
            if (mwstIdx >= 0) cboNewMwst.SelectedIndex = mwstIdx;
            try { nudNewAmount.Value = Math.Max(nudNewAmount.Minimum, Math.Min(nudNewAmount.Maximum, amount)); } catch { }

            // Kostenstellen/Konto
            txtNewK1.Text = row.Table.Columns.Contains("Kost1") && row["Kost1"] != DBNull.Value ? Convert.ToString(row["Kost1"]) : string.Empty;
            txtNewK2.Text = row.Table.Columns.Contains("Kost2") && row["Kost2"] != DBNull.Value ? Convert.ToString(row["Kost2"]) : string.Empty;
            txtNewKonto.Text = row.Table.Columns.Contains("Konto") && row["Konto"] != DBNull.Value ? Convert.ToString(row["Konto"]) : string.Empty;
        }

        // --- RE-ADDED Hilfsmethoden ---
        private void StyleGrid(DataGridView gv)
        {
            if (gv == null) return;
            gv.BorderStyle = BorderStyle.None;
            gv.EnableHeadersVisualStyles = false;
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(33, 150, 243);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            gv.RowHeadersVisible = false;
            gv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            gv.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
            gv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(227, 242, 253);
            gv.DefaultCellStyle.SelectionForeColor = Color.Black;
        }

        private static void TrySetHeader(DataGridView gv, string colName, string header)
        {
            if (gv == null) return; if (gv.Columns.Contains(colName)) gv.Columns[colName].HeaderText = header;
        }
        private static void TryHideColumn(DataGridView gv, string colName)
        { if (gv == null) return; if (gv.Columns.Contains(colName)) gv.Columns[colName].Visible = false; }
        private static void TryFormatAmount(DataGridView gv, string colName)
        { if (gv == null) return; if (gv.Columns.Contains(colName)) gv.Columns[colName].DefaultCellStyle.Format = "N2"; }

        private void ApplyGuthabenGridFormatting()
        {
            if (gvGuthabenHistory?.DataSource == null) return;
            var gv = gvGuthabenHistory;
            TrySetHeader(gv, "ErfasstAm", "Erfasst");
            TrySetHeader(gv, "Zeit", "Erfasst");
            TrySetHeader(gv, "Buchungstext", "Text");
            TrySetHeader(gv, "Text", "Text");
            TrySetHeader(gv, "Delta", "Änderung");
            TrySetHeader(gv, "Saldo", "Saldo");
            if (gv.Columns.Contains("Belegnummer")) gv.Columns["Belegnummer"].HeaderText = "B.-Nr";
            try { if (gv.Columns.Contains("Belegnummer")) gv.Columns["Belegnummer"].DisplayIndex = 0; } catch { }
            try { if (gv.Columns.Contains("ErfasstAm")) gv.Columns["ErfasstAm"].DisplayIndex = 1; else if (gv.Columns.Contains("Zeit")) gv.Columns["Zeit"].DisplayIndex = 1; } catch { }
            try { if (gv.Columns.Contains("Buchungstext")) gv.Columns["Buchungstext"].DisplayIndex = 2; else if (gv.Columns.Contains("Text")) gv.Columns["Text"].DisplayIndex = 2; } catch { }
            try { if (gv.Columns.Contains("Delta")) gv.Columns["Delta"].DisplayIndex = 3; } catch { }
            try { if (gv.Columns.Contains("Saldo")) gv.Columns["Saldo"].DisplayIndex = 4; } catch { }
            TryHideColumn(gv, "Typ"); TryHideColumn(gv, "Betrag19"); TryHideColumn(gv, "Betrag7"); TryHideColumn(gv, "Betrag0");
            if (gv.Columns.Contains("Belegnummer")) gv.Columns["Belegnummer"].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            var cErfasst = gv.Columns.Contains("ErfasstAm") ? gv.Columns["ErfasstAm"] : (gv.Columns.Contains("Zeit") ? gv.Columns["Zeit"] : null);
            if (cErfasst != null) cErfasst.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            var cText = gv.Columns.Contains("Buchungstext") ? gv.Columns["Buchungstext"] : (gv.Columns.Contains("Text") ? gv.Columns["Text"] : null);
            if (cText != null) cText.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            if (gv.Columns.Contains("Delta")) { var c = gv.Columns["Delta"]; c.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells; c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; TryFormatAmount(gv, "Delta"); }
            if (gv.Columns.Contains("Saldo")) { var c = gv.Columns["Saldo"]; c.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells; c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; TryFormatAmount(gv, "Saldo"); }
        }

        private void ApplyOpenShiftsGridFormatting()
        {
            if (gvOpenShifts?.DataSource == null) return;
            TryHideColumn(gvOpenShifts, "PersId"); TryHideColumn(gvOpenShifts, "PersName"); TryHideColumn(gvOpenShifts, "FhzId");
            TrySetHeader(gvOpenShifts, "Kennzeichen", "Kennzeichen");
            TrySetHeader(gvOpenShifts, "StartZeit", "Start");
            TrySetHeader(gvOpenShifts, "Belegnummer", "B.-Nr");
            TrySetHeader(gvOpenShifts, "Betrag19", "19%");
            TrySetHeader(gvOpenShifts, "Betrag7", "7%");
            TrySetHeader(gvOpenShifts, "Betrag0", "0%");
            TrySetHeader(gvOpenShifts, "OffenerBetrag", "Offen");
            TryFormatAmount(gvOpenShifts, "Betrag19"); TryFormatAmount(gvOpenShifts, "Betrag7"); TryFormatAmount(gvOpenShifts, "Betrag0"); TryFormatAmount(gvOpenShifts, "OffenerBetrag");
        }

        private void ApplyOpenPaymentsGridFormatting()
        {
            if (gvOpenPayments?.DataSource == null) return;
            TryHideColumn(gvOpenPayments, "PersId");
            TrySetHeader(gvOpenPayments, "Typ", "Typ");
            TrySetHeader(gvOpenPayments, "Buchungstext", "Text");
            TrySetHeader(gvOpenPayments, "Betrag19", "19%");
            TrySetHeader(gvOpenPayments, "Betrag7", "7%");
            TrySetHeader(gvOpenPayments, "Betrag0", "0%");
            TrySetHeader(gvOpenPayments, "ErfasstAm", "Erfasst");
            TrySetHeader(gvOpenPayments, "Belegnummer", "B.-Nr");
            TryFormatAmount(gvOpenPayments, "Betrag19"); TryFormatAmount(gvOpenPayments, "Betrag7"); TryFormatAmount(gvOpenPayments, "Betrag0");
        }

        private async System.Threading.Tasks.Task LoadActivePersonalAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.GetActivePersonalAsync();
                    _suppressEvents = true;
                    cboPerson.DisplayMember = "Name"; cboPerson.ValueMember = "PID"; cboPerson.DataSource = dt; cboPerson.SelectedIndex = -1;
                }
            }
            catch { }
            finally { _suppressEvents = false; }
        }

        private async System.Threading.Tasks.Task LoadAccountingPresetsAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.LoadZahlungsVorlagenAsync();
                    cboPreset.Items.Clear();
                    cboPreset.Items.Add(new ComboItem { Text = "Vorlage auswählen", Row = null });
                    foreach (DataRow r in dt.Rows)
                    {
                        string name = Convert.ToString(r["VorlagenName"]);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        cboPreset.Items.Add(new ComboItem { Text = name, Row = r });
                    }
                    if (cboPreset.Items.Count > 0) cboPreset.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void ApplyPresetToFields()
        {
            if (!(cboPreset.SelectedItem is ComboItem ci) || ci.Row == null) return;
            var row = ci.Row;
            string txt = row.Table.Columns.Contains("Buchungstext") ? Convert.ToString(row["Buchungstext"]) : null;
            string k1 = row.Table.Columns.Contains("Kost1") ? Convert.ToString(row["Kost1"]) : null;
            string k2 = row.Table.Columns.Contains("Kost2") ? Convert.ToString(row["Kost2"]) : null;
            string kto = row.Table.Columns.Contains("Konto") ? Convert.ToString(row["Konto"]) : null;
            string typ = row.Table.Columns.Contains("Typ") ? Convert.ToString(row["Typ"]) : null;
            if (!string.IsNullOrWhiteSpace(txt)) txtNewPayText.Text = txt;
            txtNewK1.Text = k1 ?? string.Empty; txtNewK2.Text = k2 ?? string.Empty; txtNewKonto.Text = kto ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(typ)) { int idx = cboNewType.FindStringExact(typ); if (idx >= 0) cboNewType.SelectedIndex = idx; }
            try
            {
                decimal m19 = 0, m7 = 0, m0 = 0;
                if (row.Table.Columns.Contains("Betrag19") && row["Betrag19"] != DBNull.Value) m19 = Convert.ToDecimal(row["Betrag19"]);
                if (row.Table.Columns.Contains("Betrag7") && row["Betrag7"] != DBNull.Value) m7 = Convert.ToDecimal(row["Betrag7"]);
                if (row.Table.Columns.Contains("Betrag0") && row["Betrag0"] != DBNull.Value) m0 = Convert.ToDecimal(row["Betrag0"]);
                string target = null;
                if (m19 == 1m && m7 == 0m && m0 == 0m) target = "19"; else if (m7 == 1m && m19 == 0m && m0 == 0m) target = "7"; else if (m0 == 1m && m19 == 0m && m7 == 0m) target = "0";
                if (target != null) { int mwIdx = cboNewMwst.FindStringExact(target); if (mwIdx >= 0) cboNewMwst.SelectedIndex = mwIdx; }
            }
            catch { }
        }
        // === Ende Hilfsmethoden ===
    }
}
