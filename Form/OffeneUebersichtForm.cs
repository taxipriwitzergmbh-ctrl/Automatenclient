using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using SuE.Tools;
using System.IO;
using TaMi_Automatenclient.UI.Layout;

namespace TaMi_Automatenclient
{
    public class OffeneUebersichtForm : KassenclientBaseForm
    {
        private TabControl tabs;
        private DataGridView gvSchichten;
        private DataGridView gvZahlungen;
        private DataGridView gvGuthaben;
        private readonly string _gridCfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SuE-Software", "SuE-TaMi Client SQL", "Automaten-Client.GridLayout.xml");
        private const string _gridCfgApp = "TaMi Automaten-Client";

        public OffeneUebersichtForm()
        {
            SetupDefaultForm("OffeneUebersichtForm", "Übersicht offen", new Size(900, 640));
            AddHeaderPanel(this.Text, true, true, true);
            BuildUI();
        }

        private void BuildUI()
        {
            tabs = new TabControl
            {
                Left = 12,
                Top = UiTheme.HeaderHeight + 12,
                Width = this.ClientSize.Width - 24,
                Height = this.ClientSize.Height - (UiTheme.HeaderHeight + 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var t1 = new TabPage("Offene Schichten");
            var t2 = new TabPage("Offene Zahlungen");
            var t3 = new TabPage("Personalguthaben");

            gvSchichten = MakeGrid();
            gvZahlungen = MakeGrid();
            gvZahlungen.CellFormatting += GvZahlungen_CellFormatting;
            gvGuthaben = MakeGrid();
            gvGuthaben.CellFormatting += GvGuthaben_CellFormatting;

            t1.Controls.Add(gvSchichten);
            t2.Controls.Add(gvZahlungen);
            t3.Controls.Add(gvGuthaben);

            tabs.TabPages.Add(t1);
            tabs.TabPages.Add(t2);
            tabs.TabPages.Add(t3);

            this.Controls.Add(tabs);

            this.Shown += async (s, e) => await LoadAllAsync();
            tabs.SelectedIndexChanged += async (s, e) => await LoadCurrentAsync();
        }

        private DataGridView MakeGrid()
        {
            var gv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            gv.EnableHeadersVisualStyles = true;
            gv.RowHeadersVisible = false;
            gv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            gv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            gv.ColumnHeadersHeight = 30;
            gv.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 2, 0, 2);
            gv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            gv.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            gv.RowTemplate.Height = 22;
            gv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            gv.DefaultCellStyle.BackColor = Color.White;
            gv.DefaultCellStyle.ForeColor = Color.Black;
            gv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(33, 150, 243);
            gv.DefaultCellStyle.SelectionForeColor = Color.White;
            gv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(33, 150, 243);
            gv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            gv.GridColor = Color.FromArgb(220, 225, 230);

            return gv;
        }

        private async Task LoadAllAsync()
        {
            await LoadOpenShiftsAsync();
            await LoadOpenPaymentsAsync();
            await LoadGuthabenAsync();
        }

        private async Task LoadCurrentAsync()
        {
            switch (tabs.SelectedIndex)
            {
                case 0: await LoadOpenShiftsAsync(); break;
                case 1: await LoadOpenPaymentsAsync(); break;
                case 2: await LoadGuthabenAsync(); break;
            }
        }

        private async Task LoadOpenShiftsAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.GetAllOpenShiftsAsync();
                    gvSchichten.DataSource = dt;
                    ApplySchichtenFormatting();
                    LoadGridLayout("Schichten", gvSchichten);
                }
            }
            catch { gvSchichten.DataSource = null; }
        }

        private async Task LoadOpenPaymentsAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.GetAllOffeneAuszahlungenAsync();
                    var view = BuildPaymentsOverview(dt);
                    ConfigurePaymentsGrid();
                    gvZahlungen.DataSource = view;
                    LoadGridLayout("Zahlungen", gvZahlungen);
                    // Rechtsbündige Ausrichtung für Betrag/MwSt (nach Layout laden setzen)
                    try
                    {
                        if (gvZahlungen.Columns.Contains("Betrag"))
                            gvZahlungen.Columns["Betrag"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        if (gvZahlungen.Columns.Contains("MwSt"))
                            gvZahlungen.Columns["MwSt"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    catch { }
                }
            }
            catch { gvZahlungen.DataSource = null; }
        }

        private async Task LoadGuthabenAsync()
        {
            try
            {
                using (var db = new DatabaseHelperKassen())
                {
                    var dt = await db.GetAllPersonalGuthabenSaldenAsync();
                    gvGuthaben.DataSource = dt;
                    LoadGridLayout("Guthaben", gvGuthaben);
                    // Rechtsbündige Ausrichtung für Beträge (nach Layout laden setzen)
                    try
                    {
                        if (gvGuthaben.Columns.Contains("Saldo"))
                            gvGuthaben.Columns["Saldo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        if (gvGuthaben.Columns.Contains("Betrag"))
                            gvGuthaben.Columns["Betrag"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    catch { }
                }
            }
            catch { gvGuthaben.DataSource = null; }
        }

        // --- Offene Zahlungen: Mapping & Grid ---
        private static string MapTypCodeToText(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;
            switch (code.Trim())
            {
                case "1": return "Anfangsbestand";
                case "2": return "Einzahlung";
                case "3": return "Auszahlung";
                case "4": return "Schichtabrechnung";
                case "5": return "Personalguthaben";
                case "6": return "Trinkgeld";
                default: return code;
            }
        }

        private DataTable BuildPaymentsOverview(DataTable raw)
        {
            var view = new DataTable();
            view.Columns.Add("Belegnummer", typeof(int));
            view.Columns.Add("PersName", typeof(string));
            view.Columns.Add("Typ", typeof(string));
            view.Columns.Add("Betrag", typeof(decimal));
            view.Columns.Add("MwSt", typeof(string));
            view.Columns.Add("Buchungstext", typeof(string));
            view.Columns.Add("Kost1", typeof(int));
            view.Columns.Add("Kost2", typeof(int));
            view.Columns.Add("Konto", typeof(int));

            if (raw == null) return view;
            foreach (DataRow r in raw.Rows)
            {
                int beleg = SafeInt(r, "Belegnummer");
                string persName = SafeString(r, "PersName");
                string typRaw = SafeString(r, "Typ");
                string typTxt = MapTypCodeToText(typRaw);
                decimal b19 = SafeDec(r, "Betrag19");
                decimal b7 = SafeDec(r, "Betrag7");
                decimal b0 = SafeDec(r, "Betrag0");
                decimal betrag = b19 != 0m ? b19 : (b7 != 0m ? b7 : b0);
                string mwst = b19 != 0m ? "19" : (b7 != 0m ? "7" : (b0 != 0m ? "0" : string.Empty));
                string text = SafeString(r, "Buchungstext");
                int k1 = SafeInt(r, "Kost1");
                int k2 = SafeInt(r, "Kost2");
                int kto = SafeInt(r, "Konto");
                view.Rows.Add(beleg, persName, typTxt, betrag, mwst, text, k1, k2, kto);
            }
            return view;
        }

        private static int SafeInt(DataRow r, string col)
        {
            try { return (r.Table.Columns.Contains(col) && r[col] != DBNull.Value) ? Convert.ToInt32(r[col]) : 0; } catch { return 0; }
        }
        private static decimal SafeDec(DataRow r, string col)
        {
            try { return (r.Table.Columns.Contains(col) && r[col] != DBNull.Value) ? Convert.ToDecimal(r[col]) : 0m; } catch { return 0m; }
        }
        private static string SafeString(DataRow r, string col)
        {
            try { return (r.Table.Columns.Contains(col) && r[col] != DBNull.Value) ? Convert.ToString(r[col]) ?? string.Empty : string.Empty; } catch { return string.Empty; }
        }

        private void ConfigurePaymentsGrid()
        {
            if (gvZahlungen == null) return;
            gvZahlungen.AutoGenerateColumns = false;
            gvZahlungen.Columns.Clear();
            DataGridViewTextBoxColumn Add(string name, string header, int width = 80, string format = null, bool fill = false)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = name,
                    HeaderText = header,
                    Name = name,
                    AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                    Width = fill ? 200 : width
                };
                if (format != null) col.DefaultCellStyle.Format = format;
                gvZahlungen.Columns.Add(col);
                return col;
            }
            Add("Belegnummer", "Belegnr.", 70);
            Add("PersName", "Mitarbeiter", 160);
            Add("Typ", "Typ", 110);
            var colBetrag = Add("Betrag", "Betrag", 90);
            colBetrag.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            var colMwst = Add("MwSt", "MwSt", 60);
            colMwst.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            Add("Buchungstext", "Text", 0, null, true);
            Add("Kost1", "Kost1", 60);
            Add("Kost2", "Kost2", 60);
            Add("Konto", "Konto", 70);
        }

        private void GvZahlungen_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                var gv = sender as DataGridView; if (gv == null) return;
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var name = gv.Columns[e.ColumnIndex].DataPropertyName ?? gv.Columns[e.ColumnIndex].Name;
                if (name == "Betrag")
                {
                    if (e.Value == null || e.Value == DBNull.Value) { e.Value = "-"; e.FormattingApplied = true; return; }
                    decimal v; if (decimal.TryParse(Convert.ToString(e.Value), out v)) { e.Value = v.ToString("N2") + " €"; e.FormattingApplied = true; return; }
                }
                else if (name == "MwSt")
                {
                    var s = Convert.ToString(e.Value) ?? string.Empty; if (s.Length > 0) { e.Value = s + " %"; e.FormattingApplied = true; }
                }
                else if (name == "Typ")
                {
                    var s = Convert.ToString(e.Value) ?? string.Empty; if (!string.IsNullOrWhiteSpace(s)) { e.Value = MapTypCodeToText(s); e.FormattingApplied = true; }
                }
            }
            catch { }
        }

        private void GvGuthaben_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                var gv = sender as DataGridView; if (gv == null) return;
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var name = gv.Columns[e.ColumnIndex].DataPropertyName ?? gv.Columns[e.ColumnIndex].Name;
                if (string.Equals(name, "Saldo", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Betrag", StringComparison.OrdinalIgnoreCase))
                {
                    if (e.Value == null || e.Value == DBNull.Value) { e.Value = "-"; e.FormattingApplied = true; return; }
                    decimal v; if (decimal.TryParse(Convert.ToString(e.Value), out v)) { e.Value = v.ToString("N2") + " €"; e.FormattingApplied = true; return; }
                }
            }
            catch { }
        }

        // Formatierung: Beträge mit 2 Nachkommastellen, 0,00 als "-" anzeigen
        private void ApplySchichtenFormatting()
        {
            if (gvSchichten == null || gvSchichten.Columns.Count == 0) return;
            void fmt(string col)
            {
                if (!gvSchichten.Columns.Contains(col)) return;
                var c = gvSchichten.Columns[col];
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                c.DefaultCellStyle.Format = "N2";
            }
            fmt("Betrag19"); fmt("Betrag7"); fmt("Betrag0"); fmt("OffenerBetrag");
            gvSchichten.CellFormatting -= GvSchichten_CellFormatting;
            gvSchichten.CellFormatting += GvSchichten_CellFormatting;
        }

        private void GvSchichten_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                var gv = sender as DataGridView;
                if (gv == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var name = gv.Columns[e.ColumnIndex].DataPropertyName ?? gv.Columns[e.ColumnIndex].Name;
                if (name == "Betrag19" || name == "Betrag7" || name == "Betrag0" || name == "OffenerBetrag")
                {
                    if (e.Value == null || e.Value == DBNull.Value) { e.Value = "-"; e.FormattingApplied = true; return; }
                    decimal v;
                    if (decimal.TryParse(Convert.ToString(e.Value), out v))
                    {
                        if (Math.Abs(v) < 0.0000001m) { e.Value = "-"; e.FormattingApplied = true; return; }
                        e.Value = v.ToString("N2") + " €"; e.FormattingApplied = true; return;
                    }
                }
            }
            catch { }
        }

        // Grid-Layout laden/speichern via AppConfig (XML unter ProgramData)
        private void LoadGridLayout(string key, DataGridView gv)
        {
            try
            {
                if (gv == null) return;
                var cfg = new AppConfig();
                cfg.LoadXML(_gridCfgPath, _gridCfgApp);
                cfg.GetDgvColumns("OffeneUebersicht", key, gv);
            }
            catch { }
        }

        private void SaveGridLayout(string key, DataGridView gv)
        {
            try
            {
                if (gv == null) return;
                var dir = Path.GetDirectoryName(_gridCfgPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var cfg = new AppConfig();
                cfg.LoadXML(_gridCfgPath, _gridCfgApp);
                cfg.SetDgvColumns("OffeneUebersicht", key, gv);
                cfg.SaveXML(_gridCfgPath, _gridCfgApp);
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                SaveGridLayout("Schichten", gvSchichten);
                SaveGridLayout("Zahlungen", gvZahlungen);
                SaveGridLayout("Guthaben", gvGuthaben);
            }
            catch { }
            base.OnFormClosed(e);
        }
    }
}
