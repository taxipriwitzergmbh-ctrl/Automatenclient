using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TaMi_Automatenclient.Export
{
    public enum DatevCsvFormat
    {
        UnternehmenOnlineKasse, // Belegdatum;Belegnummer;Buchungstext;Umsatz (ohne Soll/Haben-Kennzeichen);Soll/Haben-Kennzeichen;Steuersatz;Kost1;Kost2;Gegenkonto (ohne BU-Schl�ssel);W�hrung
        StandardVorzBetrag      // "W�hrung";"VorzBetrag";"RechNr";"BelegDatum";"Belegtext";"UStSatz";"BU";"Gegenkonto";"Kost1";"Kost2";"Kostmenge";"Skonto";"Nachricht"
    }

    public class DatevKasseCsvExporterOptions
    {
        public bool IncludeHeader { get; set; } = true;
        public string Delimiter { get; set; } = ";";
        public CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("de-DE");
        public bool Utf8Bom { get; set; } = true;
        // TT.MM.JJJJ (true) oder TTMMJJJJ (false) � f�r UnternehmenOnlineKasse
        public bool UseDotDateFormat { get; set; } = true;
        public string Currency { get; set; } = "EUR";
        public DatevCsvFormat Format { get; set; } = DatevCsvFormat.UnternehmenOnlineKasse;
        // F�r StandardVorzBetrag: BelegDatum-Format TTMM (true) oder TTMMJJJJ (false)
        public bool UseDayMonthOnly { get; set; } = true;
        // Nachricht Standardwert
        public string DefaultNachricht { get; set; } = "Kasse Import Standardformat";
    }

    public class DatevKasseCsvRow
    {
        public DateTime Belegdatum { get; set; }
        public string Belegnummer { get; set; }
        public string Buchungstext { get; set; }

        // UnternehmenOnlineKasse
        public decimal BetragOhneKz { get; set; }
        public string SollHabenKennzeichen { get; set; } // "S" / "H"
        public string Steuersatz { get; set; } // 0,7,19 (ohne %)
        public string Kostenstelle1 { get; set; } // Kost1
        public string Kostenstelle2 { get; set; } // Kost2
        public string Gegenkonto { get; set; } // ohne BU-Schl�ssel
        public string Waehrung { get; set; } = "EUR";

        // StandardVorzBetrag
        public decimal BetragSigned { get; set; } // mit Vorzeichen
        public string RechNr { get; set; } // optional, sonst Belegnummer
        public string BU { get; set; }
        public string Kostmenge { get; set; }
        public string Skonto { get; set; }
        public string Nachricht { get; set; }
    }

    public static class DatevKasseCsvExporter
    {
        public static async Task ExportAsync(IEnumerable<DatevKasseCsvRow> rows, string filePath, DatevKasseCsvExporterOptions options = null)
        {
            options = options ?? new DatevKasseCsvExporterOptions();
            var enc = options.Utf8Bom ? new UTF8Encoding(true) : new UTF8Encoding(false);

            using (var sw = new StreamWriter(filePath, false, enc))
            {
                if (options.IncludeHeader)
                {
                    if (options.Format == DatevCsvFormat.UnternehmenOnlineKasse)
                    {
                        await sw.WriteLineAsync(string.Join(options.Delimiter, new[]
                        {
                            "Belegdatum",
                            "Belegnummer",
                            "Buchungstext",
                            "Umsatz (ohne Soll/Haben-Kennzeichen)",
                            "Soll/Haben-Kennzeichen",
                            "Steuersatz",
                            "Kost1",
                            "Kost2",
                            "Gegenkonto (ohne BU-Schl�ssel)",
                            "W�hrung"
                        }));
                    }
                    else // StandardVorzBetrag
                    {
                        await sw.WriteLineAsync(string.Join(options.Delimiter, new[]
                        {
                            "W�hrung",
                            "VorzBetrag",
                            "RechNr",
                            "BelegDatum",
                            "Belegtext",
                            "UStSatz",
                            "BU",
                            "Gegenkonto",
                            "Kost1",
                            "Kost2",
                            "Kostmenge",
                            "Skonto",
                            "Nachricht"
                        }));
                    }
                }

                foreach (var r in rows)
                {
                    if (options.Format == DatevCsvFormat.UnternehmenOnlineKasse)
                    {
                        string belegdatum = options.UseDotDateFormat
                            ? r.Belegdatum.ToString("dd.MM.yyyy", options.Culture)
                            : r.Belegdatum.ToString("ddMMyyyy", options.Culture);

                        string betrag = r.BetragOhneKz.ToString("0.00", options.Culture);

                        var fields = new[]
                        {
                            belegdatum,
                            r.Belegnummer ?? string.Empty,
                            r.Buchungstext ?? string.Empty,
                            betrag,
                            r.SollHabenKennzeichen ?? string.Empty,
                            (r.Steuersatz ?? string.Empty).TrimEnd('%'),
                            r.Kostenstelle1 ?? string.Empty,
                            r.Kostenstelle2 ?? string.Empty,
                            r.Gegenkonto ?? string.Empty,
                            string.IsNullOrWhiteSpace(r.Waehrung) ? options.Currency : r.Waehrung
                        };
                        await sw.WriteLineAsync(ToCsvLine(fields, options.Delimiter));
                    }
                    else
                    {
                        // BelegDatum im Standardformat: TTMM (oder TTMMJJJJ wenn gew�nscht)
                        string belegdatum = options.UseDayMonthOnly
                            ? r.Belegdatum.ToString("ddMM", options.Culture)
                            : r.Belegdatum.ToString("ddMMyyyy", options.Culture);

                        // VorzBetrag mit f�hrendem + oder -
                        string sign = r.BetragSigned >= 0m ? "+" : "-";
                        string betrag = Math.Abs(r.BetragSigned).ToString("0.00", options.Culture);
                        string vorzBetrag = sign + betrag;

                        var fields = new[]
                        {
                            string.IsNullOrWhiteSpace(r.Waehrung) ? options.Currency : r.Waehrung,
                            vorzBetrag,
                            string.IsNullOrWhiteSpace(r.RechNr) ? (r.Belegnummer ?? string.Empty) : r.RechNr,
                            belegdatum,
                            r.Buchungstext ?? string.Empty,
                            (r.Steuersatz ?? string.Empty).TrimEnd('%'),
                            r.BU ?? string.Empty,
                            r.Gegenkonto ?? string.Empty,
                            r.Kostenstelle1 ?? string.Empty,
                            r.Kostenstelle2 ?? string.Empty,
                            r.Kostmenge ?? string.Empty,
                            r.Skonto ?? string.Empty,
                            string.IsNullOrWhiteSpace(r.Nachricht) ? options.DefaultNachricht : r.Nachricht
                        };
                        await sw.WriteLineAsync(ToCsvLine(fields, options.Delimiter));
                    }
                }
            }
        }

        private static string ToCsvLine(string[] fields, string delimiter)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(delimiter);
                sb.Append('"');
                var v = fields[i] ?? string.Empty;
                sb.Append(v.Replace("\"", "\"\""));
                sb.Append('"');
            }
            return sb.ToString();
        }
    }
}
