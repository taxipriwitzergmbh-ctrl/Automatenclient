using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SuE.TaMi;

namespace TaMi_Kassenclient
{

    // Einfache Session-Verwaltung für das Kassen-Client-Projekt
    public static class AppSession
    {
        public static PersonalInfo CurrentUser { get; internal set; }
    }



    // Leichtgewichtiges Anmeldefenster für TaMi-Kassenclient (ohne Geräte-Anbindung)
    public class LoginForm : KassenclientBaseForm
    {
        private int         _pendingPid = 0;
        private string      _expectedCode = null;
        private bool        _createMode = false; // wenn kein Fahrercode existiert, doppelte Eingabe aktivieren

        //private Panel header;
        //private Label lblTitle;
        //private Button btnClose;
        private Label lblPid;
        private Label lblPwd;
        private TextBox txtPid;
        private TextBox txtPwd;
        private Label lblPwd2;
        private TextBox txtPwd2;
        private Label lblInfo;
        private Button btnLogin;
        private Button btnExit;
        private Label lblError;

        //private static readonly Color Accent = Color.FromArgb(33, 150, 243);
        //private static readonly Color Accent2 = Color.FromArgb(33, 203, 243);

        private TaMiClient mTaMiClient;
        //private System.Windows.Forms.Timer _reconnectTimer;
        //private const int ReconnectIntervalMs = 5000;

        public LoginForm()
        {
            BuildUI();

#if DEBUG
            txtPid.Text = "902";
            txtPwd.Text = "1";
#endif
        }


        public static PersonalInfo ShowLogin(IWin32Window owner = null)
        {
            //Abmelden
            AppSession.CurrentUser = null;

            using (var dlg = new LoginForm())
            {
                var res = owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
                return (res == DialogResult.OK ? AppSession.CurrentUser : null);
            }
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);

            try { txtPid.Focus(); txtPid.SelectAll(); } catch { }

            mTaMiClient = Program.MainTaMiClient;
            btnLogin.Enabled = (mTaMiClient.State == SuE.Tools.ConnectionState.CONNECTED);


            mTaMiClient.ConnectionStateChange += (s, e) =>
            {
                btnLogin.Enabled = (e.StateNew == SuE.Tools.ConnectionState.CONNECTED);

                if (!btnLogin.Enabled)
                {
                    lblError.Text = "Keine Verbindung zum TaMi-Server!";
                }
                else
                {
                    lblError.Text = string.Empty;
                }
            };

            mTaMiClient.LoginMessage += (s, e) =>
            {
                // Login-Antworten hier nicht benötigt
            };

            

        }

        private void BuildUI()
        {
            //DoubleBuffered = true;
            this.SetupDefaultForm("LoginForm", "Kassenclient - Anmeldung", new Size(480, 320));

            /*
            Name = "LoginForm";
            Text = "Kassenclient - Anmeldung";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 330);
            BackColor = Color.WhiteSmoke;
            */

            this.AddHeaderPanel(Text, false, false, false);

            /*
            header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.Transparent };
            header.Paint += (s, e) =>
            {
                using (var brush = new LinearGradientBrush(header.ClientRectangle, Accent, Accent2, 0f))
                    e.Graphics.FillRectangle(brush, header.ClientRectangle);
            };
            Controls.Add(header);

            lblTitle = new Label { Text = "TaMi-Kassenclient – Anmeldung", AutoSize = false, Left = 16, Top = 0, Width = 420, Height = 64, ForeColor = Color.White, Font = new Font("Segoe UI Variable", 16F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            header.Controls.Add(lblTitle);

            btnClose = new Button 
            {
                Text = "\u2715",
                Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold),
                Width = 48,
                Height = 48, 
                Left = ClientSize.Width - 56, 
                Top = 8, 
                Anchor = AnchorStyles.Top | AnchorStyles.Right, 
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = {
                    BorderSize = 0,
                    MouseOverBackColor = Color.FromArgb(255, 80, 80),
                    },

                ForeColor = Color.White, 
                BackColor = Color.Transparent, 
                TabStop = false 
            };

            //try { btnClose.FlatAppearance.BorderSize = 0; btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80); } catch { }
            btnClose.Click += (s, e) => Close();
            header.Controls.Add(btnClose);

            */

            int leftLbl = 40, leftBox = 200, row1 = 96, rowH = 40, gap = 10;

            lblPid = new Label { Text = "Personalnummer:", Left = leftLbl, Top = row1, Width = 150, Height = 28, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold) };
            Controls.Add(lblPid);

