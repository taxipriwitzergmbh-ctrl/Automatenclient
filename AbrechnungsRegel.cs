using System;
using System.Collections.Generic;
using System.Linq;

namespace TaMi_Kassenclient
{
    // Modell einer Abrechnungs-Regel
    public class AbrechnungsRegel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string JoinKind { get; set; }
        public bool IsDefault { get; set; }
        public int Priority { get; set; }

        // DEPRECATED (Phase 1): formerly aggregated condition fields now superseded by Clauses
        public int? ManId { get; set; } // no longer persisted / evaluated
        public List<int> FhzIds { get; set; } // no longer persisted / evaluated
        public int? PersId { get; set; } // no longer persisted / evaluated
        // removed: AdditionalWhere

        // Ergebnisse
        public int? ResultKost1 { get; set; }
        public int? ResultKost2 { get; set; }
        public int? ResultKonto { get; set; }
        public string ResultBuchungstext { get; set; }

        // removed: RawConditionsJson, RawResultsJson
        public bool IsActive { get; set; } = true;

        // Authoritative structured clauses
        public List<AbrechnungsClause> Clauses { get; set; }
        public string PresetName { get; set; }

        public string GetReadableCondition()
        {
            // Only show clause logic now
            if (Clauses == null || Clauses.Count == 0)
                return IsDefault ? "(keine Bedingung)  [Standard]" : "(keine Bedingung)";

            var byGroup = Clauses.GroupBy(c => c.GroupId).OrderBy(g => g.Key);
            var grpStrings = new List<string>();
            foreach (var g in byGroup)
            {
                var s = string.Join(" AND ", g.Select(c => FormatClause(c)));
                grpStrings.Add("(" + s + ")");
            }
            var result = string.Join(" OR ", grpStrings);
            if (IsDefault) result += "  [Standard]";
            return result;
        }

        private static string FormatClause(AbrechnungsClause c)
        {
            if (c == null) return string.Empty;
            var val = c.Value;
            if (string.Equals(c.Operator, "IN", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
            {
                var parts = val.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                val = "(" + string.Join(",", parts) + ")";
            }
            else if (!decimal.TryParse(val, out var _) && !int.TryParse(val, out var _))
            {
                // quote string values
                val = "'" + (val ?? string.Empty).Replace("'", "''") + "'";
            }
            return $"{c.Field} {c.Operator} {val}".Trim();
        }

        public AbrechnungsRegel Clone()
        {
            return new AbrechnungsRegel
            {
                Id = this.Id,
                Name = this.Name,
                JoinKind = this.JoinKind,
                IsDefault = this.IsDefault,
                Priority = this.Priority,
                // Deprecated fields copied only for backward compatibility (will be null in new rules)
                ManId = this.ManId,
                FhzIds = this.FhzIds != null ? new List<int>(this.FhzIds) : null,
                PersId = this.PersId,
                // AdditionalWhere removed
                ResultKost1 = this.ResultKost1,
                ResultKost2 = this.ResultKost2,
                ResultKonto = this.ResultKonto,
                ResultBuchungstext = this.ResultBuchungstext,
                // RawConditionsJson/RawResultsJson removed
                IsActive = this.IsActive,
                Clauses = this.Clauses != null ? new List<AbrechnungsClause>(this.Clauses.Select(c => c.Clone())) : null,
                PresetName = this.PresetName
            };
        }
    }

    public class AbrechnungsClause
    {
        public int Id { get; set; }
        public int RuleId { get; set; }
        public int GroupId { get; set; }
        public string Field { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }

        public AbrechnungsClause Clone()
        {
            return new AbrechnungsClause
            {
                Id = this.Id,
                RuleId = this.RuleId,
                GroupId = this.GroupId,
                Field = this.Field,
                Operator = this.Operator,
                Value = this.Value
            };
        }
    }
}
