using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TaMi_Kassenclient
{
    public class OffeneUebersichtForm : KassenclientBaseForm
    {
        private TabControl tabs;
        private DataGridView gvSchichten;
        private DataGridView gvZahlungen;
        private DataGridView gvGuthaben;

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
                Top = 60,
                Width = this.ClientSize.Width - 24,
                Height = this.ClientSize.Height - 72,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var t1 = new TabPage("Offene Schichten");
            var t2 = new TabPage("Offene Zahlungen");
            var t3 = new TabPage("Personalguthaben");

            gvSchichten = MakeGrid();
            gvZahlungen = MakeGrid();
            gvGuthaben = MakeGrid();

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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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
                    gvZahlungen.DataSource = dt;
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
                }
            }
            catch { gvGuthaben.DataSource = null; }
        }
    }
}