            txtPid = new TextBox { Left = leftBox, Top = row1 - 4, Width = 260, Height = 36, Font = new Font("Segoe UI Variable", 14F), TextAlign = HorizontalAlignment.Left, MaxLength = 8 };
            txtPid.GotFocus += (s, e) => txtPid.SelectAll();
            Controls.Add(txtPid);

            lblPwd = new Label { Text = "Passwort:", Left = leftLbl, Top = row1 + rowH + gap, Width = 150, Height = 28, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold) };
            Controls.Add(lblPwd);

            txtPwd = new TextBox { Left = leftBox, Top = row1 + rowH + gap - 4, Width = 260, Height = 36, Font = new Font("Segoe UI Variable", 14F), UseSystemPasswordChar = true, MaxLength = 8 };
            Controls.Add(txtPwd);

            lblPwd2 = new Label { Text = "Passwort (Wiederholung):", Left = leftLbl, Top = row1 + 2 * (rowH + gap), Width = 230, Height = 28, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold), Visible = false };
            Controls.Add(lblPwd2);

            txtPwd2 = new TextBox { Left = leftBox, Top = row1 + 2 * (rowH + gap) - 4, Width = 260, Height = 36, Font = new Font("Segoe UI Variable", 14F), UseSystemPasswordChar = true, MaxLength = 8, Visible = false };
            Controls.Add(txtPwd2);

            lblInfo = new Label { Left = leftLbl, Top = row1 + 3 * (rowH + gap), Width = ClientSize.Width - 2 * leftLbl, Height = 26, ForeColor = Color.DimGray, Font = new Font("Segoe UI Variable", 10F, FontStyle.Italic), TextAlign = ContentAlignment.MiddleLeft, Visible = false };
            Controls.Add(lblInfo);

            lblError = new Label { Left = leftLbl, Top = ClientSize.Height - 100, Width = ClientSize.Width - 2 * leftLbl, Height = 22, ForeColor = Color.FromArgb(229, 57, 53), Font = new Font("Segoe UI Variable", 10F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            Controls.Add(lblError);

            btnExit = new Button { Text = "Beenden", Left = leftLbl, Top = ClientSize.Height - 68, Width = 160, Height = 40, BackColor = Color.FromArgb(158, 158, 158), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold) };
            try { btnExit.FlatAppearance.BorderSize = 0; } catch { }
            btnExit.Click += (s, e) => Close();
            Controls.Add(btnExit);

            btnLogin = new Button { Text = "Anmelden  F10", Left = ClientSize.Width - 200, Top = ClientSize.Height - 68, Width = 200, Height = 40, BackColor = KassenclientBaseForm.BackcolorNeutral, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold) };
            try { btnLogin.FlatAppearance.BorderSize = 0; } catch { }
            btnLogin.Click += async (s, e) => await OnLoginButtonClick();
            Controls.Add(btnLogin);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) 
                { 
                    e.SuppressKeyPress = true;
                    Close(); 
                }

                else if (e.KeyCode == Keys.Enter)
                {
                    if (txtPid.Equals(ActiveControl)) 
                    {
                        e.SuppressKeyPress = true; 
                        txtPwd.Focus(); 
                        txtPwd.SelectAll(); 
                    }
                    else if (txtPwd.Equals(ActiveControl)) 
                    { 
                        e.SuppressKeyPress = true; 
                        btnLogin.Focus();
                    }
                }

                else if (e.KeyCode == Keys.F10) 
                { 
                    e.SuppressKeyPress = true; 
                    btnLogin.PerformClick(); 
                }
            };
        }

        private async Task OnLoginButtonClick()
        {
            lblError.Text = string.Empty;
            var pidText = (txtPid.Text ?? string.Empty).Trim();
            int personalId;

            var pwd = (txtPwd.Text ?? string.Empty).Trim();
            var pwd2 = (txtPwd2.Text ?? string.Empty).Trim();

            // Änderung: 0 ist jetzt erlaubt (nur negative IDs werden abgelehnt)
            if (!int.TryParse(txtPid.Text, out personalId) || personalId < 0)
            {
                lblError.Text = "Bitte gültige Personalnummer eingeben.";
                return;
            }

            //Login
            btnLogin.Enabled = false;
            ResultCodes loginResult = await mTaMiClient.LoginAsync(AppId.KASSENCLIENT, Program.mSessionId, NotifyFlags.NONE, personalId, pwd);
            btnLogin.Enabled = true;

            if (loginResult != ResultCodes.SUCCESS)
            {
                lblError.Text = TaMiTools.GetErrorText(loginResult);
                return;
            }

            //Formular schließen und OK zurückgeben
            AppSession.CurrentUser = new PersonalInfo() 
            { 
                PID = personalId, 
                Name = mTaMiClient.Username1 + " " + mTaMiClient.Username2
            };

            DialogResult = DialogResult.OK;
            Close();

            return;


            /*
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    if (!int.TryParse(pidText, out var pid) || pid <= 0)
                    { lblError.Text = "Bitte gültige Personalnummer eingeben."; return; }

                    var p = await db.GetPersonalInfoAsync(pid);
                    if (p == null)
                    { lblError.Text = "Personalnummer nicht gefunden."; return; }

                    _pendingPid = pid;
                    _expectedCode = p.Fahrercode;

                    if (string.IsNullOrWhiteSpace(_expectedCode))
                    {
                        // Kein Fahrercode gesetzt -> Anlegemodus aktivieren
                        if (!_createMode)
                        {
                            _createMode = true;
                            lblInfo.Text = "Kein Fahrercode gesetzt. Bitte neues Passwort zweimal eingeben (min. 4 Ziffern).";
                            lblInfo.Visible = true; lblPwd2.Visible = true; txtPwd2.Visible = true;
                            if (pwd.Length < 4) { lblError.Text = "Bitte neues Passwort (min. 4 Ziffern) eingeben."; return; }
                            return; // Nutzer gibt zweite Eingabe ein und klickt erneut
                        }

                        // Zweiter Klick im Create-Mode: prüfen und setzen
                        if (pwd.Length < 4)
                        { lblError.Text = "Bitte neues Passwort (min. 4 Ziffern) eingeben."; return; }
                        if (!string.Equals(pwd, pwd2))
                        { lblError.Text = "Passwörter stimmen nicht überein."; return; }
                        await db.SetFahrercodeAsync(_pendingPid, pwd);
                        _expectedCode = pwd; _createMode = false;
                        // Erfolgreich gesetzt -> weiter einloggen
                    }

                    // Normale Anmeldung
                    if (!string.Equals(pwd, _expectedCode))
                    { lblError.Text = "Passwort falsch."; txtPwd.SelectAll(); return; }
                    await FinishLoginAsync(db, _pendingPid);
                }
            }
            catch (Exception ex)
            {
                lblError.Text = "Fehler: " + ex.Message;
            }
            */
        }

        /*
        private async Task FinishLoginAsync(DatabaseHelperKassen db, int pid)
        {
            var p = await db.GetPersonalInfoAsync(pid);
            AppSession.CurrentUser = p;
            DialogResult = DialogResult.OK;
            Close();
        }
        */
    }
}
