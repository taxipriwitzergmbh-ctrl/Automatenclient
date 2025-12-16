using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;

namespace TaMi_Kassenclient
{
    public sealed class DatabaseHelperKassen : IDisposable
    {
        private SqlConnection _connection;

        private string  _tblKassenbuch;     // z. B. [dbo].[TKassenbuch]
        private string  _tblMandanten;      // z. B. [dbo].[TMandanten]
        private bool?   _belegIstIdentity;  // true, falls Belegnummer Identity ist
        private string  _tblDevices;        // NEU: [dbo].[TKassenbuchDevice]
        private string  _tblVorlagen;       // NEU: [dbo].[TKassenbuchVorlagen]

        // Persistenz der Abrechnungsbedingungen
        private string  _tblAbrechnungsRegeln;
        private string  _tblAbrechnungsClauses; // NEU


        // Weitere Tabellen
        private string _tblPersonal;     // [dbo].[TPersonal]
        private string _tblZahlungen;    // [dbo].[TKassenbuchZahlungen]
        private string _tblFahrzeuge;    // [dbo].[TFahrzeuge] (NEU)


        public DatabaseHelperKassen()
        {
            //_connection = new SqlConnection(GetConnectionString());
        }

        public static string GetConnectionString()
        {
            // Verbindungszeichenfolge kommt vollständig aus dem externen MainTaMiClient.
            return Program.MainTaMiClient.DatabaseConnectionStr;
        }

        private async Task EnsureOpenAsync()
        {
            if (_connection == null)
                _connection = Program.MainTaMiClient.OpenTaMiDB(true);

            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            if (_tblKassenbuch == null)
                await ResolveObjectNamesAsync();
        }

        private async Task ResolveObjectNamesAsync()
        {
            _tblKassenbuch = await ResolveQualifiedTableAsync("TKassenbuch") ?? "[dbo].[TKassenbuch]";
            _tblMandanten = await ResolveQualifiedTableAsync("TMandanten") ?? "[dbo].[TMandanten]";
            _tblDevices = await ResolveQualifiedTableAsync("TKassenbuchDevice") ?? "[dbo].[TKassenbuchDevice]"; // neu
            _tblAbrechnungsRegeln = await ResolveQualifiedTableAsync("TAbrechnungsBedingungen") ?? "[dbo].[TAbrechnungsBedingungen]";
            _tblAbrechnungsClauses = await ResolveQualifiedTableAsync("TAbrechnungsBedingungenClause") ?? "[dbo].[TAbrechnungsBedingungenClause]";
            _tblPersonal = await ResolveQualifiedTableAsync("TPersonal") ?? "[dbo].[TPersonal]";
            _tblZahlungen = await ResolveQualifiedTableAsync("TKassenbuchZahlungen") ?? "[dbo].[TKassenbuchZahlungen]";
            _tblFahrzeuge = await ResolveQualifiedTableAsync("TFahrzeuge") ?? "[dbo].[TFahrzeuge]"; // NEU
            _tblVorlagen  = await ResolveQualifiedTableAsync("TKassenbuchVorlagen") ?? "[dbo].[TKassenbuchVorlagen]"; // NEU

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT c.is_identity
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'Belegnummer'
WHERE t.name = 'TKassenbuch'";
                var o = await cmd.ExecuteScalarAsync();
                _belegIstIdentity = (o != null && o != DBNull.Value) && Convert.ToBoolean(o);
            }
        }

        private async Task<string> ResolveQualifiedTableAsync(string tableName)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
SELECT QUOTENAME(s.name) + '.' + QUOTENAME(t.name)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name = @name";

                cmd.Parameters.AddWithValue("@name", tableName);

                var o = await cmd.ExecuteScalarAsync();

                return (o == null || o == DBNull.Value) ? null : Convert.ToString(o);
            }
        }

        private static string ExtractTableName(string qualified)
        {
            if (string.IsNullOrEmpty(qualified)) return null;
            // Expect format [schema].[name]
            int lastOpen = qualified.LastIndexOf('[');
            int lastClose = qualified.LastIndexOf(']');
            if (lastOpen >= 0 && lastClose > lastOpen)
                return qualified.Substring(lastOpen + 1, lastClose - lastOpen - 1);
            var parts = qualified.Split('.');
            return parts.Length > 0 ? parts[parts.Length - 1].Trim('[', ']') : qualified;
        }

        private async Task<HashSet<string>> GetWritableColumnsAsync(SqlTransaction tx)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $"SELECT TOP 0 * FROM {_tblKassenbuch}";
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var schema = rdr.GetSchemaTable();
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (DataRow r in schema.Rows)
                    {
                        string name = Convert.ToString(r["ColumnName"]);
                        bool isReadOnly = r.Table.Columns.Contains("IsReadOnly") && r["IsReadOnly"] is bool bro && bro;
                        bool isHidden = r.Table.Columns.Contains("IsHidden") && r["IsHidden"] is bool bh && bh;
                        bool isRowVersion = r.Table.Columns.Contains("IsRowVersion") && r["IsRowVersion"] is bool brv && brv;
                        if (!isReadOnly && !isHidden && !isRowVersion) set.Add(name);
                    }
                    set.Remove("BetragGesamt");
                    set.Remove("Betrag");
                    return set;
                }
            }
        }

        // --- Personal-/Zahlungs-Hilfen ---
        public async Task<DataTable> GetActivePersonalAsync()
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT PID, (Name + ' ' + Vorname) AS Name, Vorname FROM {_tblPersonal} WITH (NOLOCK) WHERE (Gesperrt = 0 OR Gesperrt IS NULL) ORDER BY Name ASC, Vorname ASC";
                using (var rdr = await cmd.ExecuteReaderAsync()) { var dt = new DataTable(); dt.Load(rdr); return dt; }
            }
        }

        public async Task<DataTable> GetMandantenAsync()
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                // Nur Mandanten deren Flags-Bit 512 NICHT gesetzt ist (Flags & 512 = 0)
                // Falls die Spalte Flags nicht existiert, fallback ohne Filter (TRY/CATCH in SQL)
                cmd.CommandText = $@"
BEGIN TRY
    SELECT ManID, ManName FROM {_tblMandanten} WITH (NOLOCK) WHERE (ISNULL(Flags,0) & 512) = 0 ORDER BY ManName ASC;
END TRY
BEGIN CATCH
    SELECT ManID, ManName FROM {_tblMandanten} WITH (NOLOCK) ORDER BY ManName ASC;
