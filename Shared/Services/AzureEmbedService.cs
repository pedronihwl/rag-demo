using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Logging;
using OpenAI;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Shared.Collections;

namespace Shared.Services;

public class AzureEmbedService(
    ILoggerFactory factory, 
    DocumentAnalysisClient documentAnalysisClient,
    OpenAIClient openAiClient,
    string modelName)
{
    private ILogger _logger = factory.CreateLogger("Embed");
    
    private const int BUFFER_SIZE = 1024;
    
    public delegate void ProgressChangedEventHandler(ProgressChangedEventArgs e);
    
    public event ProgressChangedEventHandler? ProgressChanged;
    
    public async Task<IReadOnlyList<PageDetail>> GetDocumentTextAsync(PdfPage page, int aux)
    {
        PdfDocument singlePageDocument = new PdfDocument();
        singlePageDocument.AddPage(page);

        using MemoryStream ms = new MemoryStream();
        singlePageDocument.Save(ms);

        ms.Position = 0;

        var operation = await documentAnalysisClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", ms);
        var result = await operation.WaitForCompletionAsync();
        
        var offset = 0;
        List<PageDetail> pageMap = [];

        var pages = result.Value.Pages;

        for (var i = 0; i < pages.Count; i++)
        {
            IReadOnlyList<DocumentTable> tablesOnPage = result.Value.Tables.Where(t => t.BoundingRegions[0].PageNumber == i + 1).ToList();
            
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
                    pageText.Append(result.Value.Content[pageIndex + j]);
                }
                else if (!addedTables.Contains(tableChars[j]))
                {
                    pageText.Append(TableToHtml(tablesOnPage[tableChars[j]]));
                    addedTables.Add(tableChars[j]);
                }
            }
            pageText.Append(' ');
            
            string text = CleanString(pageText.ToString());
            offset += text.Length;
            pageMap.Add(new PageDetail(aux, offset, text));
        }
        
        return pageMap.AsReadOnly();
    }

    private static string CleanString(string input)
    {
        input = Regex.Replace(input, @"(\r?\n)+", "\n");
        input = Regex.Replace(input, @"\s+", " ");

        input = input.Trim();

        return input.ToLower();
    }
    
    private static string TableToHtml(DocumentTable table)
    {
        var tableHtml = new StringBuilder("<table>");
        var rows = new List<DocumentTableCell>[table.RowCount];
        for (int i = 0; i < table.RowCount; i++)
        {
            var i1 = i;
            rows[i] =
            [
                .. table.Cells.Where(c => c.RowIndex == i1)
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
        const int SectionOverlap = 50;
        
        var sentenceEndings = new[] { '.', '!', '?' };
        var wordBreaks = new[] { ',', ';', ':', ' ', '(', ')', '[', ']', '{', '}', '\t', '\n' };
        var allText = string.Concat(pages.Select(p => p.Text));
        var length = allText.Length;
        var start = 0;
        var end = length;

        var indexes = pages.Select((p, i) => new
        {
            Number = p.Index,
            Size = pages.Take(i + 1).Aggregate(0, ((i1, p1) => i1 + p1.Offset))
        }).ToList();
        
        while (start + SectionOverlap < length)
        {
            var lastWord = -1;
            end = start + BUFFER_SIZE;
            
            if (end > length)
            {
                end = length;
            }
            else
            {
                while (end < length && (end - start - BUFFER_SIZE) < SentenceSearchLimit && !sentenceEndings.Contains(allText[end]))
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
            while (start > 0 && start > end - BUFFER_SIZE - (2 * SentenceSearchLimit) && !sentenceEndings.Contains(allText[start]))
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
            int index = indexes.First(item => item.Size >= start).Number;
            
            yield return new Fragment()
            {
                Context = file.Context,
                Index = index,
                Text = sectionText,
                File = file.Id,
                Offset = sectionText.Length
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
            int index = indexes.First(item => item.Size >= (start + end)).Number;
            
            yield return new Fragment()
            {
                Context = file.Context,
                Index = index,
                Text = txt,
                File = file.Id,
                Offset = txt.Length
            };
        }
    }

    private IEnumerable<Fragment> GetEmbeddings(IEnumerable<Fragment> fragments)
    {
        var embeddingClient = openAiClient.GetEmbeddingClient(modelName);
        foreach (var fragment in fragments)
        {
            var embedding = embeddingClient.GenerateEmbedding(fragment.Text);
            fragment.Embeddings = embedding.Value.Vector.ToArray();

            yield return fragment;
        }
    }

    private void Callback(FileCollection file, int percent)
    {
        ProgressChanged?.Invoke(new ProgressChangedEventArgs(percent, file));
    }
    
    public async Task<IReadOnlyList<Fragment>> EmbedPDFBlobAsync(Stream pdfBlobStream, FileCollection file)
    {
        using var document = PdfReader.Open(pdfBlobStream, PdfDocumentOpenMode.Import);

        file.Pages = document.Pages.Count;
        
        Callback(file, 1);

        List<PageDetail> detailedPages = [];

        _logger.LogInformation("1/3 Splitting .pdf into pages and extracting text with Document Intelligence");

        for (int i = 0; i < document.Pages.Count; i++)
        {
            var page = document.Pages[i];

            var convertedPages = (await GetDocumentTextAsync(page, i));
            detailedPages.AddRange(convertedPages);
            
            file.ProcessedPages = (i + 1);
            
            _logger.LogInformation("Processed pages {index}/{total} from file: {name}", (i + 1), file.Pages, file.Name);
            Callback(file, (int) Math.Ceiling((double) (i + 1) / file.Pages));
        }
        _logger.LogInformation("2/3 Chunking .pdf into fragments");
        var chunks = Chunks(detailedPages, file).ToList();

        file.Chunks = chunks.Count;
        Callback(file, 100);
        
        _logger.LogInformation("3/3 Embedding .pdf with OpenAi embeddings");
        var fragments = GetEmbeddings(chunks).ToList();

        return fragments;
    }
}