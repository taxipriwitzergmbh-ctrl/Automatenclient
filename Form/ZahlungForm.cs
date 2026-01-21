using System;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace TaMi_Automatenclient
{
    public class ZahlungForm : Form
    {
        // UI Felder
        private Panel headerPanel; 
        private Button btnClose; 
        private Label lblTitle; 
        private Point _mouseDownLocation;
        private ComboBox cboMitarbeiter; 
        private TextBox txtPid; 
        private ComboBox cboTyp; 
        private ComboBox cboMwst; 
        private ComboBox cboFirma; 
        private TextBox txtText; 
        private TextBox txtK1, txtK2, txtKto; 
        private NumericUpDown nudAmount; 
        private Button btnAnlegen; 
        private Button btnSaveChanges; 
        private Button btnStorno; 
        private Label lblInfo; 
        private Label lblSelName; 
        private DataGridView dgvOpen; 
        private Label lblOpenCaption; 
        private ComboBox cboPreset; 
        private Button btnCreatePreset;

        // Status
        private bool _personalLoaded; 
        private bool _presetsLoaded; 
        private bool _mandantenLoaded; 
        private bool _autoLoaded; 
        private bool _suppressPresetEvents; 
        private bool _currentSelectedGesperrt; private int? _editBeleg;

        // Suche
        private string _mitarbeiterTypeBuffer = string.Empty; 
        private DateTime _mitarbeiterTypeLastKey = DateTime.MinValue; 
        private const int MitarbeiterTypeTimeoutMs = 1000;

        // Firmen Cache
        private Dictionary<int,string> _firmenMap; 
        private bool _firmenLoaded;

        private class PresetListItem { public string Text { get; set; } public DataRow Row { get; set; } public override string ToString() => Text; }

        private static readonly Color Accent = Color.FromArgb(33,150,243); 
        private static readonly Color AccentDark = Color.FromArgb(25,118,210);

        public ZahlungForm()
        {
            this.Icon = Program.AppIcon;
            BuildUI();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_autoLoaded) 
                return; 
            
            _autoLoaded = true;

            var t = new Timer { Interval = 120 };
            t.Tick += (s, ev) => { t.Stop(); t.Dispose(); _ = LoadMandantenAsync(); _ = LoadAccountingPresetsAsync(); _ = LoadPersonalAsync(); };
            t.Start();
        }

        private void ZahlungForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (txtPid.Focused) { e.SuppressKeyPress = true; _ = SyncPidToSelectionAsync(); }
                else { e.SuppressKeyPress = true; _ = CreatePaymentAsync(); }
            }
            if (e.KeyCode == Keys.Escape) Close();
        }

        private void BuildUI()
        {
            // Breite erhöht (vorher 760)
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Text = "Neue Zahlung";
            ClientSize = new Size(940, 700);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += ZahlungForm_KeyDown;

            SuspendLayout();

            headerPanel = new Panel { Location = new Point(0,0), Size = new Size(ClientSize.Width,60), Anchor = AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            headerPanel.Paint += (s,e)=> { using(var lg=new LinearGradientBrush(headerPanel.ClientRectangle,Accent,AccentDark,0f)) e.Graphics.FillRectangle(lg,headerPanel.ClientRectangle); using(var pen=new Pen(Color.FromArgb(13,71,161))) e.Graphics.DrawLine(pen,0,headerPanel.Height-1,headerPanel.Width,headerPanel.Height-1); };
            headerPanel.MouseDown += (s,e)=> { if(e.Button==MouseButtons.Left) _mouseDownLocation=e.Location; };
            headerPanel.MouseMove += (s,e)=> { if(e.Button==MouseButtons.Left){ Left += e.X - _mouseDownLocation.X; Top += e.Y - _mouseDownLocation.Y; } };
            Controls.Add(headerPanel);
            lblTitle = new Label { Text="Zahlung erfassen", AutoSize=false, Font=new Font("Segoe UI",18F,FontStyle.Bold), ForeColor=Color.White, Location=new Point(24,0), Size=new Size(480,60), TextAlign=ContentAlignment.MiddleLeft, BackColor=Color.Transparent}; headerPanel.Controls.Add(lblTitle);
            btnClose = new Button { Text="\u2715", Font=new Font("Segoe UI Symbol",18F,FontStyle.Bold), ForeColor=Color.White, BackColor=Color.Transparent, FlatStyle=FlatStyle.Flat, Size=new Size(48,48), Location=new Point(ClientSize.Width-56,6), Anchor=AnchorStyles.Top|AnchorStyles.Right, TabStop=false }; btnClose.FlatAppearance.BorderSize=0; btnClose.FlatAppearance.MouseOverBackColor=Color.FromArgb(255,80,80); btnClose.Click += (s,e)=> Close(); headerPanel.Controls.Add(btnClose);

            var content = new Panel { Left=0, Top=headerPanel.Bottom, Width=ClientSize.Width, Height=ClientSize.Height-60, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom, BackColor=Color.White };
            Controls.Add(content);
            int y=20; int labelW=110;
            content.Controls.Add(new Label{ Text="Personalnr:", Left=24, Top=y+6, Width=labelW });
            txtPid = new TextBox { Left=24+labelW, Top=y, Width=140, Font=new Font("Segoe UI",12F), TextAlign=HorizontalAlignment.Center };
            txtPid.Leave += async (s,e)=> await SyncPidToSelectionAsync();
            txtPid.KeyDown += (s,e)=> { if(e.KeyCode==Keys.Enter){ e.SuppressKeyPress=true; _=SyncPidToSelectionAsync(); } };
            content.Controls.Add(txtPid);
            content.Controls.Add(new Label{ Text="Mitarbeiter:", Left=24+labelW+160, Top=y+6, Width=labelW });
            cboMitarbeiter = new ComboBox { Left=24+labelW+160+labelW, Top=y, Width=300, DropDownStyle=ComboBoxStyle.DropDownList }; // etwas breiter
            cboMitarbeiter.DropDown += async (s,e)=> { if(!_personalLoaded){ try{ Cursor.Current=Cursors.WaitCursor; cboMitarbeiter.Enabled=false; await LoadPersonalAsync(); } finally { cboMitarbeiter.Enabled=true; Cursor.Current=Cursors.Default; } } };
            cboMitarbeiter.SelectedIndexChanged += async (s,e)=>
            {
                if (cboMitarbeiter.SelectedItem is DataRowView drv)
                {
                    txtPid.Text = drv["PID"].ToString();
                    bool ges=false; try{ if(drv.Row.Table.Columns.Contains("Gesperrt") && drv["Gesperrt"]!=DBNull.Value) ges=Convert.ToBoolean(drv["Gesperrt"]);}catch{}
                    _currentSelectedGesperrt=ges; lblSelName.Text = ges?"Mitarbeiter gesperrt: "+drv["Name"]:"Ausgewählt: "+drv["Name"];
                    await LoadOpenPaymentsAsync();
                }
            };
            cboMitarbeiter.KeyPress += CboMitarbeiter_KeyPress; content.Controls.Add(cboMitarbeiter); y+=44;
            lblSelName = new Label { Left=24, Top=y-4, Width=ClientSize.Width-48, Height=24, ForeColor=Color.FromArgb(55,71,79), Font=new Font("Segoe UI",9.5f,FontStyle.Italic)}; content.Controls.Add(lblSelName); y+=12;
            content.Controls.Add(new Label{ Text="Vorlage:", Left=24, Top=y+32+6, Width=labelW });
            cboPreset = new ComboBox { Left=24+labelW, Top=y+32, Width=420, DropDownStyle=ComboBoxStyle.DropDownList }; // breiter
            cboPreset.DropDown += async (s,e)=> { if(!_presetsLoaded){ try{ Cursor.Current=Cursors.WaitCursor; await LoadAccountingPresetsAsync(); } finally { Cursor.Current=Cursors.Default; } } };
            cboPreset.SelectedIndexChanged += (s,e)=> { if(_suppressPresetEvents || !_presetsLoaded) return; ApplyPresetToFields(); };
            content.Controls.Add(cboPreset);
            btnCreatePreset = new Button { Text="+", Left=cboPreset.Right+8, Top=y+31, Width=40, Height=30, BackColor=Accent, ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            btnCreatePreset.FlatAppearance.BorderSize=0; btnCreatePreset.Click += (s,e)=> ShowPresetOverlay(); content.Controls.Add(btnCreatePreset);
            content.Controls.Add(new Label{ Text="Typ:", Left=24, Top=y+80+6, Width=labelW });
            cboTyp = new ComboBox { Left=24+labelW, Top=y+80, Width=160, DropDownStyle=ComboBoxStyle.DropDownList };
            cboTyp.Items.AddRange(new object[]{"Bitte auswählen","Einzahlung","Auszahlung"}); cboTyp.SelectedIndex=0; content.Controls.Add(cboTyp);
            content.Controls.Add(new Label{ Text="MwSt:", Left=cboTyp.Right+20, Top=y+80+6, Width=50 });
            cboMwst = new ComboBox { Left=cboTyp.Right+20+50, Top=y+80, Width=80, DropDownStyle=ComboBoxStyle.DropDownList };
            cboMwst.Items.AddRange(new object[]{"Bitte auswählen","19","7","0"}); cboMwst.SelectedIndex=0; content.Controls.Add(cboMwst);

            // NEU: Betrag vor Firma und größere Breiten
            int betragLabelLeft = cboMwst.Right + 30;
            content.Controls.Add(new Label{ Text="Betrag:", Left=betragLabelLeft, Top=y+80+6, Width=55 });
            nudAmount = new NumericUpDown { Left=betragLabelLeft + 55, Top=y+80, Width=100, DecimalPlaces=2, Maximum=1000000, Minimum=-1000000, Increment=0.10M }; // schmaler
            content.Controls.Add(nudAmount);
            int firmaLabelLeft = nudAmount.Right + 25; // etwas geringerer Abstand
            content.Controls.Add(new Label{ Text="Firma:", Left=firmaLabelLeft, Top=y+80+6, Width=55 });
            cboFirma = new ComboBox { Left=firmaLabelLeft + 55, Top=y+80, Width=180, DropDownStyle=ComboBoxStyle.DropDownList }; // schmaler gemacht
            cboFirma.DropDown += async (s,e)=> { if(!_mandantenLoaded){ try{ Cursor.Current=Cursors.WaitCursor; await LoadMandantenAsync(); } finally { Cursor.Current=Cursors.Default; } } };
            content.Controls.Add(cboFirma);

            y+=140;
            content.Controls.Add(new Label{ Text="Buchungstext:", Left=24, Top=y+6, Width=labelW });
            txtText = new TextBox { Left=24+labelW, Top=y, Width=ClientSize.Width - (24+labelW+40) };
            txtText.Anchor = AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            content.Controls.Add(txtText); y+=44;
            int startX=24+labelW; int spacing=20;
            content.Controls.Add(new Label{ Text="Kost1:", Left=24, Top=y+6, Width=labelW });
            txtK1 = new TextBox { Left=startX, Top=y, Width=100 }; content.Controls.Add(txtK1);
            var lblK2 = new Label { Text="Kost2:", Left=txtK1.Right+spacing, Top=y+6, Width=60 }; content.Controls.Add(lblK2);
            txtK2 = new TextBox { Left=lblK2.Right+4, Top=y, Width=100 }; content.Controls.Add(txtK2);
            var lblKto = new Label { Text="Konto:", Left=txtK2.Right+spacing, Top=y+6, Width=60 }; content.Controls.Add(lblKto);
            txtKto = new TextBox { Left=lblKto.Right+4, Top=y, Width=110 }; content.Controls.Add(txtKto); y+=56;
            btnAnlegen = new Button { Text="Zahlung anlegen", Left=24+labelW, Top=y, Width=220, Height=44, BackColor=Color.FromArgb(46,125,50), ForeColor=Color.White, FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",11F,FontStyle.Bold) };
            btnAnlegen.FlatAppearance.BorderSize=0; btnAnlegen.Click += async (s,e)=> await CreatePaymentAsync(); content.Controls.Add(btnAnlegen);
            btnSaveChanges = new Button { Text="Änderung speichern", Left=btnAnlegen.Right+12, Top=y, Width=220, Height=44, BackColor=Color.FromArgb(3,155,229), ForeColor=Color.White, FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",11F,FontStyle.Bold), Enabled=false };
            btnSaveChanges.FlatAppearance.BorderSize=0; btnSaveChanges.Click += async (s,e)=> await SaveEditedPaymentAsync(); content.Controls.Add(btnSaveChanges);
            btnStorno = new Button { Text="Stornieren", Left=btnSaveChanges.Right+12, Top=y, Width=160, Height=44, BackColor=Color.FromArgb(229,57,53), ForeColor=Color.White, FlatStyle=FlatStyle.Flat, Font=new Font("Segoe UI",11F,FontStyle.Bold) };
            btnStorno.FlatAppearance.BorderSize=0; btnStorno.Enabled=false; btnStorno.Click += async (s,e)=> await StorniereAuswahlAsync(); content.Controls.Add(btnStorno); y+=60;
            lblOpenCaption = new Label { Text="Offene Zahlungen:", Left=24, Top=y, Width=content.Width-48, Font=new Font("Segoe UI",9.5f) }; content.Controls.Add(lblOpenCaption); y+=22;
            dgvOpen = new DataGridView { Left=24, Top=y, Width=content.Width-48, Height=5*26+28, ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, AllowUserToResizeRows=false, RowHeadersVisible=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false, BackgroundColor=Color.White, BorderStyle=BorderStyle.FixedSingle, AutoGenerateColumns=false, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            // Hervorhebung komplette Zeile in Blau
            dgvOpen.DefaultCellStyle.SelectionBackColor = Accent;
            dgvOpen.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvOpen.AlternatingRowsDefaultCellStyle.SelectionBackColor = Accent;
            dgvOpen.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            dgvOpen.SelectionChanged += (s,e)=> { btnStorno.Enabled = dgvOpen.CurrentRow!=null; };
            dgvOpen.CellDoubleClick += (s,e)=> { if(e.RowIndex>=0) LoadSelectedPaymentIntoFields(); };
            content.Controls.Add(dgvOpen);
            content.Resize += (s,e)=> { try{ dgvOpen.Width = content.ClientSize.Width - 48; lblOpenCaption.Width = content.ClientSize.Width - 48; txtText.Width = ClientSize.Width - (24+labelW+40); } catch{} };
            y += dgvOpen.Height + 16;
            lblInfo = new Label { Left=24, Top=y, Width=ClientSize.Width-48, Height=80, ForeColor=Color.DimGray, Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom }; content.Controls.Add(lblInfo);
            ResumeLayout(true);
        }

        // --- Incremental Search ---
        private void CboMitarbeiter_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(!_personalLoaded) return; if(cboMitarbeiter.Items.Count==0) return;
            if(char.IsControl(e.KeyChar)){ if(e.KeyChar==(char)Keys.Back && _mitarbeiterTypeBuffer.Length>0){ _mitarbeiterTypeBuffer=_mitarbeiterTypeBuffer.Substring(0,_mitarbeiterTypeBuffer.Length-1); e.Handled=true; PerformMitarbeiterSearch(); } return; }
            var now=DateTime.UtcNow; if((now-_mitarbeiterTypeLastKey).TotalMilliseconds> MitarbeiterTypeTimeoutMs) _mitarbeiterTypeBuffer=string.Empty; _mitarbeiterTypeLastKey=now; _mitarbeiterTypeBuffer += e.KeyChar.ToString(); e.Handled=true; PerformMitarbeiterSearch();
        }
        private void PerformMitarbeiterSearch(){ if(string.IsNullOrEmpty(_mitarbeiterTypeBuffer)) return; string search=_mitarbeiterTypeBuffer.ToLowerInvariant(); for(int i=0;i<cboMitarbeiter.Items.Count;i++){ if(cboMitarbeiter.Items[i] is DataRowView drv){ string name=Convert.ToString(drv["Name"])??string.Empty; if(name.ToLowerInvariant().StartsWith(search)){ cboMitarbeiter.SelectedIndex=i; return; } } } }

        // --- Daten Laden ---
        private async Task LoadPersonalAsync()
        {
            try
            {
                using(var db=new DatabaseHelperKassen())
                {
                    var dt=await db.GetActivePersonalAsync();
                    if(dt!=null && dt.Columns.Contains("Gesperrt"))
                    {
                        var filtered=dt.Clone();
                        foreach(DataRow r in dt.Rows){ bool ges=false; try{ if(r["Gesperrt"]!=DBNull.Value) ges=Convert.ToBoolean(r["Gesperrt"]);}catch{} if(!ges) filtered.ImportRow(r); }
                        dt=filtered;
                    }
                    cboMitarbeiter.DisplayMember="Name"; cboMitarbeiter.ValueMember="PID"; cboMitarbeiter.DataSource=dt; cboMitarbeiter.SelectedIndex=-1; _personalLoaded=true;
                }
            }
            catch { }
        }

        private async Task LoadAccountingPresetsAsync()
        {
            try
            {
                using(var db=new DatabaseHelperKassen())
                {
                    var dt=await db.LoadZahlungsVorlagenAsync();
                    _suppressPresetEvents=true; cboPreset.Items.Clear(); cboPreset.Items.Add(new PresetListItem{ Text="Vorlage auswählen", Row=null});
                    foreach(DataRow r in dt.Rows){ string name=Convert.ToString(r["VorlagenName"]); if(string.IsNullOrWhiteSpace(name)) continue; cboPreset.Items.Add(new PresetListItem{ Text=name, Row=r}); }
                    cboPreset.SelectedIndex=0; _presetsLoaded=true;
                }
            }
            catch { }
            finally { _suppressPresetEvents=false; }
        }

        private async Task LoadMandantenAsync()
        {
            try
            {
                using(var db=new DatabaseHelperKassen())
                {
                    var dt=await db.GetMandantenAsync();
                    if(!dt.Columns.Contains("ManID")) dt.Columns.Add("ManID",typeof(int));
                    if(!dt.Columns.Contains("ManName")) dt.Columns.Add("ManName",typeof(string));
                    var ph=dt.NewRow(); ph["ManID"]=0; ph["ManName"]="Bitte auswählen"; dt.Rows.InsertAt(ph,0);
                    cboFirma.DisplayMember="ManName"; cboFirma.ValueMember="ManID"; cboFirma.DataSource=dt; cboFirma.SelectedIndex=0; _mandantenLoaded=true;
                }
            }
            catch { }
        }

        // --- Preset anwenden ---
        private void ApplyPresetToFields()
        {
            if(!(cboPreset.SelectedItem is PresetListItem pi) || pi.Row==null) return;
            var row=pi.Row;
            if(row.Table.Columns.Contains("Buchungstext")) txtText.Text = Convert.ToString(row["Buchungstext"]) ?? string.Empty;
            txtK1.Text = row.Table.Columns.Contains("Kost1")? Convert.ToString(row["Kost1"]) : string.Empty;
            txtK2.Text = row.Table.Columns.Contains("Kost2")? Convert.ToString(row["Kost2"]) : string.Empty;
            txtKto.Text = row.Table.Columns.Contains("Konto")? Convert.ToString(row["Konto"]) : string.Empty;
            string typRaw = row.Table.Columns.Contains("Typ")? Convert.ToString(row["Typ"]) : null;
            string typText = MapTypCodeToText(typRaw ?? string.Empty);
            if(!string.IsNullOrWhiteSpace(typText)){ int idx=cboTyp.FindStringExact(typText); if(idx>=0) cboTyp.SelectedIndex=idx; }
            try{
                decimal m19=0,m7=0,m0=0; if(row.Table.Columns.Contains("Betrag19") && row["Betrag19"]!=DBNull.Value) m19=Convert.ToDecimal(row["Betrag19"]); if(row.Table.Columns.Contains("Betrag7") && row["Betrag7"]!=DBNull.Value) m7=Convert.ToDecimal(row["Betrag7"]); if(row.Table.Columns.Contains("Betrag0") && row["Betrag0"]!=DBNull.Value) m0=Convert.ToDecimal(row["Betrag0"]);
                string target=null; if(m19==1m && m7==0m && m0==0m) target="19"; else if(m7==1m && m19==0m && m0==0m) target="7"; else if(m0==1m && m19==0m && m7==0m) target="0"; if(target!=null){ int ix=cboMwst.FindStringExact(target); if(ix>=0) cboMwst.SelectedIndex=ix; }
            }catch{}
            // FirmenID automatisch setzen (Kasse)
            try
            {
                int fid=0; if(row.Table.Columns.Contains("FirmenID") && row["FirmenID"]!=DBNull.Value) fid=Convert.ToInt32(row["FirmenID"]);
                Action assign = () => {
                    if(cboFirma.DataSource is DataTable man)
                    {
                        if(fid<=0){ if(cboFirma.Items.Count>0) cboFirma.SelectedIndex=0; return; }
                        for(int i=0;i<man.Rows.Count;i++) if(Convert.ToInt32(man.Rows[i]["ManID"])==fid){ cboFirma.SelectedIndex=i; return; }
                    }
                };
                if(!_mandantenLoaded){ _ = LoadMandantenAsync().ContinueWith(t=> { try { if(IsHandleCreated) BeginInvoke(assign); else assign(); } catch { } }); }
                else assign();
            }
            catch { }
        }

        // --- Validierung ---
        private bool ValidateEntry(bool editing, out string msg)
        {
            if(_currentSelectedGesperrt){ msg="Mitarbeiter gesperrt."; return false; }
            if(!int.TryParse(txtPid.Text.Trim(), out var pid) || pid<=0){ msg="Personalnummer ungültig."; return false; }
            if(cboTyp.SelectedIndex<=0){ msg="Bitte Typ auswählen."; return false; }
            if(cboMwst.SelectedIndex<=0){ msg="Bitte MwSt auswählen."; return false; }
            if(cboFirma.SelectedIndex<=0){ msg="Bitte Firma auswählen."; return false; }
            if(string.IsNullOrWhiteSpace(txtText.Text)){ msg="Buchungstext fehlt."; return false; }
            if(editing && !_editBeleg.HasValue){ msg="Keine Zahlung gewählt."; return false; }
            msg=null; return true;
        }

        // --- Erstellung ---
        private async Task CreatePaymentAsync()
        {
            if(!ValidateEntry(false, out var msg)){ if(msg!=null) MessageBox.Show(this,msg,"Hinweis",MessageBoxButtons.OK,MessageBoxIcon.Information); return; }
            string typText=cboTyp.SelectedItem as string; // Benutzer-Text
            string typCode = MapTypTextToCode(typText);
            string mwst=cboMwst.SelectedItem as string; decimal amount=nudAmount.Value; decimal b19=0,b7=0,b0=0; if(mwst=="19") b19=amount; else if(mwst=="7") b7=amount; else b0=amount; int? k1=int.TryParse(txtK1.Text,out var vk1)?(int?)vk1:null; int? k2=int.TryParse(txtK2.Text,out var vk2)?(int?)vk2:null; int? kto=int.TryParse(txtKto.Text,out var vkto)?(int?)vkto:null; int fid=0; try{ fid=Convert.ToInt32(cboFirma.SelectedValue);}catch{}
            try{ using(var db=new DatabaseHelperKassen()) await db.InsertOffeneZahlungAsync(int.Parse(txtPid.Text.Trim()), typCode, txtText.Text.Trim(), b19,b7,b0, k1,k2,kto,fid); lblInfo.Text=$"Zahlung gespeichert ({typText}, {amount:0.00} EUR)."; ResetEntryFields(); await LoadOpenPaymentsAsync(); }
            catch(Exception ex){ MessageBox.Show(this,"Fehler: "+ex.Message,"Fehler",MessageBoxButtons.OK,MessageBoxIcon.Error);} }

        private void ResetEntryFields(){ txtText.Clear(); nudAmount.Value=0; txtK1.Clear(); txtK2.Clear(); txtKto.Clear(); cboTyp.SelectedIndex=0; cboMwst.SelectedIndex=0; if(cboFirma.Items.Count>0) cboFirma.SelectedIndex=0; _editBeleg=null; btnSaveChanges.Enabled=false; }

        // --- Auswahl laden (Doppelklick) ---
        private void LoadSelectedPaymentIntoFields()
        {
            if(dgvOpen?.CurrentRow==null) return; var drv=dgvOpen.CurrentRow.DataBoundItem as DataRowView; if(drv==null) return; var row=drv.Row;
            _editBeleg = row.Table.Columns.Contains("Belegnummer") && row["Belegnummer"]!=DBNull.Value ? (int?)Convert.ToInt32(row["Belegnummer"]) : null;
            string typRaw = Convert.ToString(row["Typ"]) ?? string.Empty; string typText = MapTypCodeToText(typRaw); if(!string.IsNullOrWhiteSpace(typText)){ int ix=cboTyp.FindStringExact(typText); if(ix>=0) cboTyp.SelectedIndex=ix; }
            decimal b19 = row.Table.Columns.Contains("Betrag19") && row["Betrag19"]!=DBNull.Value ? Convert.ToDecimal(row["Betrag19"]) : 0m;
            decimal b7  = row.Table.Columns.Contains("Betrag7")  && row["Betrag7"] !=DBNull.Value ? Convert.ToDecimal(row["Betrag7"])  : 0m;
            decimal b0  = row.Table.Columns.Contains("Betrag0")  && row["Betrag0"] !=DBNull.Value ? Convert.ToDecimal(row["Betrag0"])  : 0m;
            string mw = b19!=0m?"19": (b7!=0m?"7": (b0!=0m?"0":null)); if(mw!=null){ int mIdx=cboMwst.FindStringExact(mw); if(mIdx>=0) cboMwst.SelectedIndex=mIdx; }
            decimal amount = b19!=0m?b19: (b7!=0m?b7:b0); try{ nudAmount.Value = Math.Max(nudAmount.Minimum, Math.Min(nudAmount.Maximum, amount)); }catch{}
            txtText.Text = Convert.ToString(row["Buchungstext"]) ?? string.Empty;
            txtK1.Text = row.Table.Columns.Contains("Kost1") && row["Kost1"]!=DBNull.Value ? Convert.ToString(row["Kost1"]) : string.Empty;
            txtK2.Text = row.Table.Columns.Contains("Kost2") && row["Kost2"]!=DBNull.Value ? Convert.ToString(row["Kost2"]) : string.Empty;
            txtKto.Text = row.Table.Columns.Contains("Konto") && row["Konto"]!=DBNull.Value ? Convert.ToString(row["Konto"]) : string.Empty;
            if(row.Table.Columns.Contains("FirmenID") && row["FirmenID"]!=DBNull.Value && cboFirma.DataSource is DataTable manDt){ int fid=0; try{ fid=Convert.ToInt32(row["FirmenID"]);}catch{} for(int i=0;i<manDt.Rows.Count;i++) if(Convert.ToInt32(manDt.Rows[i]["ManID"])==fid){ cboFirma.SelectedIndex=i; break; } }
            btnSaveChanges.Enabled = _editBeleg.HasValue;
        }

        // --- Änderungen speichern ---
        private async Task SaveEditedPaymentAsync()
        {
            if(!ValidateEntry(true, out var msg)){ if(msg!=null) MessageBox.Show(this,msg,"Hinweis",MessageBoxButtons.OK,MessageBoxIcon.Information); return; }
            if(!_editBeleg.HasValue) return; string typText=cboTyp.SelectedItem as string; string typCode=MapTypTextToCode(typText); string mwst=cboMwst.SelectedItem as string; decimal amount=nudAmount.Value; decimal b19=0,b7=0,b0=0; if(mwst=="19") b19=amount; else if(mwst=="7") b7=amount; else b0=amount; int? k1=int.TryParse(txtK1.Text,out var vk1)?(int?)vk1:null; int? k2=int.TryParse(txtK2.Text,out var vk2)?(int?)vk2:null; int? kto=int.TryParse(txtKto.Text,out var vkto)?(int?)vkto:null; int fid=0; try{ fid=Convert.ToInt32(cboFirma.SelectedValue);}catch{}
            try{ using(var db=new DatabaseHelperKassen()){ int n=await db.UpdateOffeneZahlungAsync(_editBeleg.Value, typCode, txtText.Text.Trim(), b19,b7,b0, k1,k2,kto); if(n<=0){ MessageBox.Show(this,"Änderung nicht möglich (evtl. verbucht).","Hinweis",MessageBoxButtons.OK,MessageBoxIcon.Information); return; } } lblInfo.Text="Änderungen gespeichert."; _editBeleg=null; btnSaveChanges.Enabled=false; await LoadOpenPaymentsAsync(); }
            catch(Exception ex){ MessageBox.Show(this,"Fehler beim Speichern: "+ex.Message,"Fehler",MessageBoxButtons.OK,MessageBoxIcon.Error);} }

        // --- Storno ---
        private async Task StorniereAuswahlAsync()
        {
            if(dgvOpen?.CurrentRow==null) return; var row=(dgvOpen.CurrentRow.DataBoundItem as DataRowView)?.Row; if(row==null) return; if(!row.Table.Columns.Contains("Belegnummer")) return; int beleg=0; try{ beleg=Convert.ToInt32(row["Belegnummer"]);}catch{} if(beleg<=0) return; if(MessageBox.Show(this,$"Zahlung {beleg} wirklich stornieren?","Bestätigung",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            try{ using(var db=new DatabaseHelperKassen()) { int n=await db.StorniereOffeneZahlungAsync(beleg); if(n<=0){ MessageBox.Show(this,"Storno nicht möglich (evtl. verbucht).","Hinweis",MessageBoxButtons.OK,MessageBoxIcon.Information); return; } } await LoadOpenPaymentsAsync(); }
            catch(Exception ex){ MessageBox.Show(this,"Fehler beim Stornieren: "+ex.Message,"Fehler",MessageBoxButtons.OK,MessageBoxIcon.Error);} }

        // --- Grid ---
        private void ConfigureOpenGridColumns()
        {
            dgvOpen.Columns.Clear();
            DataGridViewTextBoxColumn Add(string name,string header,int width=80,string format=null,bool fill=false){ var col=new DataGridViewTextBoxColumn{ DataPropertyName=name, HeaderText=header, Name=name, AutoSizeMode= fill? DataGridViewAutoSizeColumnMode.Fill: DataGridViewAutoSizeColumnMode.None, Width= fill?200:width }; if(format!=null) col.DefaultCellStyle.Format=format; dgvOpen.Columns.Add(col); return col; }
            Add("Belegnummer","Belegnr.",60); Add("Typ","Typ",70); Add("BetragGesamt","Betrag",70,"0.00"); Add("MwSt","MwSt",50); Add("FirmenName","Firma",160); Add("Buchungstext","Text",0,null,true); Add("Kost1","Kost1",55); Add("Kost2","Kost2",55); Add("Konto","Konto",60);
        }

        private async Task EnsureFirmenMapAsync(){ if(_firmenLoaded) return; _firmenMap=new Dictionary<int,string>(); try{ using(var db=new DatabaseHelperKassen()) { var dt=await db.GetMandantenAsync(); foreach(DataRow r in dt.Rows){ int id; if(!int.TryParse(Convert.ToString(r["ManID"]), out id)) continue; string name=Convert.ToString(r["ManName"])??("ID "+id); if(!_firmenMap.ContainsKey(id)) _firmenMap.Add(id,name); } } } catch{} _firmenLoaded=true; }
        private string ResolveFirmenName(object raw){ try{ if(raw==null||raw==DBNull.Value) return string.Empty; int id; if(!int.TryParse(Convert.ToString(raw), out id)) return Convert.ToString(raw); if(_firmenMap!=null && _firmenMap.TryGetValue(id,out var n)) return n; return id==0?string.Empty:("ID "+id);} catch { return string.Empty; } }

        private async Task LoadOpenPaymentsAsync()
        {
            try
            {
                if(!int.TryParse(txtPid.Text.Trim(), out var pid) || pid<=0){ dgvOpen.DataSource=null; lblOpenCaption.Text="Offene Zahlungen:"; return; }
                await EnsureFirmenMapAsync();
                using(var db=new DatabaseHelperKassen())
                {
                    var raw=await db.GetOffeneAuszahlungenAsync(pid);
                    var view=new DataTable(); view.Columns.Add("Belegnummer",typeof(int)); view.Columns.Add("Typ",typeof(string)); view.Columns.Add("BetragGesamt",typeof(decimal)); view.Columns.Add("MwSt",typeof(string)); view.Columns.Add("FirmenName",typeof(string)); view.Columns.Add("Buchungstext",typeof(string)); view.Columns.Add("Kost1",typeof(int)); view.Columns.Add("Kost2",typeof(int)); view.Columns.Add("Konto",typeof(int)); view.Columns.Add("Betrag19",typeof(decimal)); view.Columns.Add("Betrag7",typeof(decimal)); view.Columns.Add("Betrag0",typeof(decimal)); view.Columns.Add("FirmenID",typeof(int));
                    foreach(DataRow r in raw.Rows)
                    {
                        int beleg = r.Table.Columns.Contains("Belegnummer") && r["Belegnummer"]!=DBNull.Value ? Convert.ToInt32(r["Belegnummer"]) : 0;
                        string typRaw = Convert.ToString(r["Typ"]) ?? string.Empty; string typText = MapTypCodeToText(typRaw);
                        decimal b19 = r.Table.Columns.Contains("Betrag19") && r["Betrag19"]!=DBNull.Value ? Convert.ToDecimal(r["Betrag19"]) : 0m;
                        decimal b7  = r.Table.Columns.Contains("Betrag7")  && r["Betrag7"] !=DBNull.Value ? Convert.ToDecimal(r["Betrag7"])  : 0m;
                        decimal b0  = r.Table.Columns.Contains("Betrag0")  && r["Betrag0"] !=DBNull.Value ? Convert.ToDecimal(r["Betrag0"])  : 0m;
                        decimal ges = r.Table.Columns.Contains("BetragGesamt") && r["BetragGesamt"]!=DBNull.Value ? Convert.ToDecimal(r["BetragGesamt"]) : (b19+b7+b0);
                        string mw  = b19>0m?"19": (b7>0m?"7": (b0>0m?"0": string.Empty));
                        int fid=0; try{ if(r.Table.Columns.Contains("FirmenID") && r["FirmenID"]!=DBNull.Value) fid=Convert.ToInt32(r["FirmenID"]);}catch{}
                        string firma = ResolveFirmenName(fid);
                        int k1 = r.Table.Columns.Contains("Kost1") && r["Kost1"]!=DBNull.Value ? Convert.ToInt32(r["Kost1"]) : 0;
                        int k2 = r.Table.Columns.Contains("Kost2") && r["Kost2"]!=DBNull.Value ? Convert.ToInt32(r["Kost2"]) : 0;
                        int kto = r.Table.Columns.Contains("Konto") && r["Konto"]!=DBNull.Value ? Convert.ToInt32(r["Konto"]) : 0;
                        string textVal = Convert.ToString(r["Buchungstext"]) ?? string.Empty;
                        view.Rows.Add(beleg,typText,ges,mw,firma,textVal,k1,k2,kto,b19,b7,b0,fid);
                    }
                    ConfigureOpenGridColumns(); dgvOpen.DataSource=view; lblOpenCaption.Text = $"Offene Zahlungen: {view.Rows.Count}";
                }
            }
            catch { try{ dgvOpen.DataSource=null; } catch {} }
        }

        // --- PID Sync ---
        private async Task SyncPidToSelectionAsync()
        {
            if(!int.TryParse(txtPid.Text.Trim(), out var pid)) return;
            if(cboMitarbeiter.DataSource is DataTable dt)
            {
                foreach(DataRow r in dt.Rows)
                {
                    if(Convert.ToInt32(r["PID"])==pid)
                    {
                        if(Convert.ToInt32(cboMitarbeiter.SelectedValue ?? -1)!=pid) cboMitarbeiter.SelectedValue=pid;
                        bool ges=false; try{ if(r.Table.Columns.Contains("Gesperrt") && r["Gesperrt"]!=DBNull.Value) ges=Convert.ToBoolean(r["Gesperrt"]);}catch{}
                        _currentSelectedGesperrt=ges; lblSelName.Text = ges?"Mitarbeiter gesperrt: "+r["Name"]:"Ausgewählt: "+r["Name"];
                        await LoadOpenPaymentsAsync(); return;
                    }
                }
            }
            using(var db=new DatabaseHelperKassen())
            {
                var p = await db.GetPersonalInfoAsync(pid);
                if(p!=null)
                {
                    _currentSelectedGesperrt=false; lblSelName.Text=$"Ausgewählt: {p.Name} {p.Vorname}";
                }
                else
                {
                    lblSelName.Text="Personalnummer nicht gefunden."; _currentSelectedGesperrt=false;
                }
            }
            await LoadOpenPaymentsAsync();
        }

        private void SelectPresetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || cboPreset == null) return;
            for (int i = 0; i < cboPreset.Items.Count; i++)
            {
                if (cboPreset.Items[i] is PresetListItem pli && string.Equals(pli.Text, name, StringComparison.OrdinalIgnoreCase))
                { cboPreset.SelectedIndex = i; break; }
            }
        }

        // --- Vorlagenverwaltung (implementiert) ---
        private async void ShowPresetOverlay()
        {
            var dlg = new Form
            {
                Text = "Vorlagen verwalten",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(760, 520),
                BackColor = Color.White,
                ShowInTaskbar = false
            };
            Button btnSave = null;
            ComboBox cbFirma = null; // Kasse Auswahl
            dlg.KeyPreview = true;
            dlg.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); }
                else if (e.KeyCode == Keys.Enter && btnSave != null && btnSave.Enabled) btnSave.PerformClick();
            };

            var header = new Panel { Left = 0, Top = 0, Width = dlg.ClientSize.Width, Height = 54, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            header.Paint += (s, e) =>
            {
                using (var lg = new LinearGradientBrush(header.ClientRectangle, Accent, AccentDark, 0f))
                    e.Graphics.FillRectangle(lg, header.ClientRectangle);
                using (var pen = new Pen(Color.FromArgb(13, 71,161)))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            var hdrLabel = new Label { Text = "Vorlagen verwalten", AutoSize = false, Left = 20, Top = 0, Width = 400, Height = 54, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            var hdrClose = new Button { Text = "X", FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Size = new Size(48, 48), Location = new Point(header.Width - 56, 3), Anchor = AnchorStyles.Top | AnchorStyles.Right, TabStop = false };
            hdrClose.FlatAppearance.BorderSize = 0; hdrClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(229, 57, 53); hdrClose.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            header.Controls.Add(hdrLabel); header.Controls.Add(hdrClose); dlg.Controls.Add(header);

            var body = new Panel { Left = 0, Top = header.Bottom, Width = dlg.ClientSize.Width, Height = dlg.ClientSize.Height - header.Height, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.White };
            dlg.Controls.Add(body);

            string searchPlaceholder = "Suchen...";
            var txtSearch = new TextBox { Left = 24, Top = 12, Width = 220, Text = searchPlaceholder, ForeColor = Color.Gray };
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == searchPlaceholder) { txtSearch.Text = string.Empty; txtSearch.ForeColor = Color.Black; } };
            txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = searchPlaceholder; txtSearch.ForeColor = Color.Gray; } };
            var lst = new ListBox { Left = 24, Top = txtSearch.Bottom + 6, Width = 220, Height = 320, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom, BorderStyle = BorderStyle.FixedSingle };
            body.Controls.Add(txtSearch); body.Controls.Add(lst);

            var btnNeu = new Button { Text = "Neu", Left = 24, Width = 80, Height = 34, Top = body.Height - 46, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNeu.FlatAppearance.BorderSize = 0;
            var btnDelete = new Button { Text = "Löschen", Left = btnNeu.Right + 8, Width = 90, Height = 34, Top = body.Height - 46, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = Color.FromArgb(229, 57, 53), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDelete.FlatAppearance.BorderSize = 0; body.Controls.Add(btnNeu); body.Controls.Add(btnDelete);

            int baseX = 270; int wLabel = 70; int curY = 14; int spacing = 30;
            Func<string, Label> makeLbl = t => new Label { Text = t, Left = baseX, Top = curY + 4, Width = wLabel, ForeColor = Color.FromArgb(55, 71, 79) };
            var cbTyp = new ComboBox { Left = baseX + wLabel + 4, Top = curY, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cbTyp.Items.AddRange(new object[] { "Einzahlung", "Auszahlung" }); cbTyp.SelectedIndex = 0; body.Controls.Add(makeLbl("Typ:")); body.Controls.Add(cbTyp); curY += spacing;
            var tbName = new TextBox { Left = baseX + wLabel + 4, Top = curY, Width = 300 }; body.Controls.Add(makeLbl("Name:")); body.Controls.Add(tbName); curY += spacing;
            var cbMwst = new ComboBox { Left = baseX + wLabel + 4, Top = curY, Width = 70, DropDownStyle = ComboBoxStyle.DropDownList }; cbMwst.Items.AddRange(new object[] { "19", "7", "0" }); cbMwst.SelectedIndex = 0; body.Controls.Add(makeLbl("MwSt:")); body.Controls.Add(cbMwst); curY += spacing;
            var tbTxt = new TextBox { Left = baseX + wLabel + 4, Top = curY, Width = 360 }; body.Controls.Add(makeLbl("Text:")); body.Controls.Add(tbTxt); curY += spacing;
            cbFirma = new ComboBox { Left = baseX + wLabel + 4, Top = curY, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList }; body.Controls.Add(makeLbl("Kasse:")); body.Controls.Add(cbFirma); curY += spacing;
            var tbK1 = new TextBox { Left = baseX + wLabel + 4, Top = curY, Width = 70 }; var lblK2 = new Label { Text = "Kost2:", Left = tbK1.Right + 14, Top = curY + 4, Width = 50, ForeColor = Color.FromArgb(55, 71, 79) }; var tbK2 = new TextBox { Left = lblK2.Right + 4, Top = curY, Width = 70 }; var lblKto = new Label { Text = "Konto:", Left = tbK2.Right + 14, Top = curY + 4, Width = 50, ForeColor = Color.FromArgb(55, 71, 79) }; var tbKto = new TextBox { Left = lblKto.Right + 4, Top = curY, Width = 80 }; body.Controls.Add(makeLbl("Kost1:")); body.Controls.Add(tbK1); body.Controls.Add(lblK2); body.Controls.Add(tbK2); body.Controls.Add(lblKto); body.Controls.Add(tbKto); curY += spacing + 6;

            var btnCancel = new Button { Text = "Abbrechen", Left = baseX + wLabel + 4, Top = body.Height - 46, Width = 140, Height = 40, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.Gainsboro, FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0; btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            btnSave = new Button { Text = "Speichern", Left = btnCancel.Right + 12, Top = body.Height - 46, Width = 160, Height = 40, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
            btnSave.FlatAppearance.BorderSize = 0; body.Controls.Add(btnCancel); body.Controls.Add(btnSave);

            int? editBeleg = null; var allItems = new List<PresetListItem>();
            Action applyFilter = () => { string f = (txtSearch.Text == searchPlaceholder ? string.Empty : txtSearch.Text).Trim().ToLowerInvariant(); lst.BeginUpdate(); lst.Items.Clear(); foreach (var item in allItems) if (f.Length == 0 || item.Text.ToLowerInvariant().Contains(f)) lst.Items.Add(item); lst.EndUpdate(); };
            Action clearFields = () => { editBeleg = null; cbTyp.SelectedIndex = 0; cbMwst.SelectedIndex = 0; tbName.Clear(); tbTxt.Clear(); tbK1.Clear(); tbK2.Clear(); tbKto.Clear(); if (cbFirma.Items.Count>0) cbFirma.SelectedIndex = 0; lst.ClearSelected(); btnSave.Enabled = false; };

            async Task loadMandantenAsync()
            {
                try
                {
                    using (var db = new DatabaseHelperKassen())
                    {
                        var dt = await db.GetMandantenAsync();
                        if(!dt.Columns.Contains("ManID")) dt.Columns.Add("ManID", typeof(int));
                        if(!dt.Columns.Contains("ManName")) dt.Columns.Add("ManName", typeof(string));
                        var blank = dt.NewRow(); blank["ManID"] = 0; blank["ManName"] = "(leer)"; dt.Rows.InsertAt(blank,0);
                        cbFirma.DisplayMember = "ManName"; cbFirma.ValueMember = "ManID"; cbFirma.DataSource = dt; cbFirma.SelectedIndex = 0;
                    }
                }
                catch { }
            }

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
                            string name = Convert.ToString(r["VorlagenName"]); if (string.IsNullOrWhiteSpace(name)) name = "(ohne Name)"; allItems.Add(new PresetListItem { Text = name, Row = r });
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
                if (lst.SelectedItem is PresetListItem ciSel && ciSel.Row != null)
                {
                    var r = ciSel.Row; editBeleg = r.Table.Columns.Contains("Belegnummer") ? (int?)Convert.ToInt32(r["Belegnummer"]) : null;
                    cbTyp.SelectedIndex = cbTyp.FindStringExact(Convert.ToString(r["Typ"]) ?? "Einzahlung");
                    tbName.Text = Convert.ToString(r["VorlagenName"]) ?? string.Empty;
                    tbTxt.Text = Convert.ToString(r["Buchungstext"]) ?? string.Empty;
                    tbK1.Text = r["Kost1"] == DBNull.Value ? string.Empty : Convert.ToString(r["Kost1"]);
                    tbK2.Text = r["Kost2"] == DBNull.Value ? string.Empty : Convert.ToString(r["Kost2"]);
                    tbKto.Text = r["Konto"] == DBNull.Value ? string.Empty : Convert.ToString(r["Konto"]);
                    string mw = "19"; try { decimal m19 = r["Betrag19"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Betrag19"]); decimal m7 = r["Betrag7"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Betrag7"]); decimal m0 = r["Betrag0"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Betrag0"]); if (m7 == 1m && m19 == 0m && m0 == 0m) mw = "7"; else if (m0 == 1m && m19 == 0m && m7 == 0m) mw = "0"; } catch { }
                    cbMwst.SelectedIndex = cbMwst.FindStringExact(mw);
                    // FirmenID -> cbFirma
                    try
                    {
                        int fid=0; if(r.Table.Columns.Contains("FirmenID") && r["FirmenID"]!=DBNull.Value) fid=Convert.ToInt32(r["FirmenID"]);
                        if(cbFirma.DataSource is DataTable man){
                            bool set=false; for(int i=0;i<man.Rows.Count;i++){ if(Convert.ToInt32(man.Rows[i]["ManID"])==fid){ cbFirma.SelectedIndex=i; set=true; break; }}
                            if(!set && cbFirma.Items.Count>0) cbFirma.SelectedIndex=0;
                        }
                    }
                    catch { if(cbFirma.Items.Count>0) cbFirma.SelectedIndex=0; }
                    btnSave.Enabled = !string.IsNullOrWhiteSpace(tbName.Text);
                }
            };
            btnDelete.Click += async (s, e) =>
            {
                if (!(lst.SelectedItem is PresetListItem ciDel) || editBeleg == null) return;
                if (MessageBox.Show(dlg, "Vorlage wirklich löschen?", "Bestätigung", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    using (var db = new DatabaseHelperKassen()) await db.UpdateZahlungsVorlageAsync(editBeleg.Value, "Einzahlung", "Vorlage", string.Empty, null, null, null, "19", 0);
                }
                catch (Exception ex) { MessageBox.Show(dlg, "Löschen fehlgeschlagen: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                await loadListAsync(); clearFields();
            };
            tbName.TextChanged += (s, e) => btnSave.Enabled = !string.IsNullOrWhiteSpace(tbName.Text);
            btnSave.Click += async (s, e) =>
            {
                string typ = cbTyp.SelectedItem as string; string name = tbName.Text?.Trim(); if (string.IsNullOrWhiteSpace(name)) return; string mwst = cbMwst.SelectedItem as string; string txtVal = tbTxt.Text?.Trim(); string k1Val = string.IsNullOrWhiteSpace(tbK1.Text) ? null : tbK1.Text.Trim(); string k2Val = string.IsNullOrWhiteSpace(tbK2.Text) ? null : tbK2.Text.Trim(); string ktoVal = string.IsNullOrWhiteSpace(tbKto.Text) ? null : tbKto.Text.Trim(); int fid=0; try{ if(cbFirma.SelectedValue!=null) fid=Convert.ToInt32(cbFirma.SelectedValue);}catch{}
                try
                {
                    using (var db = new DatabaseHelperKassen())
                    {
                        if (editBeleg.HasValue)
                            await db.UpdateZahlungsVorlageAsync(editBeleg.Value, typ, name, txtVal, k1Val, k2Val, ktoVal, mwst, fid);
                        else
                            await db.InsertZahlungsVorlageAsync(typ, name, txtVal, k1Val, k2Val, ktoVal, mwst, fid);
                    }
                    dlg.Tag = name; dlg.DialogResult = DialogResult.OK; dlg.Close();
                }
                catch (Exception ex2) { MessageBox.Show(dlg, "Fehler: " + ex2.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            await loadMandantenAsync();
            await loadListAsync();
            dlg.Shown += (s, e) => { if (txtSearch.Text == searchPlaceholder) txtSearch.Select(0, 0); else txtSearch.Focus(); };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string lastName = dlg.Tag as string;
                _ = LoadAccountingPresetsAsync().ContinueWith(t => { try { if (InvokeRequired) BeginInvoke(new Action(() => SelectPresetByName(lastName))); else SelectPresetByName(lastName); } catch { } });
            }
            dlg.Dispose();
        }

        // Hilfsmethoden für Typ-Mapping (minimalinvasiv)
        private static string MapTypTextToCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            switch (text.Trim().ToLowerInvariant())
            {
                case "einzahlung": return "2";
                case "auszahlung": return "3";
                // ggf. weitere Typen später:
                case "anfangsbestand": return "1";
                case "schichtabrechnung": return "4";
                case "personalguthaben": return "5";
                default: return text; // falls bereits numerisch oder unbekannt
            }
        }
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
                default: return code; // schon Text oder unbekannt
            }
        }
    }
}