END CATCH";
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable();
                    dt.Load(rdr);
                    return dt;
                }
            }
        }

        public async Task<PersonalInfo> GetPersonalInfoAsync(int pid)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT PID, Name, Vorname, NFCTagUID, Fahrercode FROM {_tblPersonal} WITH (NOLOCK) WHERE PID = @PID";
                cmd.Parameters.AddWithValue("@PID", pid);
                using (var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                {
                    if (!rdr.Read()) return null;
                    return new PersonalInfo
                    {
                        PID = rdr.GetInt32(rdr.GetOrdinal("PID")),
                        Name = rdr.GetString(rdr.GetOrdinal("Name")),
                        Vorname = rdr.GetString(rdr.GetOrdinal("Vorname")),
                        NFCTagUID = rdr.IsDBNull(rdr.GetOrdinal("NFCTagUID")) ? null : rdr["NFCTagUID"].ToString(),
                        Fahrercode = rdr.IsDBNull(rdr.GetOrdinal("Fahrercode")) ? null : rdr["Fahrercode"].ToString()
                    };
                }
            }
        }

        public async Task<DataTable> GetOpenShiftsListAsync(int persId)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"
SELECT 
    s.SchichtId,
    s.PersId,
    s.PersName,
    s.FhzId,
    f.Kennzeichen,
    s.StartZeit,
    ISNULL(s.EinnahmenBar1,0) - ISNULL(s.EinzahlungFahrer1,0) AS Betrag19,
    ISNULL(s.EinnahmenBar2,0) - ISNULL(s.EinzahlungFahrer2,0) AS Betrag7,
    ISNULL(s.EinnahmenBar3,0) - ISNULL(s.EinzahlungFahrer3,0) AS Betrag0,
    CAST(
        (ISNULL(s.EinnahmenBar1,0) - ISNULL(s.EinzahlungFahrer1,0)) +
        (ISNULL(s.EinnahmenBar2,0) - ISNULL(s.EinzahlungFahrer2,0)) +
        (ISNULL(s.EinnahmenBar3,0) - ISNULL(s.EinzahlungFahrer3,0))
        AS money) AS OffenerBetrag
