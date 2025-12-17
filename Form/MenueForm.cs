using SuE.TaMi;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

namespace TaMi_Kassenclient
{
    public partial class MenueForm : KassenclientBaseForm
    {
        private TaMiClient mTaMiClient;

        //private Panel headerPanel;
        //private Label lblTitle;
        //private Button btnClose;
        private Button btnKassenbuch;
        private Button btnRegeln; // neu
        private Button btnPersonal; // neu
        private Button btnZahlungen; // neu
        private Button btnOffene; // neu
        //private Point _mouseDownLocation;
        private Label lblBuildInfo; // neu

        // Farben analog der anderen Forms
        private static readonly Color Accent = Color.FromArgb(33, 150, 243);
        private static readonly Color AccentHover = Color.FromArgb(25, 118, 210);

        // Composited zur Reduktion von Flackern
        /*
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
        */

        public MenueForm()
        {
            mTaMiClient = Program.MainTaMiClient;
            BuildUI();
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);

            //Nicht verbunden oder angemeldet
            if (mTaMiClient.State != SuE.Tools.ConnectionState.CONNECTED || mTaMiClient.UserId == -1)
            {
                if (LoginForm.ShowLogin(this) == null)
                    this.Close();
            }

        }


        private void BuildUI()
        {
            this.SetupDefaultForm("MenueForm", "Kassenclient", new Size(500, 500));
            ShowInTaskbar = true;

            //this.headerPanel = new Panel();
            //this.lblTitle = new Label();
            //this.btnClose = new Button();
            this.btnKassenbuch = new Button();
            this.btnRegeln = new Button();
            this.btnPersonal = new Button();
            this.btnZahlungen = new Button();
            this.btnOffene = new Button();
            this.lblBuildInfo = new Label();

            this.SuspendLayout();
            
            /*
            // 
            // headerPanel
            // 
            this.headerPanel.Location = new Point(0, 0);
            this.headerPanel.Size = new Size(500, 60);
            this.headerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.headerPanel.Paint += HeaderPanel_Paint;
            this.headerPanel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _mouseDownLocation = e.Location; };
            this.headerPanel.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Left += e.X - _mouseDownLocation.X;
                    Top += e.Y - _mouseDownLocation.Y;
                }
            };

            // 
            // lblTitle
            // 
            this.lblTitle.Text = "TaMi-Kassenclient";
            this.lblTitle.Font = new Font("Segoe UI Variable", 18F, FontStyle.Bold);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            this.lblTitle.Location = new Point(24, 0);
            this.lblTitle.Size = new Size(360, 60);
            this.lblTitle.BackColor = Color.Transparent;
            this.headerPanel.Controls.Add(this.lblTitle);

            // 
            // btnClose
            // 
            this.btnClose.Text = "\u2715";
            this.btnClose.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold);
            this.btnClose.ForeColor = Color.White;
            this.btnClose.BackColor = Color.Transparent;
            this.btnClose.FlatStyle = FlatStyle.Flat;
            this.btnClose.Size = new Size(48, 48);
            this.btnClose.Location = new Point(500 - 56, 6);
            this.btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnClose.TabStop = false;
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80);
            this.btnClose.Click += (s, e) => Close();
            this.headerPanel.Controls.Add(this.btnClose);
            */

            // 
            // Buttons – einheitlicher Stil
            // 
            int w = 320; 
            int h = 56; 
            int x = (500 - w) / 2; 
            int y = 96; 
            int padY = 16;

            StylePrimaryButton(this.btnKassenbuch, "Kassenbuch", new Point(x, y), new Size(w, h));
            this.btnKassenbuch.TabIndex = 0;
            this.btnKassenbuch.Click += new EventHandler(this.btnKassenbuch_Click);

            StylePrimaryButton(this.btnRegeln, "Abrechnungsbedingungen", new Point(x, y + h + padY), new Size(w, h));
            this.btnRegeln.TabIndex = 1;
            this.btnRegeln.Click += (s, e) => { using (var f = new AbrechnungBedingungenForm()) f.ShowDialog(this); };

            StylePrimaryButton(this.btnPersonal, "Personal", new Point(x, y + (h + padY) * 2), new Size(w, h));
            this.btnPersonal.TabIndex = 2;
            this.btnPersonal.Click += (s, e) => { using (var f = new PersonalForm()) f.ShowDialog(this); };

            StylePrimaryButton(this.btnZahlungen, "Zahlungen", new Point(x, y + (h + padY) * 3), new Size(w, h));
            this.btnZahlungen.TabIndex = 3;
            this.btnZahlungen.Click += (s, e) => { using (var f = new ZahlungForm()) f.ShowDialog(this); };

            // Neuer Button: Offene Übersicht
            StylePrimaryButton(this.btnOffene, "Offene Übersicht", new Point(x, y + (h + padY) * 4), new Size(w, h));
            this.btnOffene.TabIndex = 4;
            this.btnOffene.Click += (s, e) => { using (var f = new OffeneUebersichtForm()) f.ShowDialog(this); };

            // 
            // MenueForm
            // 
            /*
            this.Name = "MenueForm";
            this.Text = "Kassenclient";
            this.FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(500, 500); // erhöht damit alle Buttons sichtbar sind
            this.BackColor = Color.WhiteSmoke;
            
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            this.DoubleBuffered = true;
            */

            //try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 24, 24)); } catch { }

            this.AddHeaderPanel(this.Text, true, true, true);

            //this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.btnKassenbuch);
            this.Controls.Add(this.btnRegeln);
            this.Controls.Add(this.btnPersonal);
            this.Controls.Add(this.btnZahlungen);
            this.Controls.Add(this.btnOffene);

            // Build-Info Label unten dezent
            try
            {
                string version = Application.ProductVersion;
                string asmPath = Assembly.GetExecutingAssembly().Location;
                DateTime buildDt;
                try { buildDt = File.GetLastWriteTime(asmPath); }
                catch { buildDt = DateTime.Now; }
                string info = $"Version {version} • Build {buildDt:dd.MM.yyyy HH:mm}";

                lblBuildInfo.AutoSize = true;
                lblBuildInfo.Text = info;
                lblBuildInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                lblBuildInfo.ForeColor = Color.FromArgb(120, 120, 120);
                lblBuildInfo.BackColor = Color.Transparent;
                // Unten mittig zentrieren
                lblBuildInfo.Anchor = AnchorStyles.Bottom; // nur unten verankern, horizontal zentrieren per Resize
                this.Controls.Add(lblBuildInfo);
                // Initial positionieren und bei Resize nachziehen
                this.Resize += (s, e) => PositionBuildInfo();
                PositionBuildInfo();
            }
            catch { }

            this.ResumeLayout(false);
        }

