using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace StepScraper;

public static partial class DocReader
{
    public static List<string> ReadFolderAndExtractYoutubeLinks(string folderPath)
    {
        List<string> youtubeLinks = [];
        foreach (string file in Directory.EnumerateFiles(folderPath))
        {
            Console.WriteLine($"reading {file}");

            if (file.EndsWith(".pdf"))
            {
                PdfDocument pdfDocument = PdfReader.Open(file);
                foreach (PdfPage page in pdfDocument.Pages)
                {
                    string agg = string.Join("", page.ExtractText());

                    youtubeLinks.AddRange(ExtractYoutubeLinks(agg));
                }
            }
        }

        return youtubeLinks;
    }

    static IEnumerable<string> ExtractYoutubeLinks(string text)
    {
        List<string> youtubeLinks = [];
        foreach (Match match in MyRegex().Matches(text))
        {
            youtubeLinks.Add(match.Value);
        }

        return youtubeLinks;
    }

    [GeneratedRegex(@"(http(s)?://)?(www\.)?(youtube\.com/|youtu\.be/)[a-zA-Z0-9\-_]+(\?[\w=&]+)?")]
    private static partial Regex MyRegex();
}