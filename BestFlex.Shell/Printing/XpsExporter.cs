using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;

namespace BestFlex.Shell.Printing
{
    public static class XpsExporter
    {
        public static void ExportMany(string folderPath, IEnumerable<(FlowDocument doc, string fileName)> docs)
        {
            Directory.CreateDirectory(folderPath);

            foreach (var pair in docs)
            {
                var safe = MakeSafeFileName(pair.fileName);
                var path = Path.Combine(folderPath, safe + ".xps");

                using var xps = new XpsDocument(path, FileAccess.Write, CompressionOption.Maximum);
                XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xps);
                var paginator = ((IDocumentPaginatorSource)pair.doc).DocumentPaginator;
                writer.Write(paginator);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "document" : cleaned;
        }
    }
}