        private void PositionBuildInfo()
        {
            try
            {
                if (lblBuildInfo == null) return;
                int x = (this.ClientSize.Width - lblBuildInfo.Width) / 2;
                int y = this.ClientSize.Height - lblBuildInfo.Height - 8;
                if (x < 8) x = 8;
                if (y < 0) y = 0;
                lblBuildInfo.Location = new Point(x, y);
            }
            catch { }
        }

        private void StylePrimaryButton(Button b, string text, Point location, Size size)
        {
            b.Text = text;
            b.Font = new Font("Segoe UI Variable", 14F, FontStyle.Bold);
            b.Size = size;
            b.Location = location;
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.TextAlign = ContentAlignment.MiddleCenter;

            // Runde Ecken
            b.Resize += (s, e) =>
            {
                try
                {
                    using (var path = new GraphicsPath())
                    {
                        int r = 12; var rect = new Rectangle(0, 0, b.Width, b.Height);
                        path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                        path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                        path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                        path.CloseFigure();
                        b.Region = new Region(path);
                    }
                }
                catch { }
            };

            b.PerformLayout();
        }

        private void btnKassenbuch_Click(object sender, EventArgs e)
        {
            var kassenuebersichtForm = new KassenübersichtForm();
            kassenuebersichtForm.ShowDialog();
        }

        /*
        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(headerPanel.ClientRectangle,
                Color.FromArgb(33, 150, 243), Color.FromArgb(33, 203, 243), 0f))
            {
                e.Graphics.FillRectangle(brush, headerPanel.ClientRectangle);
            }
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        */
    }
}