FROM TSchichten s WITH (NOLOCK)
LEFT JOIN {_tblFahrzeuge} f WITH (NOLOCK) ON f.FID = s.FhzId
WHERE (s.Flags & 1) = 0 AND (s.Flags & 4) = 0 AND s.PersId = @PersId
ORDER BY s.StartZeit DESC;";

                cmd.Parameters.AddWithValue("@PersId", persId);
                using (var rdr = await cmd.ExecuteReaderAsync()) 
                { 
                    var dt = new DataTable(); 
                    dt.Load(rdr); 
                    return dt; 
                }
            }
        }

        public async Task<DataTable> GetOffeneAuszahlungenAsync(int persId)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT * FROM {_tblZahlungen} WHERE PersId = @pid AND (Verbucht = 0 OR Verbucht IS NULL)";
                cmd.Parameters.AddWithValue("@pid", persId);
                using (var rdr = await cmd.ExecuteReaderAsync()) { var dt = new DataTable(); dt.Load(rdr); return dt; }
            }
        }

        public async Task<int> InsertOffeneZahlungAsync(int persId, string typ, string buchungstext, decimal b19, decimal b7, decimal b0, int? k1 = null, int? k2 = null, int? kto = null, int firmenId = 0)
        {
            await EnsureOpenAsync();

            // Optional: Spalte 'UserAnlage' (int) verwenden, falls vorhanden
            bool hasUserAnlage = false;
            try
            {
                using (var chk = _connection.CreateCommand())
                {
                    // _tblZahlungen liegt in Form [schema].[name] vor -> Klammern entfernen für OBJECT_ID
                    string obj = (_tblZahlungen ?? "[dbo].[TKassenbuchZahlungen]").Replace("[", string.Empty).Replace("]", string.Empty);
                    chk.CommandText = "SELECT 1 FROM sys.columns WHERE Name='UserAnlage' AND object_id = OBJECT_ID(@obj)";
                    chk.Parameters.AddWithValue("@obj", obj);
                    var exists = await chk.ExecuteScalarAsync();
                    hasUserAnlage = exists != null && exists != DBNull.Value;
                }
            }
            catch { hasUserAnlage = false; }

            using (var cmd = _connection.CreateCommand())
            {
                decimal safeB19 = b19;
                decimal safeB7 = b7;
                decimal safeB0 = b0;
                decimal sum = safeB19 + safeB7 + safeB0;
                int safeK1 = k1 ?? 0;
                int safeK2 = k2 ?? 0;
                int safeKto = kto ?? 0;
                string safeTyp = typ ?? string.Empty; // kann bereits numerischer Code sein ("2"/"3")
                string safeTxt = buchungstext ?? string.Empty;

                if (hasUserAnlage)
                {
                    cmd.CommandText = $@"INSERT INTO {_tblZahlungen}
(PersId, Typ, Buchungstext, Betrag19, Betrag7, Betrag0, BetragGesamt, Kost1, Kost2, Konto, FirmenID, DeviceID, Verbucht, ErfasstAm, UserAnlage)
OUTPUT INSERTED.Belegnummer
VALUES(@pid,@typ,@txt,@b19,@b7,@b0,@bg,@k1,@k2,@kto,@fid,0,0,SYSDATETIME(),@ua);";
                }
                else
                {
                    cmd.CommandText = $@"INSERT INTO {_tblZahlungen}
(PersId, Typ, Buchungstext, Betrag19, Betrag7, Betrag0, BetragGesamt, Kost1, Kost2, Konto, FirmenID, DeviceID, Verbucht, ErfasstAm)
OUTPUT INSERTED.Belegnummer
VALUES(@pid,@typ,@txt,@b19,@b7,@b0,@bg,@k1,@k2,@kto,@fid,0,0,SYSDATETIME());";
                }

                cmd.Parameters.AddWithValue("@pid", persId);
                // Typ als tinyint ablegen
                cmd.Parameters.Add("@typ", SqlDbType.TinyInt).Value = MapTypStringToCode(safeTyp);
                cmd.Parameters.AddWithValue("@txt", safeTxt);
                cmd.Parameters.AddWithValue("@b19", safeB19);
                cmd.Parameters.AddWithValue("@b7", safeB7);
                cmd.Parameters.AddWithValue("@b0", safeB0);
                cmd.Parameters.AddWithValue("@bg", sum);
                cmd.Parameters.AddWithValue("@k1", safeK1);
                cmd.Parameters.AddWithValue("@k2", safeK2);
                cmd.Parameters.AddWithValue("@kto", safeKto);
                cmd.Parameters.AddWithValue("@fid", firmenId);
                if (hasUserAnlage)
                {
                    int ua = 0; try { ua = AppSession.CurrentUser?.PID ?? 0; } catch { ua = 0; }
                    cmd.Parameters.AddWithValue("@ua", ua);
                }

                var o = await cmd.ExecuteScalarAsync();
                if (o == null || o == DBNull.Value) return 0;
                int id; if (o is int) id = (int)o; else if (o is decimal) id = Convert.ToInt32((decimal)o); else int.TryParse(Convert.ToString(o), out id);
                return id;
            }
        }

        // Vorlagen (Templates) – Erkennung: eigene Tabelle TKassenbuchVorlagen (NEU)
        public async Task<int> InsertZahlungsVorlageAsync(string typ, string vorlagenName, string buchungstext, string kost1, string kost2, string konto, string mwst, int firmenId = 0)
        {
            await EnsureOpenAsync();
            byte typCode = MapTypStringToCode(typ);
            decimal b19 = 0m, b7 = 0m, b0 = 0m;
            switch ((mwst ?? string.Empty).Trim())
            {
                case "19": b19 = 1m; break;
                case "7":  b7  = 1m; break;
                case "0":  b0  = 1m; break;
            }
            string vName = (vorlagenName ?? string.Empty).Trim();
            string bText = (buchungstext ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(vName)) vName = bText;
            if (string.IsNullOrWhiteSpace(bText)) bText = vName;
            if (string.IsNullOrEmpty(vName)) vName = "(leer)";
            if (string.IsNullOrEmpty(bText)) bText = vName;
            string k1 = string.IsNullOrWhiteSpace(kost1) ? "0" : kost1.Trim();
            string k2 = string.IsNullOrWhiteSpace(kost2) ? "0" : kost2.Trim();
            string kto = string.IsNullOrWhiteSpace(konto) ? "0" : konto.Trim();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"INSERT INTO {_tblVorlagen}
(Typ,VorlagenName,Buchungstext,Betrag19,Betrag7,Betrag0,FirmenID,Kost1,Kost2,Konto)
OUTPUT INSERTED.ID
VALUES(@Typ,@VName,@BText,@B19,@B7,@B0,@FID,@K1,@K2,@Kto);";
                cmd.Parameters.AddWithValue("@Typ", typCode);
                cmd.Parameters.AddWithValue("@VName", vName);
                cmd.Parameters.AddWithValue("@BText", bText);
                cmd.Parameters.AddWithValue("@B19", b19);
                cmd.Parameters.AddWithValue("@B7", b7);
                cmd.Parameters.AddWithValue("@B0", b0);
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@K1", k1);
                cmd.Parameters.AddWithValue("@K2", k2);
                cmd.Parameters.AddWithValue("@Kto", kto);
                var o = await cmd.ExecuteScalarAsync();
                int id = 0; if (o is int) id = (int)o; else if (o is decimal) id = Convert.ToInt32((decimal)o); else int.TryParse(Convert.ToString(o), out id);
                return id;
            }
        }

        public async Task<int> UpdateZahlungsVorlageAsync(int belegnummer, string typ, string vorlagenName, string buchungstext, string kost1, string kost2, string konto, string mwst, int firmenId = 0)
        {
            await EnsureOpenAsync();
            byte typCode = MapTypStringToCode(typ);
            decimal b19 = 0m, b7 = 0m, b0 = 0m;
            switch ((mwst ?? string.Empty).Trim())
            {
                case "19": b19 = 1m; break; case "7": b7 = 1m; break; case "0": b0 = 1m; break;
            }
            string vName = (vorlagenName ?? string.Empty).Trim();
            string bText = (buchungstext ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(vName)) vName = bText;
            if (string.IsNullOrWhiteSpace(bText)) bText = vName;
            if (string.IsNullOrEmpty(vName)) vName = "(leer)";
            if (string.IsNullOrEmpty(bText)) bText = vName;
            string k1 = string.IsNullOrWhiteSpace(kost1) ? "0" : kost1.Trim();
            string k2 = string.IsNullOrWhiteSpace(kost2) ? "0" : kost2.Trim();
            string kto = string.IsNullOrWhiteSpace(konto) ? "0" : konto.Trim();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"UPDATE {_tblVorlagen}
SET Typ=@Typ, VorlagenName=@VName, Buchungstext=@BText, Betrag19=@B19, Betrag7=@B7, Betrag0=@B0,
    FirmenID=@FID, Kost1=@K1, Kost2=@K2, Konto=@Kto
WHERE ID=@ID";
                cmd.Parameters.AddWithValue("@Typ", typCode);
                cmd.Parameters.AddWithValue("@VName", vName);
                cmd.Parameters.AddWithValue("@BText", bText);
                cmd.Parameters.AddWithValue("@B19", b19);
                cmd.Parameters.AddWithValue("@B7", b7);
                cmd.Parameters.AddWithValue("@B0", b0);
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@K1", k1);
                cmd.Parameters.AddWithValue("@K2", k2);
                cmd.Parameters.AddWithValue("@Kto", kto);
                cmd.Parameters.AddWithValue("@ID", belegnummer);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<DataTable> LoadZahlungsVorlagenAsync()
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT 
    v.ID AS Belegnummer,
    CASE v.Typ WHEN 2 THEN 'Einzahlung' WHEN 3 THEN 'Auszahlung' ELSE CONVERT(varchar(3),v.Typ) END AS Typ,
    v.VorlagenName,
    v.Buchungstext,
    v.Kost1,
    v.Kost2,
    v.Konto,
    v.Betrag19,
    v.Betrag7,
    v.Betrag0,
    v.FirmenID
FROM {_tblVorlagen} v WITH (NOLOCK)
ORDER BY v.VorlagenName ASC, v.Typ ASC;";
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable(); dt.Load(rdr); return dt;
                }
            }
        }

        public async Task<int> SetFahrercodeAsync(int pid, string code)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"UPDATE {_tblPersonal} SET Fahrercode=@C WHERE PID=@PID";
                cmd.Parameters.AddWithValue("@C", (object)code ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PID", pid);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> SetNfcAsync(int pid, string nfc)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"UPDATE {_tblPersonal} SET NFCTagUID=@N WHERE PID=@PID";
                cmd.Parameters.AddWithValue("@N", (object)nfc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PID", pid);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<decimal> GetLastPersonalGuthabenSaldoAsync(int persId)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT TOP 1 SaldoPersonalguthaben FROM {_tblKassenbuch} WITH (NOLOCK) WHERE PersId = @PersId AND Typ = '5' ORDER BY ErfasstAm DESC";
                cmd.Parameters.AddWithValue("@PersId", persId);
                var o = await cmd.ExecuteScalarAsync();
                return (o == null || o == DBNull.Value) ? 0m : Convert.ToDecimal(o);
            }
        }

        // NEU: Personalguthaben-Verlauf (jüngste zuerst)
        public async Task<DataTable> GetPersonalGuthabenVerlaufAsync(int persId)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"
