using SuE.TaMi;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

namespace TaMi_Automatenclient
{
    public partial class MenueForm : KassenclientBaseForm
    {
        private TaMiClient mTaMiClient;

        private Button btnKassenbuch;
        private Button btnRegeln;
        private Button btnPersonal;
        private Button btnZahlungen;
        private Button btnOffene;
        private Button btnUpdateHints; // NEW
        private Label lblBuildInfo;

        private static readonly Color Accent = Color.FromArgb(33, 150, 243);
        private static readonly Color AccentHover = Color.FromArgb(25, 118, 210);

        public MenueForm()
        {
            mTaMiClient = Program.MainTaMiClient;
            BuildUI();
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);

            if (mTaMiClient.State != SuE.Tools.ConnectionState.CONNECTED || mTaMiClient.UserId == -1)
            {
                if (LoginForm.ShowLogin(this) == null)
                    this.Close();
            }

            try { ReleaseNotes.CheckAndShowAtStartup(this); } catch { }
        }

        private void BuildUI()
        {
            this.SetupDefaultForm("MenueForm", "Automaten-Client", new Size(500, 580));
            ShowInTaskbar = true;

            this.btnKassenbuch = new Button();
            this.btnRegeln = new Button();
            this.btnPersonal = new Button();
            this.btnZahlungen = new Button();
            this.btnOffene = new Button();
            this.btnUpdateHints = new Button();
            this.lblBuildInfo = new Label();

            this.SuspendLayout();

            int w = 320; 
            int h = 56; 
            int x = (500 - w) / 2; 
            int y = 96; 
            int padY = 16;

            StylePrimaryButton(this.btnKassenbuch, "Einzahlungen", new Point(x, y), new Size(w, h));
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

            StylePrimaryButton(this.btnOffene, "Offene Übersicht", new Point(x, y + (h + padY) * 4), new Size(w, h));
            this.btnOffene.TabIndex = 4;
            this.btnOffene.Click += (s, e) => { using (var f = new OffeneUebersichtForm()) f.ShowDialog(this); };

            StylePrimaryButton(this.btnUpdateHints, "Update", new Point(x, y + (h + padY) * 5), new Size(w, h));
            this.btnUpdateHints.TabIndex = 5;
            this.btnUpdateHints.BackColor = Color.FromArgb(0, 122, 204);
            this.btnUpdateHints.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 102, 184);
            this.btnUpdateHints.Click += (s, e) =>
            {
                try { Program.CheckForUpdateNow(this); } catch { }
                try { Program.ShowUpdateHints(this); } catch { }
            };

            this.AddHeaderPanel(this.Text, true, true, true);

            this.Controls.Add(this.btnKassenbuch);
            this.Controls.Add(this.btnRegeln);
            this.Controls.Add(this.btnPersonal);
            this.Controls.Add(this.btnZahlungen);
            this.Controls.Add(this.btnOffene);
            this.Controls.Add(this.btnUpdateHints);

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
                lblBuildInfo.Anchor = AnchorStyles.Bottom;
                this.Controls.Add(lblBuildInfo);
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

            b.Resize += (s, e) =>
            {
                try
                {
                    using (var path = new System.Drawing.Drawing2D.GraphicsPath())
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
            kassenuebersichtForm.ShowDialog(this);
        }
    }
}
