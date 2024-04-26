using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;

namespace StepScraper;

public static class PdfSharpExtensions
{
    public static IEnumerable<string> ExtractText(this PdfPage page)
    {
        CSequence content = ContentReader.ReadContent(page);
        IEnumerable<string> text = content.ExtractText();
        return text;
    }

    public static IEnumerable<string> ExtractText(this CObject cObject)
    {
        if (cObject is COperator)
        {
            COperator? cOperator = cObject as COperator;
            if (cOperator.OpCode.Name == OpCodeName.Tj.ToString() ||
                cOperator.OpCode.Name == OpCodeName.TJ.ToString())
            {
                foreach (CObject cOperand in cOperator.Operands)
                foreach (string txt in ExtractText(cOperand))
                    yield return txt;
            }
        }
        else if (cObject is CSequence)
        {
            CSequence? cSequence = cObject as CSequence;
            foreach (CObject element in cSequence)
            foreach (string txt in ExtractText(element))
                yield return txt;
        }
        else if (cObject is CString)
        {
            CString? cString = cObject as CString;
            bool valid = !cString.Value.Any(char.IsControl);
            yield return valid ? cString.Value : "";
        }
    }
}