using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TaMi_Kassenclient
{
    public partial class AbrechnungBedingungenForm : Form
    {
        private const int HeaderHeight = 60;
        private static readonly Color Accent = Color.FromArgb(33,150,243);
        private static readonly Color Accent2 = Color.FromArgb(33,203,243);

        private Panel header; private Label lblTitle; private Button btnClose; private Panel content; private SplitContainer split; private ListView lvRegeln; private Button btnNeu, btnBearbeiten, btnDuplizieren, btnLoeschen, btnSpeichernAlle;
        private BindingList<AbrechnungsRegel> _regeln = new BindingList<AbrechnungsRegel>();

        public AbrechnungBedingungenForm() { BuildUi(); }

        protected override async void OnShown(EventArgs e) { base.OnShown(e); try { PositionCloseButton(); await LoadRulesAsync(); } catch { } }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Color.White); var rect = new Rectangle(0,0,ClientSize.Width,HeaderHeight); using(var br=new LinearGradientBrush(rect,Accent,Accent2,0f)) e.Graphics.FillRectangle(br,rect); }

        private void BuildUi()
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; var wa = Screen.PrimaryScreen.WorkingArea; int targetW = Math.Min(1600, Math.Max(1280, wa.Width - 60)); int targetH = Math.Min(900, Math.Max(760, wa.Height - 60)); ClientSize = new Size(targetW,targetH); BackColor = Color.White; Font = new Font("Segoe UI Variable",10F); DoubleBuffered = true;
            content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.White }; Controls.Add(content);
            header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = Color.Transparent };
            lblTitle = new Label { Text = "Abrechnungsbedingungen", Left = 20, Top = 0, Width = 600, Height = HeaderHeight, ForeColor = Color.White, Font = new Font("Segoe UI Variable",16F,FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            btnClose = new Button { Text = "?", Width = 44, Height = 44, Top = 8, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Transparent, TabStop = false };
            btnClose.FlatAppearance.BorderSize=0; btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(255,80,80); btnClose.Click += (s,e)=>Close();
            header.SizeChanged += (s,e)=> PositionCloseButton();
            header.Controls.Add(lblTitle); header.Controls.Add(btnClose); Controls.Add(header);
            split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 460, Padding = new Padding(0) }; content.Controls.Add(split); try { split.Panel2Collapsed = true; split.IsSplitterFixed = true; split.SplitterWidth = 1; } catch { }
            split.Panel1.Padding = new Padding(0,0,0,12);
            lvRegeln = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false }; lvRegeln.Columns.Add("Name",220); lvRegeln.Columns.Add("Bedingung",520); lvRegeln.Columns.Add("Fallback",90); lvRegeln.Columns.Add("Ergebnis",300); lvRegeln.Resize += (s,e)=>AdjustListColumns(); split.Panel1.Controls.Add(lvRegeln);
            var pnlBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8)};
            btnNeu = MakeBtn("Neu", Accent); btnBearbeiten = MakeBtn("Bearbeiten", Color.FromArgb(0,172,193)); btnDuplizieren = MakeBtn("Duplizieren", Color.FromArgb(3,155,229)); btnLoeschen = MakeBtn("Löschen", Color.IndianRed); btnSpeichernAlle = MakeBtn("Speichern", Color.FromArgb(76,175,80));
            btnNeu.Click += (s,e)=> NewRuleViaPopup(); btnBearbeiten.Click += (s,e)=> EditSelectedViaPopup(); btnDuplizieren.Click += (s,e)=> DuplicateSelectedViaPopup(); btnLoeschen.Click += (s,e)=> DeleteSelected(); btnSpeichernAlle.Click += (s,e)=> SaveAll();
            pnlBtns.Controls.AddRange(new Control[]{btnNeu, btnBearbeiten, btnDuplizieren, btnLoeschen, btnSpeichernAlle}); split.Panel1.Controls.Add(pnlBtns); AdjustListColumns();
        }
        private void PositionCloseButton()
        {
            try
            {
                if (btnClose == null || header == null) return;
                btnClose.Left = Math.Max(8, header.ClientSize.Width - btnClose.Width - 8);
                btnClose.BringToFront();
            }
            catch { }
        }
        private Button MakeBtn(string txt, Color c){ return new Button{ Text=txt, Width=110, Height=34, BackColor=c, ForeColor=Color.White, FlatStyle=FlatStyle.Flat, FlatAppearance={ BorderSize=0 } }; }

        private void RefreshList()
        {
            lvRegeln.Items.Clear();
            foreach(var r in _regeln){ var item = new ListViewItem(r.Name ?? "(ohne Namen)"); item.SubItems.Add(r.GetReadableCondition()); item.SubItems.Add(r.IsDefault?"Ja":"Nein"); var parts=new List<string>(); if(r.ResultKost1.HasValue) parts.Add($"Kost1={r.ResultKost1}"); if(r.ResultKost2.HasValue) parts.Add($"Kost2={r.ResultKost2}"); if(r.ResultKonto.HasValue) parts.Add($"Konto={r.ResultKonto}"); if(!string.IsNullOrWhiteSpace(r.ResultBuchungstext)) parts.Add($"Text=\"{r.ResultBuchungstext}\""); item.SubItems.Add(parts.Count==0?"(kein Ergebnis)":string.Join(", ",parts)); item.Tag=r; lvRegeln.Items.Add(item);} AdjustListColumns(); }
        private void AdjustListColumns(){ try { if(lvRegeln.Columns.Count==0) return; int w = lvRegeln.ClientSize.Width - SystemInformation.VerticalScrollBarWidth; if (w<=0) return; if(lvRegeln.Columns.Count>=4){ int nameW=Math.Max(180,(int)(w*0.22)); int fallbackW=Math.Max(80,(int)(w*0.10)); int ergW=Math.Max(180,(int)(w*0.25)); int bedW=Math.Max(300,w-(nameW+fallbackW+ergW)-8); lvRegeln.Columns[0].Width=nameW; lvRegeln.Columns[1].Width=bedW; lvRegeln.Columns[2].Width=fallbackW; lvRegeln.Columns[3].Width=ergW; } } catch { } }
        private AbrechnungsRegel GetSelectedRule(){ if(lvRegeln.SelectedItems.Count==0) return null; return lvRegeln.SelectedItems[0].Tag as AbrechnungsRegel; }

        private void NewRuleViaPopup(){ using(var editor=new AbrechnungsBedingungEditorForm()){ if(editor.ShowDialog(this)==DialogResult.OK){ var r=MapFromEditor(editor); r.Name= string.IsNullOrWhiteSpace(editor.RuleName)?$"Regel {DateTime.Now:HHmmss}":editor.RuleName.Trim(); _regeln.Add(r); RefreshList(); } } }
        private void EditSelectedViaPopup(){ var r=GetSelectedRule(); if(r==null) return; using(var editor=new AbrechnungsBedingungEditorForm()){ try{ editor.LoadFromRule(r);}catch{} if(editor.ShowDialog(this)==DialogResult.OK){ var upd=MapFromEditor(editor); upd.Name = string.IsNullOrWhiteSpace(editor.RuleName)? r.Name : editor.RuleName.Trim(); r.Clauses = upd.Clauses; r.ResultKost1=upd.ResultKost1; r.ResultKost2=upd.ResultKost2; r.ResultKonto=upd.ResultKonto; r.ResultBuchungstext=upd.ResultBuchungstext; r.Name=upd.Name; r.IsDefault=upd.IsDefault; r.Priority=upd.Priority; RefreshList(); } } }
        private void DuplicateSelectedViaPopup(){ var r=GetSelectedRule(); if(r==null) return; using(var editor=new AbrechnungsBedingungEditorForm()){ try{editor.LoadFromRule(r);}catch{} if(editor.ShowDialog(this)==DialogResult.OK){ var copy=MapFromEditor(editor); copy.Name= string.IsNullOrWhiteSpace(editor.RuleName)? (r.Name??"Regel")+" (Kopie)" : editor.RuleName.Trim(); copy.Id=0; _regeln.Add(copy); RefreshList(); } } }
        private async void DeleteSelected(){ var r=GetSelectedRule(); if(r==null) return; if(MessageBox.Show(this,$"Regel '{r.Name}' löschen?","Bestätigen",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return; try { await SoftDeleteRuleBySqlAsync(r.Id);} catch { } await LoadRulesAsync(); }
        private async Task<int> SoftDeleteRuleBySqlAsync(int ruleId){ if(ruleId<=0) return 0; var cs=DatabaseHelperKassen.GetConnectionString(); using(var conn=new SqlConnection(cs)){ await conn.OpenAsync(); string tblRules= await ResolveQualifiedTableAsync(conn,"TAbrechnungsBedingungen") ?? "[dbo].[TAbrechnungsBedingungen]"; string tblClauses= await ResolveQualifiedTableAsync(conn,"TAbrechnungsBedingungenClause") ?? "[dbo].[TAbrechnungsBedingungenClause]"; using(var tx=conn.BeginTransaction()) using(var cmd=conn.CreateCommand()){ cmd.Transaction=tx; cmd.CommandText=$"UPDATE {tblRules} SET IsActive=0, ModifiedAt=SYSUTCDATETIME() WHERE Id=@Id"; cmd.Parameters.AddWithValue("@Id", ruleId); int affected= await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear(); try { cmd.CommandText=$"DELETE FROM {tblClauses} WHERE RuleId=@R"; cmd.Parameters.AddWithValue("@R", ruleId); await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear(); } catch { cmd.Parameters.Clear(); } tx.Commit(); return affected; } } }
        private static async Task<string> ResolveQualifiedTableAsync(SqlConnection conn,string tableName){ using(var cmd=conn.CreateCommand()){ cmd.CommandText=@"SELECT '[' + s.name + '].[' + t.name + ']' FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.name=@n"; cmd.Parameters.AddWithValue("@n", tableName); var o= await cmd.ExecuteScalarAsync(); return (o==null||o==DBNull.Value)? null : Convert.ToString(o);} }
        private async void SaveAll(){ try { using(var db=new DatabaseHelperKassen()){ foreach(var r in _regeln){ r.RawConditionsJson=null; r.RawResultsJson=null; r.Id = await db.SaveAbrechnungsRegelAsync(r);} } MessageBox.Show(this,"Regeln gespeichert.","Info",MessageBoxButtons.OK,MessageBoxIcon.Information);} catch(Exception ex){ MessageBox.Show(this,"Fehler beim Speichern: "+ex.Message,"Fehler",MessageBoxButtons.OK,MessageBoxIcon.Error);} }

        private async Task LoadRulesAsync()
        {
            using (var db = new DatabaseHelperKassen())
            {
                var dtRules = await db.LoadAbrechnungsRegelnAsync();
                var dtClauses = await db.LoadAbrechnungsClausesAsync();
                var rules = new List<AbrechnungsRegel>();
                foreach (DataRow row in dtRules.Rows)
                {
                    var r = new AbrechnungsRegel();
                    try { r.Id = row["Id"] != DBNull.Value ? Convert.ToInt32(row["Id"]) : 0; } catch { }
                    try { r.Name = row["Name"] as string; } catch { }
                    try { r.JoinKind = row["JoinKind"] as string; } catch { }
                    try { r.IsDefault = row["IsDefault"] != DBNull.Value && Convert.ToBoolean(row["IsDefault"]); } catch { r.IsDefault = false; }
                    try { r.Priority = row["Priority"] != DBNull.Value ? Convert.ToInt32(row["Priority"]) : 100; } catch { r.Priority = 100; }
                    try { r.ResultKost1 = row["ResultKost1"] != DBNull.Value ? (int?)Convert.ToInt32(row["ResultKost1"]) : null; } catch { }
                    try { r.ResultKost2 = row["ResultKost2"] != DBNull.Value ? (int?)Convert.ToInt32(row["ResultKost2"]) : null; } catch { }
                    try { r.ResultKonto = row["ResultKonto"] != DBNull.Value ? (int?)Convert.ToInt32(row["ResultKonto"]) : null; } catch { }
                    try { r.ResultBuchungstext = row["ResultText"] as string; } catch { }
                    try { r.RawConditionsJson = row["RawConditions"] as string; } catch { }
                    try { r.RawResultsJson = row["RawResults"] as string; } catch { }
                    try { r.IsActive = row.Table.Columns.Contains("IsActive") && row["IsActive"] != DBNull.Value ? Convert.ToBoolean(row["IsActive"]) : true; } catch { r.IsActive = true; }
                    rules.Add(r);
                }
                var byRule = dtClauses.AsEnumerable().GroupBy(dr => dr.Field<int>("RuleId")).ToDictionary(g => g.Key,g=>g.ToList());
                foreach(var r in rules){ if(r==null) continue; if(byRule.TryGetValue(r.Id,out var list)){ r.Clauses = new List<AbrechnungsClause>(); foreach(var dr in list){ var c=new AbrechnungsClause(); try { c.Id = dr.Field<int>("Id"); } catch { } try { c.RuleId = dr.Field<int>("RuleId"); } catch { c.RuleId = r.Id; } try { c.GroupId = dr["GroupId"]==DBNull.Value?0:Convert.ToInt32(dr["GroupId"]); } catch { } try { c.Field = dr["Field"] as string; } catch { } try { c.Operator = dr["Operator"] as string; } catch { } try { c.Value = dr["Value"] as string; } catch { } r.Clauses.Add(c);} } }
                _regeln.Clear(); foreach(var r in rules) _regeln.Add(r); RefreshList();
            }
        }

        private AbrechnungsRegel MapFromEditor(AbrechnungsBedingungEditorForm editor)
        {
            var rule = new AbrechnungsRegel { JoinKind = editor.Verknuepfung, IsDefault = editor.IsDefault, Priority = editor.Priority, Clauses = new List<AbrechnungsClause>() };
            int group = 0; foreach(var c in editor.Bedingungen){ string fld=(c.Feld??"").Trim(); string op=(c.Operator??"=").Trim(); string val=(c.Wert??"").Trim(); if(string.Equals(fld,"MwSt",StringComparison.OrdinalIgnoreCase)){ if(val=="19") rule.Clauses.Add(new AbrechnungsClause{GroupId=group,Field="Betrag19",Operator=(op=="!="||op=="<>")?"=":">",Value="0"}); else if(val=="7") rule.Clauses.Add(new AbrechnungsClause{GroupId=group,Field="Betrag7",Operator=(op=="!="||op=="<>")?"=":">",Value="0"}); else if(val=="0") rule.Clauses.Add(new AbrechnungsClause{GroupId=group,Field="Betrag0",Operator=(op=="!="||op=="<>")?"=":">",Value="0"}); continue; } if(string.IsNullOrWhiteSpace(fld)) continue; if(string.Equals(op,"!=",StringComparison.OrdinalIgnoreCase)) op="<>"; if(string.Equals(fld,"FhzId",StringComparison.OrdinalIgnoreCase) && string.Equals(op,"IN",StringComparison.OrdinalIgnoreCase)){ var list=string.Join(";",(val??string.Empty).Split(new[]{';',',',' '},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim())); val=list; } rule.Clauses.Add(new AbrechnungsClause{GroupId=group,Field=fld,Operator=op,Value=val}); }
            foreach(var res in editor.Ergebnisse){ switch((res.Feld??string.Empty).Trim()){ case "Kost1": if(int.TryParse(res.Wert,out var k1)) rule.ResultKost1=k1; break; case "Kost2": if(int.TryParse(res.Wert,out var k2)) rule.ResultKost2=k2; break; case "Konto": if(int.TryParse(res.Wert,out var kt)) rule.ResultKonto=kt; break; case "Buchungstext": rule.ResultBuchungstext = res.Wert; break; } }
            return rule;
        }
    }
}