SELECT TOP 500
    ErfasstAm,
    Typ,
    Buchungstext,
    CAST(ISNULL(Betrag19,0) + ISNULL(Betrag7,0) + ISNULL(Betrag0,0) AS money) AS Delta,
    SaldoPersonalguthaben AS Saldo,
    Betrag19,
    Betrag7,
    Betrag0,
    Belegnummer
FROM {_tblKassenbuch} WITH (NOLOCK)
WHERE PersId = @PersId AND Typ = '5' AND ISNULL(RevIsOld,0) = 0
ORDER BY ErfasstAm DESC, Belegnummer DESC;";
                cmd.Parameters.AddWithValue("@PersId", persId);
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable();
                    dt.Load(rdr);
                    return dt;
                }
            }
        }

        public async Task MarkZahlungAlsVerbuchtAsync(int belegnummer)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"UPDATE {_tblZahlungen} SET Verbucht = 1 WHERE Belegnummer = @bnr";
                cmd.Parameters.AddWithValue("@bnr", belegnummer);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Bearbeiten/Löschen offener Zahlungen
        public async Task<int> UpdateOffeneZahlungAsync(int belegnummer, string typ, string buchungstext, decimal b19, decimal b7, decimal b0, int? k1, int? k2, int? kto)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                decimal safeB19 = b19;
                decimal safeB7 = b7;
                decimal safeB0 = b0;
                decimal sum = safeB19 + safeB7 + safeB0;
                int safeK1 = k1 ?? 0;
                int safeK2 = k2 ?? 0;
                int safeKto = kto ?? 0;
                string safeTxt = buchungstext ?? string.Empty;

                cmd.CommandText = $@"UPDATE {_tblZahlungen}
