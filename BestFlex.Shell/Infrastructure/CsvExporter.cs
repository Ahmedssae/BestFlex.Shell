using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace BestFlex.Shell.Infrastructure
{
    public static class CsvExporter
    {
        public static void Export<T>(IEnumerable<T> rows, IReadOnlyList<(string Header, Func<T, object?> Selector)> columns, string defaultFileName = "invoices.csv")
        {
            var dlg = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                OverwritePrompt = true,
                AddExtension = true,
                DefaultExt = ".csv"
            };
            if (dlg.ShowDialog() != true) return;

            // UTF-8 BOM for Excel friendliness
            using var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            // header
            sw.WriteLine(string.Join(",", columns.Select(c => Escape(c.Header))));

            foreach (var r in rows)
            {
                var cells = columns.Select(c =>
                {
                    var v = c.Selector(r);
                    return Escape(v is IFormattable f
                        ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                        : v?.ToString() ?? "");
                });
                sw.WriteLine(string.Join(",", cells));
            }
        }

        private static string Escape(string s)
        {
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
