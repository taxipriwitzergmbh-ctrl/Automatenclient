using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text; // added for diagnostics

namespace TaMi_Kassenclient
{
    public static class RulesEngine
    {
        public sealed class RuleContext
        {
            public int FirmenId { get; set; }
            public int? PersId { get; set; }
            public int? FhzId { get; set; }
            public string Typ { get; set; }
            public decimal Betrag19 { get; set; }
            public decimal Betrag7 { get; set; }
            public decimal Betrag0 { get; set; }
        }

        private static bool TryParseInt(string s, out int v) { return int.TryParse((s ?? string.Empty).Trim(), out v); }
        private static bool TryParseDecimal(string s, out decimal v) { return decimal.TryParse((s ?? string.Empty).Trim(), out v); }

        private static bool EvalClause(AbrechnungsClause c, RuleContext ctx)
        {
            if (c == null) return true;
            var op = (c.Operator ?? string.Empty).Trim().ToUpperInvariant();
            var fld = (c.Field ?? string.Empty).Trim();
            var val = c.Value ?? string.Empty;

            Func<int?> getInt = () =>
            {
                switch (fld.ToLowerInvariant())
                {
                    case "manid": return ctx.FirmenId; // legacy name
                    case "firmenid": return ctx.FirmenId; // alias after remap
                    case "persid": return ctx.PersId;
                    case "fhzid": return ctx.FhzId;
                }
                return null;
            };
            Func<decimal?> getDec = () =>
            {
                switch (fld.ToLowerInvariant())
                {
                    case "betrag19": return ctx.Betrag19;
                    case "betrag7": return ctx.Betrag7;
                    case "betrag0": return ctx.Betrag0;
                }
                return null;
            };
            Func<string> getStr = () =>
            {
                switch (fld.ToLowerInvariant())
                {
                    case "typ": return ctx.Typ ?? string.Empty;
                }
                return null;
            };

            if (op == "IN" || op == "NOT IN")
            {
                var items = (val ?? string.Empty).Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                var ii = new HashSet<int>();
                foreach (var s in items) { if (TryParseInt(s, out var iv)) ii.Add(iv); }
                bool contains;
                if (ii.Count > 0)
                {
                    var iv = getInt();
                    contains = iv.HasValue && ii.Contains(iv.Value);
                }
                else
                {
                    var sv = getStr();
                    contains = sv != null && items.Contains(sv, StringComparer.OrdinalIgnoreCase);
                }
                return op == "IN" ? contains : !contains;
            }

            if (op == "IS NULL" || op == "IS NOT NULL")
            {
                bool isNull;
                var iv = getInt();
                if (iv.HasValue) isNull = false; else
                {
                    var dv = getDec(); if (dv.HasValue) isNull = false; else
                    {
                        var sv = getStr(); isNull = string.IsNullOrEmpty(sv);
                    }
                }
                return op == "IS NULL" ? isNull : !isNull;
            }

            var leftInt = getInt();
            if (leftInt.HasValue)
            {
                if (!TryParseInt(val, out var rightInt)) rightInt = 0;
                switch (op)
                {
                    case "=": return leftInt.Value == rightInt;
                    case "!=": return leftInt.Value != rightInt;
                    case ">": return leftInt.Value > rightInt;
                    case ">=": return leftInt.Value >= rightInt;
                    case "<": return leftInt.Value < rightInt;
                    case "<=": return leftInt.Value <= rightInt;
                }
            }

            var leftDec = getDec();
            if (leftDec.HasValue)
            {
                if (!TryParseDecimal(val, out var rightDec)) rightDec = 0m;
                switch (op)
                {
                    case "=": return leftDec.Value == rightDec;
                    case "!=": return leftDec.Value != rightDec;
                    case ">": return leftDec.Value > rightDec;
                    case ">=": return leftDec.Value >= rightDec;
                    case "<": return leftDec.Value < rightDec;
                    case "<=": return leftDec.Value <= rightDec;
                }
            }

            var leftStr = getStr();
            if (leftStr != null)
            {
                switch (op)
                {
                    case "=": return string.Equals(leftStr, val, StringComparison.OrdinalIgnoreCase);
                    case "!=": return !string.Equals(leftStr, val, StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        // Legacy AdditionalWhere support kept but we now expect it to be NULL; method retained for backwards compatible data
        private static string SanitizeWhere(string where)
        {
            if (string.IsNullOrWhiteSpace(where)) return string.Empty;
            var w = where;
            try { w = Regex.Replace(w, @"\[[^\]]*\]", " "); } catch { }
            w = w.Replace("ISNULL(Betrag19,0)", "Betrag19");
            w = w.Replace("ISNULL(Betrag7,0)", "Betrag7");
            w = w.Replace("ISNULL(Betrag0,0)", "Betrag0");
            w = Regex.Replace(w, @"\s+", " ").Trim();
            return w;
        }
        private static bool EvalAdditionalWhere(string where, RuleContext ctx)
        {
            if (string.IsNullOrWhiteSpace(where)) return true; // treat empty as pass
            var w = SanitizeWhere(where);
            var orParts = Regex.Split(w, @"\s+OR\s+", RegexOptions.IgnoreCase);
            bool anyTrue = false;
            foreach (var orp in orParts)
            {
                var andParts = Regex.Split(orp, @"\s+AND\s+", RegexOptions.IgnoreCase);
                bool allAnd = true;
                foreach (var raw in andParts)
                {
                    var e = (raw ?? string.Empty).Trim(); if (string.IsNullOrEmpty(e)) continue;
                    while (e.StartsWith("(") && e.EndsWith(")") && e.Length >= 2) e = e.Substring(1, e.Length - 2).Trim();
                    // Fallback: if parsing gets too complex, ignore (do not block rule)
                    if (!(e.Contains("=") || e.Contains(">") || e.Contains("<"))) continue;
                    // Very light-weight evaluation (only Typ/FhzId/ManId/BetragX) – kept for legacy rows
                    var ops = new[] { ">=", "<=", "!=", "=", ">", "<" };
                    string found = null; foreach (var op in ops) { var idx = e.IndexOf(op, StringComparison.OrdinalIgnoreCase); if (idx > 0) { found = op; break; } }
                    if (found == null) continue;
                    var parts = e.Split(new[] { found }, StringSplitOptions.None); if (parts.Length != 2) continue;
                    var left = parts[0].Trim().ToLowerInvariant(); var right = parts[1].Trim().Trim('"', '\'');
                    bool ok = true;
                    if (left == "typ") ok = CompareStrings(ctx.Typ ?? string.Empty, right, found);
                    else if (left == "fhzid") ok = CompareInts(ctx.FhzId, right, found);
                    else if (left == "manid" || left == "firmenid") ok = CompareInts(ctx.FirmenId, right, found);
                    else if (left == "betrag19") ok = CompareDecimals(ctx.Betrag19, right, found);
                    else if (left == "betrag7") ok = CompareDecimals(ctx.Betrag7, right, found);
                    else if (left == "betrag0") ok = CompareDecimals(ctx.Betrag0, right, found);
                    if (!ok) { allAnd = false; break; }
                }
                if (allAnd) { anyTrue = true; break; }
            }
            return anyTrue;
        }

        private static int IndexOfInvariant(string haystack, string needle) => haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

        private static bool CompareInts(int? left, string rightStr, string op)
        { if (!TryParseInt(rightStr, out var right)) right = 0; if (!left.HasValue) return false; switch (op) { case "=": return left.Value == right; case "!=": return left.Value != right; case ">": return left.Value > right; case ">=": return left.Value >= right; case "<": return left.Value < right; case "<=": return left.Value <= right; } return false; }
        private static bool CompareDecimals(decimal left, string rightStr, string op)
        { if (!TryParseDecimal(rightStr, out var right)) right = 0m; switch (op) { case "=": return left == right; case "!=": return left != right; case ">": return left > right; case ">=": return left >= right; case "<": return left < right; case "<=": return left <= right; } return false; }
        private static bool CompareStrings(string left, string right, string op)
        { switch (op) { case "=": return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); case "!=": return !string.Equals(left, right, StringComparison.OrdinalIgnoreCase); } return false; }

        private static bool MatchesRule(AbrechnungsRegel r, RuleContext ctx)
        {
            // Legacy aggregated fields (ManId, PersId, FhzIds, AdditionalWhere) are ignored now; only Clauses are authoritative.
            if (r.Clauses != null && r.Clauses.Count > 0)
            {
                var groups = r.Clauses.GroupBy(c => c.GroupId);
                bool anyGroupTrue = false;
                foreach (var g in groups)
                {
                    bool allTrue = true;
                    foreach (var c in g)
                    {
                        if (!EvalClause(c, ctx)) { allTrue = false; break; }
                    }
                    if (allTrue) { anyGroupTrue = true; break; }
                }
                if (!anyGroupTrue) return false;
            }
            // AdditionalWhere (legacy) last – permissive if null/empty
            // removed: legacy AdditionalWhere handling
            return true;
        }

        public static string ExplainRuleMatching(IEnumerable<AbrechnungsRegel> rules, RuleContext ctx)
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine($"Context: ManId={ctx?.FirmenId}, PersId={ctx?.PersId}, FhzId={ctx?.FhzId}, Typ='{ctx?.Typ}', B19={ctx?.Betrag19}, B7={ctx?.Betrag7}, B0={ctx?.Betrag0}");
                if (rules == null) { sb.AppendLine("No rules loaded."); return sb.ToString(); }
                int idx = 0;
                foreach (var r in rules)
                {
                    idx++;
                    if (r == null) { sb.AppendLine($"[{idx}] <null> rule"); continue; }
                    if (!r.IsActive) { sb.AppendLine($"[{idx}] {r.Name} (Id={r.Id}) -> inactive"); continue; }
                    bool match = MatchesRule(r, ctx);
                    sb.AppendLine(match ? $"[{idx}] MATCH {r.Name} (Id={r.Id})" : $"[{idx}] no match {r.Name} (Id={r.Id})");
                }
            }
            catch (Exception ex) { sb.AppendLine("Explain failed: " + ex.Message); }
            return sb.ToString();
        }

        public static async Task<List<AbrechnungsRegel>> LoadRulesAsync()
        {
            using (var db = new DatabaseHelperKassen())
            {
                var dt = await db.LoadAbrechnungsRegelnAsync();
                var list = new List<AbrechnungsRegel>();
                foreach (DataRow r in dt.Rows)
                {
                    var model = new AbrechnungsRegel
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Name = r["Name"] as string,
                        JoinKind = r["JoinKind"] as string,
                        IsDefault = r["IsDefault"] != DBNull.Value && Convert.ToBoolean(r["IsDefault"]),
                        Priority = r["Priority"] == DBNull.Value ? 100 : Convert.ToInt32(r["Priority"]),
                        ManId = null,
                        FhzIds = null,
                        PersId = null,
                        ResultKost1 = r["ResultKost1"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ResultKost1"]),
                        ResultKost2 = r["ResultKost2"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ResultKost2"]),
                        ResultKonto = r["ResultKonto"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ResultKonto"]),
                        ResultBuchungstext = r["ResultText"] as string,
                        IsActive = r.Table.Columns.Contains("IsActive") && r["IsActive"] != DBNull.Value ? Convert.ToBoolean(r["IsActive"]) : true,
                        Clauses = new List<AbrechnungsClause>()
                    };
                    list.Add(model);
                }

                await TryLoadClausesAsync(list, db);

                return list
                    .OrderBy(r => r.IsDefault ? 1 : 0)
                    .ThenBy(r => r.Priority)
                    .ThenBy(r => r.Id)
                    .ToList();
            }
        }

        private static async Task TryLoadClausesAsync(List<AbrechnungsRegel> rules, DatabaseHelperKassen db)
        {
            try
            {
                var dt = await db.LoadAbrechnungsClausesAsync();
                var lookup = rules.ToDictionary(r => r.Id, r => r);
                foreach (DataRow r in dt.Rows)
                {
                    int ruleId = Convert.ToInt32(r["RuleId"]);
                    if (!lookup.TryGetValue(ruleId, out var rule)) continue;
                    if (rule.Clauses == null) rule.Clauses = new List<AbrechnungsClause>();
                    rule.Clauses.Add(new AbrechnungsClause
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        RuleId = ruleId,
                        GroupId = r["GroupId"] == DBNull.Value ? 0 : Convert.ToInt32(r["GroupId"]),
                        Field = r["Field"] as string,
                        Operator = r["Operator"] as string,
                        Value = r["Value"] as string
                    });
                }
            }
            catch { }
        }

        public static void ApplyForEdit(IEnumerable<AbrechnungsRegel> rules, RuleContext ctx, decimal v19, decimal v7, decimal v0,
            ref int? kost1, ref int? kost2, ref int? konto, ref string buchungstext)
        {
            if (ctx == null) ctx = new RuleContext();
            ctx.Betrag19 = v19; ctx.Betrag7 = v7; ctx.Betrag0 = v0;
            ApplyForEdit(rules, ctx, ref kost1, ref kost2, ref konto, ref buchungstext);
        }

        public static void ApplyForEdit(IEnumerable<AbrechnungsRegel> rules, RuleContext ctx,
            ref int? kost1, ref int? kost2, ref int? konto, ref string buchungstext)
        {
            if (rules == null) return;
            AbrechnungsRegel bestDefault = null;

            foreach (var r in rules)
            {
                if (!r.IsActive) continue;
                if (r.IsDefault && bestDefault == null) bestDefault = r; // first active default
                if (!MatchesRule(r, ctx)) continue;
                if (!kost1.HasValue && r.ResultKost1.HasValue) kost1 = r.ResultKost1;
                if (!kost2.HasValue && r.ResultKost2.HasValue) kost2 = r.ResultKost2;
                if (!konto.HasValue && r.ResultKonto.HasValue) konto = r.ResultKonto;
                if (string.IsNullOrWhiteSpace(buchungstext) && !string.IsNullOrWhiteSpace(r.ResultBuchungstext)) buchungstext = r.ResultBuchungstext;
                if (kost1.HasValue && kost2.HasValue && konto.HasValue && !string.IsNullOrWhiteSpace(buchungstext)) break;
            }

            if ((!kost1.HasValue && !kost2.HasValue && !konto.HasValue) && bestDefault != null)
            {
                if (!kost1.HasValue && bestDefault.ResultKost1.HasValue) kost1 = bestDefault.ResultKost1;
                if (!kost2.HasValue && bestDefault.ResultKost2.HasValue) kost2 = bestDefault.ResultKost2;
                if (!konto.HasValue && bestDefault.ResultKonto.HasValue) konto = bestDefault.ResultKonto;
                if (string.IsNullOrWhiteSpace(buchungstext) && !string.IsNullOrWhiteSpace(bestDefault.ResultBuchungstext)) buchungstext = bestDefault.ResultBuchungstext;
            }
        }
    }
}