SET Typ=@typ, Buchungstext=@txt, Betrag19=@b19, Betrag7=@b7, Betrag0=@b0, BetragGesamt=@bg, Kost1=@k1, Kost2=@k2, Konto=@kto
WHERE Belegnummer=@bnr AND (Verbucht=0 OR Verbucht IS NULL)";
                cmd.Parameters.Add("@typ", SqlDbType.TinyInt).Value = MapTypStringToCode(typ);
                cmd.Parameters.AddWithValue("@txt", safeTxt);
                cmd.Parameters.AddWithValue("@b19", safeB19);
                cmd.Parameters.AddWithValue("@b7", safeB7);
                cmd.Parameters.AddWithValue("@b0", safeB0);
                cmd.Parameters.AddWithValue("@bg", sum);
                cmd.Parameters.AddWithValue("@k1", safeK1);
                cmd.Parameters.AddWithValue("@k2", safeK2);
                cmd.Parameters.AddWithValue("@kto", safeKto);
                cmd.Parameters.AddWithValue("@bnr", belegnummer);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> DeleteOffeneZahlungAsync(int belegnummer)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"DELETE FROM {_tblZahlungen} WHERE Belegnummer=@bnr AND (Verbucht=0 OR Verbucht IS NULL)";
                cmd.Parameters.AddWithValue("@bnr", belegnummer);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        // NEU: Storniert eine offene Zahlung (setzt Verbucht=1 und AutomatenName='Storniert')
        public async Task<int> StorniereOffeneZahlungAsync(int belegnummer)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                // AutomatenName nicht mehr verändern, nur Verbucht setzen
                cmd.CommandText = $@"UPDATE {_tblZahlungen} SET Verbucht=1 WHERE Belegnummer=@bnr AND (Verbucht=0 OR Verbucht IS NULL)";
                cmd.Parameters.AddWithValue("@bnr", belegnummer);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        // ---------- Kassenfunktionen ----------
        public async Task<DataRow> GetEintragByBelegnummerAsync(string belegnummer)
        {
            await EnsureOpenAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT TOP 1 * FROM {_tblKassenbuch} WITH (NOLOCK) WHERE Belegnummer = @Belegnummer AND ISNULL(RevIsOld,0) = 0 ORDER BY ErfasstAm DESC";
                cmd.Parameters.AddWithValue("@Belegnummer", belegnummer ?? (object)DBNull.Value);
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable();
                    dt.Load(rdr);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
        }

        // Hilfsfunktion: Belegnummer über KassenBelegnummer auflösen (neueste, nicht alte Revision)
        public async Task<string> ResolveBelegnummerByKassenBelegAsync(string kassenBelegnummer)
        {
            await EnsureOpenAsync();
            if (string.IsNullOrWhiteSpace(kassenBelegnummer)) return null;
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT TOP 1 Belegnummer FROM {_tblKassenbuch} WITH (NOLOCK) WHERE KassenBelegnummer = @KBNR AND ISNULL(RevIsOld,0)=0 ORDER BY ErfasstAm DESC";
                cmd.Parameters.AddWithValue("@KBNR", kassenBelegnummer);
                var o = await cmd.ExecuteScalarAsync();
                if (o == null || o == DBNull.Value) return null;
                return Convert.ToString(o);
            }
        }

        // Direkte Bearbeitung des aktuellen Eintrags ohne Revision – NUR per eindeutiger Belegnummer
        public async Task<int> UpdateEntryDirectAsync(string belegnummer, string kassenBelegnummer,
            string buchungstext, int? kost1, int? kost2, int? konto,
            decimal betrag19, decimal betrag7, decimal betrag0)
        {
            await EnsureOpenAsync();

            if (string.IsNullOrWhiteSpace(belegnummer))
                throw new ArgumentException("Belegnummer ist erforderlich, um genau einen Eintrag zu aktualisieren.", nameof(belegnummer));

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"UPDATE {_tblKassenbuch}
SET Buchungstext=@txt, Betrag19=@b19, Betrag7=@b7, Betrag0=@b0,
    Kost1=@k1, Kost2=@k2, Konto=@kto
WHERE ISNULL(RevIsOld,0)=0 AND Belegnummer = @bnr";
                cmd.Parameters.AddWithValue("@txt", (object)(buchungstext ?? (object)DBNull.Value) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@b19", betrag19);
                cmd.Parameters.AddWithValue("@b7", betrag7);
                cmd.Parameters.AddWithValue("@b0", betrag0);
                cmd.Parameters.AddWithValue("@k1", (object)kost1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@k2", (object)kost2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@kto", (object)konto ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@bnr", belegnummer);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task InsertCloneWithSameBelegnummerAsync(SqlTransaction tx, DataRow src, IDictionary<string, object> overrides, int newRevNum)
        {
            var writable = await GetWritableColumnsAsync(tx);
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                var cols = new List<string>();
                var vals = new List<string>();
                int pi = 0;

                foreach (DataColumn c in src.Table.Columns)
                {
                    var name = c.ColumnName;
                    if (!writable.Contains(name)) continue;
                    if (name.Equals("RevIsOld", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Equals("Belegnummer", StringComparison.OrdinalIgnoreCase)) continue; // NIE explizit setzen -> Identity übernimmt

                    object value;
                    if (name.Equals("RevNum", StringComparison.OrdinalIgnoreCase)) value = newRevNum;
                    else if (name.Equals("RevDatum", StringComparison.OrdinalIgnoreCase)) value = DateTime.Now;
                    else if (overrides != null && overrides.ContainsKey(name)) value = overrides[name] ?? DBNull.Value;
                    else value = src[name] ?? DBNull.Value;

                    var pName = "@p" + (pi++).ToString();
                    cols.Add("[" + name + "]");
                    vals.Add(pName);
                    cmd.Parameters.AddWithValue(pName, value ?? DBNull.Value);
                }

                // Wichtig: Belegnummer NICHT setzen; DB generiert sie automatisch
                cmd.CommandText = $@"INSERT INTO {_tblKassenbuch} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // -------- Angepasste Methoden: AutomatenName -> DeviceID ---------

        public async Task<DataTable> GetKassenListeAsync(IEnumerable<string> automatenNamen)
        {
            await EnsureOpenAsync();
            var ids = new List<byte>();
            foreach (var n in automatenNamen ?? Array.Empty<string>())
            {
                if (byte.TryParse((n ?? string.Empty).Trim(), out var b)) ids.Add(b);
            }
            if (ids.Count == 0) return new DataTable();

            var paramNames = new List<string>();
            int i = 0; foreach (var _ in ids) paramNames.Add($"@D{i++}");
            string inClause = string.Join(", ", paramNames);

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"
;WITH raw AS (
    SELECT 
        CASE 
            WHEN LTRIM(RTRIM(k.FirmenId)) = '-1' THEN -1
            WHEN TRY_CONVERT(int, LTRIM(RTRIM(k.FirmenId))) IS NULL THEN NULL
            ELSE TRY_CONVERT(int, LTRIM(RTRIM(k.FirmenId)))
        END AS FirmenId,
        k.DeviceID,
        k.Kassenbestand,
        k.ErfasstAm
    FROM {_tblKassenbuch} k WITH (NOLOCK)
    WHERE k.DeviceID IN ({inClause})
), x AS (
    SELECT 
        FirmenId,
        DeviceID,
        Kassenbestand,
        ErfasstAm,
        ROW_NUMBER() OVER (PARTITION BY FirmenId, DeviceID ORDER BY ErfasstAm DESC) rn
    FROM raw
)
SELECT 
    x.FirmenId,
    x.DeviceID,
    COALESCE(NULLIF(LTRIM(RTRIM(d.AutomatenName)), ''), 'Gerät ' + CAST(x.DeviceID AS varchar(10))) AS AutomatenName,
    CASE 
        WHEN x.FirmenId = -1 THEN 'Personalguthaben'
        WHEN x.FirmenId BETWEEN 0 AND 255 THEN ISNULL(m.ManName, 'ID ' + CAST(x.FirmenId AS varchar(10)))
        WHEN x.FirmenId IS NULL THEN 'Unbekannt'
        ELSE 'ID ' + CAST(x.FirmenId AS varchar(10))
    END AS ManName,
    x.Kassenbestand,
    x.ErfasstAm
FROM x
LEFT JOIN {_tblMandanten} m ON (x.FirmenId BETWEEN 0 AND 255 AND m.ManID = x.FirmenId)
LEFT JOIN {_tblDevices} d ON d.DeviceID = x.DeviceID
WHERE x.rn = 1
ORDER BY ManName ASC, x.DeviceID ASC;";

                i = 0; foreach (var id in ids) cmd.Parameters.AddWithValue($"@D{i++}", id);

                using (var rdr = await cmd.ExecuteReaderAsync()) { var dt = new DataTable(); dt.Load(rdr); return dt; }
            }
        }

        public async Task<decimal> GetAnfangsbestandAsync(int firmenId, string automatenNameAlsDeviceId, DateTime tag)
        {
            await EnsureOpenAsync();
            byte deviceId = 0; byte.TryParse(automatenNameAlsDeviceId ?? string.Empty, out deviceId);
            var dayStart = new DateTime(tag.Year, tag.Month, tag.Day, 0, 0, 0);
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $"SELECT TOP 1 Kassenbestand FROM {_tblKassenbuch} WITH (NOLOCK) WHERE FirmenId = @FID AND DeviceID = @Dev AND ErfasstAm < @DayStart ORDER BY ErfasstAm DESC;";
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@Dev", deviceId);
                cmd.Parameters.AddWithValue("@DayStart", dayStart);
                var res = await cmd.ExecuteScalarAsync(); return (res == null || res == DBNull.Value) ? 0m : Convert.ToDecimal(res);
            }
        }

        public async Task<decimal> GetEndbestandAsync(int firmenId, string automatenNameAlsDeviceId, DateTime tag)
        {
            await EnsureOpenAsync();
            byte deviceId = 0; byte.TryParse(automatenNameAlsDeviceId ?? string.Empty, out deviceId);
            var dayEnd = new DateTime(tag.Year, tag.Month, tag.Day, 23, 59, 59);
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $"SELECT TOP 1 Kassenbestand FROM {_tblKassenbuch} WITH (NOLOCK) WHERE FirmenId = @FID AND DeviceID = @Dev AND ErfasstAm <= @DayEnd ORDER BY ErfasstAm DESC;";
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@Dev", deviceId);
                cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                var res = await cmd.ExecuteScalarAsync(); return (res == null || res == DBNull.Value) ? 0m : Convert.ToDecimal(res);
            }
        }

        public async Task<DataTable> GetKassenTagEintraegeAsync(int firmenId, string automatenNameAlsDeviceId, DateTime tag)
        {
            await EnsureOpenAsync();
            byte deviceId = 0; byte.TryParse(automatenNameAlsDeviceId ?? string.Empty, out deviceId);
            var dayStart = new DateTime(tag.Year, tag.Month, tag.Day, 0, 0, 0);
            var dayEnd = new DateTime(tag.Year, tag.Month, tag.Day, 23, 59, 59);
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"
SELECT 
    Belegnummer,
    KassenBelegnummer,
    SchichtId,
    FhzId,
    ErfasstAm,
    Typ,
    Buchungstext,
    CAST(ISNULL(Betrag19,0) + ISNULL(Betrag7,0) + ISNULL(Betrag0,0) AS money) AS Betrag,
    Betrag19,
    Betrag7,
    Betrag0,
    Kassenbestand,
    Kost1,
    Kost2,
    Konto,
    ISNULL(RevNum, 0) AS RevNum,
    ISNULL(RevIsOld, 0) AS RevIsOld,
    ISNULL(Festgeschrieben, 0) AS Festgeschrieben
FROM {_tblKassenbuch} WITH (NOLOCK)
WHERE FirmenId = @FID AND DeviceID = @Dev AND ErfasstAm >= @From AND ErfasstAm <= @To
ORDER BY ErfasstAm ASC, Belegnummer ASC;";
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@Dev", deviceId);
                cmd.Parameters.AddWithValue("@From", dayStart);
                cmd.Parameters.AddWithValue("@To", dayEnd);
                using (var rdr = await cmd.ExecuteReaderAsync()) { var dt = new DataTable(); dt.Load(rdr); return dt; }
            }
        }

        public async Task<int> LockDayAsync(int firmenId, string automatenNameAlsDeviceId, DateTime tag)
        {
            await EnsureOpenAsync();
            byte deviceId = 0; byte.TryParse(automatenNameAlsDeviceId ?? string.Empty, out deviceId);
            using (var cmd = _connection.CreateCommand())
            {
                var dayEnd = new DateTime(tag.Year, tag.Month, tag.Day, 23, 59, 59);
                cmd.CommandText = $"UPDATE {_tblKassenbuch} SET Festgeschrieben = 1 WHERE FirmenId = @FID AND DeviceID = @Dev AND ErfasstAm <= @DayEnd AND ISNULL(Festgeschrieben,0) = 0";
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@Dev", deviceId);
                cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> IsLockedUntilAsync(int firmenId, string automatenNameAlsDeviceId, DateTime tag)
        {
            await EnsureOpenAsync();
            byte deviceId = 0; byte.TryParse(automatenNameAlsDeviceId ?? string.Empty, out deviceId);
            using (var cmd = _connection.CreateCommand())
            {
                var dayEnd = new DateTime(tag.Year, tag.Month, tag.Day, 23, 59, 59);
                cmd.CommandText = $"SELECT COUNT(1) FROM {_tblKassenbuch} WHERE FirmenId = @FID AND DeviceID = @Dev AND ErfasstAm <= @DayEnd AND ISNULL(Festgeschrieben,0) = 0";
                cmd.Parameters.AddWithValue("@FID", firmenId);
                cmd.Parameters.AddWithValue("@Dev", deviceId);
                cmd.Parameters.AddWithValue("@DayEnd", dayEnd);
                var o = await cmd.ExecuteScalarAsync();
                var cnt = (o == null || o == DBNull.Value) ? 0 : Convert.ToInt32(o);
                return cnt == 0; // true, wenn bis inkl. Tag alles festgeschrieben ist
            }
        }

        // Revision / Split Methoden bleiben unverändert, da sie Belegnummer-basiert arbeiten
        public async Task ReviseSingleAsync(string belegnummer, string buchungstext, int? kost1, int? kost2, int? konto,
            decimal betrag19, decimal betrag7, decimal betrag0)
        {
            await EnsureOpenAsync();
            using (var tx = _connection.BeginTransaction())
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $@"SELECT TOP 1 * FROM {_tblKassenbuch} WITH (UPDLOCK, ROWLOCK) WHERE Belegnummer = @Belegnummer AND ISNULL(RevIsOld,0)=0 ORDER BY ErfasstAm DESC";
                cmd.Parameters.AddWithValue("@Belegnummer", belegnummer ?? (object)DBNull.Value);
                var src = new DataTable(); using (var rdr = await cmd.ExecuteReaderAsync()) { src.Load(rdr); }
                if (src.Rows.Count == 0) { tx.Rollback(); throw new InvalidOperationException("Eintrag nicht gefunden."); }
                var r = src.Rows[0];
                int currentRev = (r.Table.Columns.Contains("RevNum") && r["RevNum"] != DBNull.Value) ? Convert.ToInt32(r["RevNum"]) : 0;
                int newRev = currentRev + 1;

                // Kassenbestand für neue Revision berechnen: alter Vor-Kassenbestand + neuer Delta
                Func<object, decimal> toDec = (o) => (o == null || o == DBNull.Value) ? 0m : Convert.ToDecimal(o);
                decimal oldDelta = Math.Round(toDec(r["Betrag19"]) + toDec(r["Betrag7"]) + toDec(r["Betrag0"]), 2);
                decimal kbAfterOld = Math.Round(toDec(r["Kassenbestand"]), 2);
                decimal kbBefore = Math.Round(kbAfterOld - oldDelta, 2);
                decimal newDelta = Math.Round(betrag19 + betrag7 + betrag0, 2);
                decimal kbAfterNew = Math.Round(kbBefore + newDelta, 2);

                cmd.Parameters.Clear();
                cmd.CommandText = $@"UPDATE {_tblKassenbuch} SET RevIsOld = 1 WHERE Belegnummer = @Belegnummer AND ISNULL(RevIsOld,0)=0";
                cmd.Parameters.AddWithValue("@Belegnummer", belegnummer ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                var overrides = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Buchungstext", (object)(buchungstext ?? r["Buchungstext"]) ?? DBNull.Value },
                    { "Betrag19", betrag19 },
                    { "Betrag7", betrag7 },
                    { "Betrag0", betrag0 },
                    { "Kassenbestand", kbAfterNew }
                };
                // RevPersId (falls Spalte existiert) mit aktuell eingeloggter PID belegen
                try { overrides["RevPersId"] = (object)(AppSession.CurrentUser?.PID ?? 0); } catch { overrides["RevPersId"] = 0; }
                if (kost1.HasValue) overrides["Kost1"] = kost1.Value; if (kost2.HasValue) overrides["Kost2"] = kost2.Value; if (konto.HasValue) overrides["Konto"] = konto.Value;
                // KassenBelegnummer übernehmen, falls vorhanden
                if (r.Table.Columns.Contains("KassenBelegnummer")) overrides["KassenBelegnummer"] = r["KassenBelegnummer"];
                await InsertCloneWithSameBelegnummerAsync(tx, r, overrides, newRev);
                tx.Commit();
            }
        }

        public async Task SplitByVatAsync(string belegnummer, string buchungstext,
            decimal b19, int? k1_19, int? k2_19, int? kto_19,
            decimal b7, int? k1_7, int? k2_7, int? kto_7,
            decimal b0, int? k1_0, int? k2_0, int? kto_0)
        {
            await EnsureOpenAsync();
            using (var tx = _connection.BeginTransaction())
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $@"SELECT TOP 1 * FROM {_tblKassenbuch} WITH (UPDLOCK, ROWLOCK) WHERE Belegnummer = @Belegnummer AND ISNULL(RevIsOld,0) = 0 ORDER BY ErfasstAm DESC";
                cmd.Parameters.AddWithValue("@Belegnummer", belegnummer ?? (object)DBNull.Value);
                var src = new DataTable(); using (var rdr = await cmd.ExecuteReaderAsync()) { src.Load(rdr); }
                if (src.Rows.Count == 0) { tx.Rollback(); throw new InvalidOperationException("Eintrag nicht gefunden."); }
                var r = src.Rows[0];
                int currentRev = (r.Table.Columns.Contains("RevNum") && r["RevNum"] != DBNull.Value) ? Convert.ToInt32(r["RevNum"]) : 0;
                int nextRev = currentRev + 1;

                // Basis-Kassenbestand vor der ursprünglichen Buchung ermitteln
                Func<object, decimal> toDec = (o) => (o == null || o == DBNull.Value) ? 0m : Convert.ToDecimal(o);
                decimal oldDelta = Math.Round(toDec(r["Betrag19"]) + toDec(r["Betrag7"]) + toDec(r["Betrag0"]), 2);
                decimal kbAfterOld = Math.Round(toDec(r["Kassenbestand"]), 2);
                decimal kbBefore = Math.Round(kbAfterOld - oldDelta, 2);
                decimal running = 0m;

                cmd.Parameters.Clear();
                cmd.CommandText = $@"UPDATE {_tblKassenbuch} SET RevIsOld = 1 WHERE Belegnummer = @Belegnummer AND ISNULL(RevIsOld,0) = 0";
                cmd.Parameters.AddWithValue("@Belegnummer", belegnummer ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                // KassenBelegnummer aus Quelle übernehmen, falls vorhanden
                object kassenBeleg = r.Table.Columns.Contains("KassenBelegnummer") ? r["KassenBelegnummer"] : (object)DBNull.Value;

                if (b19 != 0m)
                {
                    running = Math.Round(running + b19, 2);
                    var o = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Buchungstext", (object)(buchungstext ?? r["Buchungstext"]) ?? DBNull.Value },
                        { "Betrag19", b19 },
                        { "Betrag7", 0m },
                        { "Betrag0", 0m },
                        { "Kassenbestand", Math.Round(kbBefore + running, 2) }
                    };
                    // RevPersId (falls vorhanden) mit eingeloggter PID setzen
                    try { o["RevPersId"] = (object)(AppSession.CurrentUser?.PID ?? 0); } catch { o["RevPersId"] = 0; }
                    if (k1_19.HasValue) o["Kost1"] = k1_19.Value; if (k2_19.HasValue) o["Kost2"] = k2_19.Value; if (kto_19.HasValue) o["Konto"] = kto_19.Value;
                    if (!(kassenBeleg is DBNull)) o["KassenBelegnummer"] = kassenBeleg;
                    await InsertCloneWithSameBelegnummerAsync(tx, r, o, nextRev++);
                }
                if (b7 != 0m)
                {
                    running = Math.Round(running + b7, 2);
                    var o = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Buchungstext", (object)(buchungstext ?? r["Buchungstext"]) ?? DBNull.Value },
                        { "Betrag19", 0m },
                        { "Betrag7", b7 },
                        { "Betrag0", 0m },
                        { "Kassenbestand", Math.Round(kbBefore + running, 2) }
                    };
                    // RevPersId (falls vorhanden) mit eingeloggter PID setzen
                    try { o["RevPersId"] = (object)(AppSession.CurrentUser?.PID ?? 0); } catch { o["RevPersId"] = 0; }
                    if (k1_7.HasValue) o["Kost1"] = k1_7.Value; if (k2_7.HasValue) o["Kost2"] = k2_7.Value; if (kto_7.HasValue) o["Konto"] = kto_7.Value;
                    if (!(kassenBeleg is DBNull)) o["KassenBelegnummer"] = kassenBeleg;
                    await InsertCloneWithSameBelegnummerAsync(tx, r, o, nextRev++);
                }
                if (b0 != 0m)
                {
                    running = Math.Round(running + b0, 2);
                    var o = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Buchungstext", (object)(buchungstext ?? r["Buchungstext"]) ?? DBNull.Value },
                        { "Betrag19", 0m },
                        { "Betrag7", 0m },
                        { "Betrag0", b0 },
                        { "Kassenbestand", Math.Round(kbBefore + running, 2) }
                    };
                    // RevPersId (falls vorhanden) mit eingeloggter PID setzen
                    try { o["RevPersId"] = (object)(AppSession.CurrentUser?.PID ?? 0); } catch { o["RevPersId"] = 0; }
                    if (k1_0.HasValue) o["Kost1"] = k1_0.Value; if (k2_0.HasValue) o["Kost2"] = k2_0.Value; if (kto_0.HasValue) o["Konto"] = kto_0.Value;
                    if (!(kassenBeleg is DBNull)) o["KassenBelegnummer"] = kassenBeleg;
                    await InsertCloneWithSameBelegnummerAsync(tx, r, o, nextRev++);
                }

                tx.Commit();
            }
        }

        // ---------- Persistenz Abrechnungsregeln ----------
        private async Task EnsureRulesTableAsync()
        {
            await EnsureOpenAsync();
            if (string.IsNullOrEmpty(_tblAbrechnungsRegeln))
                _tblAbrechnungsRegeln = await ResolveQualifiedTableAsync("TAbrechnungsBedingungen") ?? "[dbo].[TAbrechnungsBedingungen]";
            if (string.IsNullOrEmpty(_tblAbrechnungsClauses))
                _tblAbrechnungsClauses = await ResolveQualifiedTableAsync("TAbrechnungsBedingungenClause") ?? "[dbo].[TAbrechnungsBedingungenClause]";

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.name='TAbrechnungsBedingungen')
BEGIN
    CREATE TABLE {_tblAbrechnungsRegeln}
    (
        Id              int IDENTITY(1,1) PRIMARY KEY,
        Name            varchar(200) NOT NULL,
        JoinKind        varchar(10)  NULL,
        IsDefault       bit NOT NULL DEFAULT(0),
        Priority        int NOT NULL DEFAULT(100),
        ManId           int NULL,
        FhzIdList       varchar(4000) NULL,
        PersId          int NULL,
        ResultKost1     int NULL,
        ResultKost2     int NULL,
        ResultKonto     int NULL,
        ResultText      nvarchar(4000) NULL,
        IsActive        bit NOT NULL DEFAULT(1),
        CreatedAt       datetime2(0) NOT NULL DEFAULT(SYSUTCDATETIME()),
        ModifiedAt      datetime2(0) NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.name='TAbrechnungsBedingungenClause')
