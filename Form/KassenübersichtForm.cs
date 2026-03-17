using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using TaMi_Automatenclient.UI.Layout;

namespace TaMi_Automatenclient
{
    public class KassenübersichtForm : KassenclientBaseForm
    {
        private FlowLayoutPanel pnlKassen;

        public KassenübersichtForm()
        {
            this.Icon = Program.AppIcon;
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            SetupDefaultForm("KassenuebersichtForm", "TaMi Automatenclient – Einzahlübersicht", new Size(1180, 720));
            ShowInTaskbar = false;
            AddHeaderPanel(Text, true, true, true);

            pnlKassen = new FlowLayoutPanel
            {
                Location = new Point(20, UiTheme.HeaderHeight + 20),
                Size = new Size(1120, 600),
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
                AppSettings.LoadAutomatenNamenFromIni(); // (jetzt DeviceIDs als String)

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

                    // Gruppierung nach DeviceID
                    var gruppen = dt.AsEnumerable()
                        .GroupBy(r => r.Field<byte>("DeviceID"))
                        .OrderBy(g => g.Key)
                        .ToList();

                    foreach (var gruppe in gruppen)
                    {
                        string deviceName = gruppe.Select(r => r.Field<string>("AutomatenName")).FirstOrDefault();
                        if (string.IsNullOrWhiteSpace(deviceName)) deviceName = "Gerät " + gruppe.Key;

                        var lblGroup = new Label
                        {
                            Text = deviceName + " (ID " + gruppe.Key + ")",
                            AutoSize = false,
                            Width = pnlKassen.Width - 40,
                            Height = 32,
                            Font = new Font("Segoe UI Variable", 15F, FontStyle.Bold),
                            ForeColor = Color.FromArgb(33, 150, 243),
                            Padding = new Padding(8, 8, 0, 0),
                            Margin = new Padding(8, 16, 8, 4)
                        };
                        pnlKassen.Controls.Add(lblGroup);

                        var sortierteButtons = gruppe
                            .OrderBy(r => Convert.ToInt32(r["FirmenId"]) < 0 ? 1 : 0) // Personalguthaben zuletzt
                            .ThenBy(r => Convert.ToInt32(r["FirmenId"]))
                            .ToList();

                        foreach (var row in sortierteButtons)
                        {
                            int fid = row["FirmenId"] == DBNull.Value ? 0 : Convert.ToInt32(row["FirmenId"]);
                            byte deviceId = row.Field<byte>("DeviceID");
                            string manName = row.Field<string>("ManName") ?? ("ID " + fid);
                            decimal kb = row["Kassenbestand"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Kassenbestand"]);

                            var btn = new ModernGradientButton
                            {
                                Width = 300,
                                Height = 90,
                                Margin = new Padding(8),
                                Font = new Font("Segoe UI Variable", 12F, FontStyle.Bold),
                                GradientStart = UiTheme.PrimaryStart,
                                GradientEnd = UiTheme.PrimaryEnd,
                                TextAlign = ContentAlignment.MiddleLeft,
                                Tag = new { FirmenId = fid, DeviceID = deviceId, KassenName = manName }
                            };
                            btn.Text = $"{manName} – {deviceName}\r\nBestand: {kb:C2}";

                            btn.Click += (s, e) =>
                            {
                                dynamic t = btn.Tag;
                                int _fid = (int)t.FirmenId;
                                byte _dev = (byte)t.DeviceID;
                                string _name = (string)t.KassenName;
                                using (var frm = new DayViewForm(_fid, _name, _dev.ToString()))
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
                MessageBox.Show(this, $"Fehler beim Laden der Kassen:\r\n{ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        
    }
}