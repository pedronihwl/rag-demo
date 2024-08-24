using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Shared.Collections;
using Shared.Options;

namespace Shared.Services;

public class AzureEmbedService(
    ILoggerFactory factory, 
    IOptions<AzureEmbedOptions> options,
    DocumentAnalysisClient documentAnalysisClient)
{
    private ILogger _logger = factory.CreateLogger("Embed");
    private AzureEmbedOptions Options { get; } = options.Value;
    
    private int Buffer { get; set; }
    
    public delegate void ProgressChangedEventHandler(object sender, ProgressChangedEventArgs e);
    
    public event ProgressChangedEventHandler? ProgressChanged;
    
    public async Task<IReadOnlyList<PageDetail>> GetDocumentTextAsync(PdfPage page)
    {
        using var ms = new MemoryStream(page.Stream.Value);
        
        var operation = await documentAnalysisClient.AnalyzeDocumentAsync(WaitUntil.Started, "prebuilt-layout", ms);
        var results = await operation.WaitForCompletionAsync();
        
        var offset = 0;
        List<PageDetail> pageMap = [];

        var pages = results.Value.Pages;

        for (var i = 0; i < pages.Count; i++)
        {
            IReadOnlyList<DocumentTable> tablesOnPage =
                results.Value.Tables.Where(t => t.BoundingRegions[0].PageNumber == i + 1).ToList();
            
            int pageIndex = pages[i].Spans[0].Index;
            int pageLength = pages[i].Spans[0].Length;
            int[] tableChars = Enumerable.Repeat(-1, pageLength).ToArray();
            for (var tableId = 0; tableId < tablesOnPage.Count; tableId++)
            {
                foreach (DocumentSpan span in tablesOnPage[tableId].Spans)
                {
                    for (var j = 0; j < span.Length; j++)
                    {
                        int index = span.Index - pageIndex + j;
                        if (index >= 0 && index < pageLength)
                        {
                            tableChars[index] = tableId;
                        }
                    }
                }
            }
            
            StringBuilder pageText = new();
            HashSet<int> addedTables = [];
            for (int j = 0; j < tableChars.Length; j++)
            {
                if (tableChars[j] == -1)
                {
                    pageText.Append(results.Value.Content[pageIndex + j]);
                }
                else if (!addedTables.Contains(tableChars[j]))
                {
                    pageText.Append(TableToHtml(tablesOnPage[tableChars[j]]));
                    addedTables.Add(tableChars[j]);
                }
            }

            string text = CleanString(pageText.ToString());
            pageText.Append(' ');
            pageMap.Add(new PageDetail(i, offset, pageText.ToString()));
            offset += text.Length;
        }
        
        return pageMap.AsReadOnly();
    }
    
    public static string CleanString(string input)
    {
        string pattern = @"(\r?\n){3,}|\s{2,}";

        string cleanedString = Regex.Replace(input, pattern, match => 
        {
            if (match.Value.StartsWith($"\r") || match.Value.StartsWith("\n"))
            {
                return "\n\n"; 
            }
            return " "; 
        });
        
        cleanedString = cleanedString.Trim();

        return cleanedString.ToLower();
    }
    
    private static string TableToHtml(DocumentTable table)
    {
        var tableHtml = new StringBuilder("<table>");
        var rows = new List<DocumentTableCell>[table.RowCount];
        for (int i = 0; i < table.RowCount; i++)
        {
            rows[i] =
            [
                .. table.Cells.Where(c => c.RowIndex == i)
                    .OrderBy(c => c.ColumnIndex)
                ,
            ];
        }

        foreach (var rowCells in rows)
        {
            tableHtml.Append("<tr>");
            foreach (DocumentTableCell cell in rowCells)
            {
                var tag = (cell.Kind == "columnHeader" || cell.Kind == "rowHeader") ? "th" : "td";
                var cellSpans = string.Empty;
                if (cell.ColumnSpan > 1)
                {
                    cellSpans += $" colSpan='{cell.ColumnSpan}'";
                }

                if (cell.RowSpan > 1)
                {
                    cellSpans += $" rowSpan='{cell.RowSpan}'";
                }

                tableHtml.AppendFormat(
                    "<{0}{1}>{2}</{0}>", tag, cellSpans, WebUtility.HtmlEncode(cell.Content));
            }

            tableHtml.Append("</tr>");
        }

        tableHtml.Append("</table>");

        return tableHtml.ToString();
    }


    private IEnumerable<Fragment> Chunks(IReadOnlyList<PageDetail> pages, FileCollection file)
    {
        const int SentenceSearchLimit = 100;
        const int SectionOverlap = 100;

        var sentenceEndings = new[] { '.', '!', '?' };
        var wordBreaks = new[] { ',', ';', ':', ' ', '(', ')', '[', ']', '{', '}', '\t', '\n' };
        var allText = string.Concat(pages.Select(p => p.Text));
        var length = allText.Length;
        var start = 0;
        var end = length;
        
        while (start + SectionOverlap < length)
        {
            var lastWord = -1;
            end = start + Options.ChunkSize;

            if (end > length)
            {
                end = length;
            }
            else
            {
                while (end < length && (end - start - Options.ChunkSize) < SentenceSearchLimit && !sentenceEndings.Contains(allText[end]))
                {
                    if (wordBreaks.Contains(allText[end]))
                    {
                        lastWord = end;
                    }
                    end++;
                }

                if (end < length && !sentenceEndings.Contains(allText[end]) && lastWord > 0)
                {
                    end = lastWord;
                }
            }

            if (end < length)
            {
                end++;
            }
            
            lastWord = -1;
            while (start > 0 && start > end - Options.ChunkSize - (2 * SentenceSearchLimit) && !sentenceEndings.Contains(allText[start]))
            {
                if (wordBreaks.Contains(allText[start]))
                {
                    lastWord = start;
                }
                start--;
            }

            if (!sentenceEndings.Contains(allText[start]) && lastWord > 0)
            {
                start = lastWord;
            }
            if (start > 0)
            {
                start++;
            }

            var sectionText = allText[start..end];

            string txt = allText[start..end];
            
            yield return new Fragment()
            {
                Context = file.Context,
                Index = FindPage(pages, start),
                Text = txt,
                File = file.Id,
                Offset = txt.Length
            };

            var lastTableStart = sectionText.LastIndexOf("<table", StringComparison.Ordinal);
            
            if (lastTableStart > 2 * SentenceSearchLimit && lastTableStart > sectionText.LastIndexOf("</table", StringComparison.Ordinal))
            {
                start = Math.Min(end - SectionOverlap, start + lastTableStart);
            }
            else
            {
                start = end - SectionOverlap;
            }
        }
        
        if (start + SectionOverlap < end)
        {
            string txt = allText[start..end];
            yield return new Fragment()
            {
                Context = file.Context,
                Index = FindPage(pages, start),
                Text = txt,
                File = file.Id,
                Offset = txt.Length
            };
        }
    }
    
    private static int FindPage(IReadOnlyList<PageDetail> pageMap, int offset)
    {
        var length = pageMap.Count;
        for (var i = 0; i < length - 1; i++)
        {
            if (offset >= pageMap[i].Offset && offset < pageMap[i + 1].Offset)
            {
                return i;
            }
        }

        return length - 1;
    }
    
    public async Task<bool> EmbedPDFBlobAsync(Stream pdfBlobStream, FileCollection file)
    {
        using var document = PdfReader.Open(pdfBlobStream, PdfDocumentOpenMode.Import);
        _logger.LogInformation("Start embedding {name} with {pages} from {ctx}", file.Name, document.Pages.Count, file.Context);

        file.Pages = document.Pages.Count;
        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(1,file));

        int processedPages = 0;

        var tasks = new Task<IReadOnlyList<PageDetail>>[document.Pages.Count];
        foreach (var page in document.Pages)
        {
            tasks[processedPages] = GetDocumentTextAsync(page).ContinueWith(t =>
            {
                if (t.IsFaulted) return t.Result;
                
                file.ProcessedPages = ++processedPages;
                _logger.LogInformation("Processed {processed}/{total} from {fileName}", processedPages, file.Pages, file.Name);
                    
                ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(
                    (int)Math.Ceiling((double)file.ProcessedPages / file.Pages), file));

                return t.Result;
            });
        }

        var pages = (await Task.WhenAll(tasks)).SelectMany(item => item).ToList();

        var fragments = Chunks(pages, file);

        return true;
    }
}