BEGIN
    CREATE TABLE {_tblAbrechnungsClauses}
    (
        Id       int IDENTITY(1,1) PRIMARY KEY,
        RuleId   int NOT NULL,
        GroupId  int NOT NULL DEFAULT(0),
        Field    nvarchar(128) NOT NULL,
        Operator nvarchar(16) NOT NULL,
        Value    nvarchar(4000) NULL
    );
    CREATE INDEX IX_Clauses_RuleId ON {_tblAbrechnungsClauses}(RuleId);
END";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<DataTable> LoadAbrechnungsRegelnAsync()
        {
            await EnsureRulesTableAsync();
            using (var cmd = _connection.CreateCommand())
            {
                // Explicit column list to avoid binding issues with legacy/removed columns
                cmd.CommandText = $@"SELECT Id, Name, JoinKind, IsDefault, Priority,
    ResultKost1, ResultKost2, ResultKonto, ResultText, IsActive
FROM {_tblAbrechnungsRegeln} WITH (NOLOCK)
WHERE IsActive=1
ORDER BY Priority ASC, Id ASC";
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable(); dt.Load(rdr); return dt;
                }
            }
        }

        public async Task<DataTable> LoadAbrechnungsClausesAsync()
        {
            await EnsureRulesTableAsync();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $@"SELECT * FROM {_tblAbrechnungsClauses} WITH (NOLOCK) ORDER BY RuleId ASC, GroupId ASC, Id ASC";
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable(); dt.Load(rdr); return dt;
                }
            }
        }

        public async Task<int> SaveAbrechnungsRegelAsync(AbrechnungsRegel r)
        {
            await EnsureRulesTableAsync();
            using (var cmd = _connection.CreateCommand())
            {
                if (r.Id == 0)
                {
                    cmd.CommandText = $@"INSERT INTO {_tblAbrechnungsRegeln}
(Name, JoinKind, IsDefault, Priority, ResultKost1, ResultKost2, ResultKonto, ResultText, IsActive, ModifiedAt)
VALUES (@Name,@Join,@Def,@Prio,@K1,@K2,@Kto,@Txt,1,SYSUTCDATETIME()); SELECT SCOPE_IDENTITY();";
                }
                else
                {
                    cmd.CommandText = $@"UPDATE {_tblAbrechnungsRegeln}
SET Name=@Name, JoinKind=@Join, IsDefault=@Def, Priority=@Prio, ResultKost1=@K1, ResultKost2=@K2, ResultKonto=@Kto, ResultText=@Txt, ModifiedAt=SYSUTCDATETIME()
WHERE Id=@Id; SELECT @Id;";
                    cmd.Parameters.AddWithValue("@Id", r.Id);
                }
                cmd.Parameters.AddWithValue("@Name", (object)(r.Name ?? (object)DBNull.Value) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Join", (object)(r.JoinKind ?? (object)DBNull.Value) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Def", r.IsDefault);
                cmd.Parameters.AddWithValue("@Prio", r.Priority);
                // removed: AdditionalWhere column
                cmd.Parameters.AddWithValue("@K1", (object)r.ResultKost1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@K2", (object)r.ResultKost2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Kto", (object)r.ResultKonto ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Txt", (object)(r.ResultBuchungstext ?? (object)DBNull.Value) ?? DBNull.Value);
                // removed: RawConditions/RawResults columns
                var o = await cmd.ExecuteScalarAsync();
                int ruleId = Convert.ToInt32(Convert.ToDecimal(o));

                await SaveClausesAsync(ruleId, r.Clauses);
                return ruleId;
            }
        }

        public async Task SaveClausesAsync(int ruleId, IList<AbrechnungsClause> clauses)
        {
            await EnsureRulesTableAsync();
            using (var tx = _connection.BeginTransaction())
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $@"DELETE FROM {_tblAbrechnungsClauses} WHERE RuleId=@R";
                cmd.Parameters.AddWithValue("@R", ruleId);
                await cmd.ExecuteNonQueryAsync();
                cmd.Parameters.Clear();

                if (clauses != null)
                {
                    foreach (var c in clauses)
                    {
                        cmd.CommandText = $@"INSERT INTO {_tblAbrechnungsClauses} (RuleId, GroupId, Field, Operator, Value) VALUES (@R,@G,@F,@O,@V)";
                        cmd.Parameters.AddWithValue("@R", ruleId);
                        cmd.Parameters.AddWithValue("@G", c.GroupId);
                        cmd.Parameters.AddWithValue("@F", (object)(c.Field ?? (object)DBNull.Value) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@O", (object)(c.Operator ?? (object)DBNull.Value) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@V", (object)(c.Value ?? (object)DBNull.Value) ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                        cmd.Parameters.Clear();
                    }
                }

                tx.Commit();
            }
        }

        private static byte MapTypStringToCode(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return 0;
            t = t.Trim();
            // Neu: numerische Codes direkt erlauben
            if (byte.TryParse(t, out var num) && num >= 1 && num <= 5)
                return num;
            switch (t.ToLowerInvariant())
            {
                case "anfangsbestand": return 1;
                case "einzahlung": return 2;
                case "auszahlung": return 3;
                case "schichtabrechnung": return 4;
                case "personalguthaben": return 5;
                default: return 0;
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }

    // DTO für dieses Projekt
    public class PersonalInfo
    {
        public int PID { get; set; }
        public string Name { get; set; }
        public string Vorname { get; set; }
        public string NFCTagUID { get; set; }
        public string Fahrercode { get; set; }
    }
}
