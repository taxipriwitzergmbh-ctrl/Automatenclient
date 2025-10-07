using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace TaMi_Kassenclient
{
    public class KassenübersichtForm : Form
    {
        private Panel headerPanel;
        private Label lblTitle;
        private Button btnClose;
        private Point _mouseDownLocation;
        private FlowLayoutPanel pnlKassen;

        public KassenübersichtForm()
        {
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 680);
            BackColor = Color.White;
            DoubleBuffered = true;

            headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            headerPanel.Paint += HeaderPanel_Paint;
            headerPanel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _mouseDownLocation = e.Location; };
            headerPanel.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Left += e.X - _mouseDownLocation.X;
                    Top += e.Y - _mouseDownLocation.Y;
                }
            };
            Controls.Add(headerPanel);

            lblTitle = new Label
            {
                Text = "TaMi-Kassenclient – Kassenübersicht",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Variable", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 0),
                Size = new Size(720, 60),
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(lblTitle);

            btnClose = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI Symbol", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(48, 48),
                Location = new Point(ClientSize.Width - 56, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 80, 80);
            btnClose.Click += (s, e) => Close();
            headerPanel.Controls.Add(btnClose);

            try { Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 24, 24)); } catch { }

            pnlKassen = new FlowLayoutPanel
            {
                Location = new Point(20, 80),
                Size = new Size(960, 580),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };
            Controls.Add(pnlKassen);

            Shown += async (s, e) => await LoadKassenAsync();
        }

        private async Task LoadKassenAsync()
        {
            pnlKassen.Controls.Clear();

            if (AppSettings.AutomatenNamen.Count == 0)
                AppSettings.LoadAutomatenNamenFromIni();

            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.GetKassenListeAsync(AppSettings.AutomatenNamen);
                    if (dt.Rows.Count == 0)
                    {
                        var lbl = new Label
                        {
                            Text = "Keine Kassen gefunden.",
                            AutoSize = true,
                            ForeColor = Color.DimGray,
                            Font = new Font("Segoe UI Variable", 14F, FontStyle.Regular),
                            Margin = new Padding(8)
                        };
                        pnlKassen.Controls.Add(lbl);
                        return;
                    }

                    // Gruppierung nach AutomatenName, "Personalguthaben" immer als letzte Gruppe
                    var gruppen = dt.AsEnumerable()
                        .GroupBy(r => r.Field<string>("AutomatenName"))
                        .OrderBy(g => string.Equals(g.Key, "Personalguthaben", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                        .ThenBy(g => g.Key);

                    foreach (var gruppe in gruppen)
                    {
                        // Überschrift für die Gruppe
                        var lblGroup = new Label
                        {
                            Text = gruppe.Key,
                            AutoSize = false,
                            Width = pnlKassen.Width - 40,
                            Height = 32,
                            Font = new Font("Segoe UI Variable", 15F, FontStyle.Bold),
                            ForeColor = Color.FromArgb(33, 150, 243),
                            Padding = new Padding(8, 8, 0, 0),
                            Margin = new Padding(8, 16, 8, 4)
                        };
                        pnlKassen.Controls.Add(lblGroup);

                        // Sortierung: erst FirmenId >= 0, dann FirmenId < 0 (jeweils aufsteigend)
                        var sortierteButtons = gruppe
                            .OrderBy(r => Convert.ToInt32(r["FirmenId"]) < 0 ? 1 : 0)
                            .ThenBy(r => Convert.ToInt32(r["FirmenId"]))
                            .ToList();

                        foreach (var row in sortierteButtons)
                        {
                            int fid = Convert.ToInt32(row["FirmenId"]);
                            string auto = row["AutomatenName"] as string ?? "";
                            string manName = row["ManName"] as string ?? ("ID " + fid);
                            decimal kb = row["Kassenbestand"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Kassenbestand"]);

                            var btn = new Button
                            {
                                Width = 300,
                                Height = 90,
                                Margin = new Padding(8),
                                FlatStyle = FlatStyle.Flat,
                                BackColor = Color.FromArgb(33, 150, 243),
                                ForeColor = Color.White,
                                Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold),
                                TextAlign = ContentAlignment.MiddleLeft,
                                Tag = new { FirmenId = fid, AutomatenName = auto, KassenName = manName }
                            };
                            btn.FlatAppearance.BorderSize = 0;

                            btn.Text = $"{manName} – {auto}\r\nBestand: {kb:C2}";

                            btn.Click += (s, e) =>
                            {
                                dynamic t = btn.Tag;
                                int _fid = (int)t.FirmenId;
                                string _auto = (string)t.AutomatenName;
                                string _name = (string)t.KassenName;
                                using (var frm = new DayViewForm(_fid, _name, _auto))
                                {
                                    frm.ShowDialog(this);
                                }
                            };

                            pnlKassen.Controls.Add(btn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Laden der Kassen:\r\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
    }
}