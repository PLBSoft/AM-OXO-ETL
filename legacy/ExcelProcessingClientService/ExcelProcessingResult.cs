using System;
using System.IO;

namespace Legacy.ExcelProcessingClientService
{
    /// <summary>
    /// The processed workbook streamed back synchronously by the Web API's
    /// POST api/excel/process response.
    /// </summary>
    public sealed class ExcelProcessingResult : IDisposable
    {
        public ExcelProcessingResult(string fileName, Stream content)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            FileName = fileName;
            Content = content;
        }

        public string FileName { get; private set; }

        public Stream Content { get; private set; }

        public void Dispose()
        {
            Content.Dispose();
        }
    }
}
