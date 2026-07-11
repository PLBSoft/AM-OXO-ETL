using System.IO;
using System.Web;

namespace Legacy.ExcelProcessingClientService.Tests
{
    // HttpPostedFileBase is abstract and sealed away from direct construction, so tests
    // stand in with this minimal double instead of spinning up a real ASP.NET MVC request.
    internal class FakeHttpPostedFile : HttpPostedFileBase
    {
        private readonly Stream _inputStream;

        public FakeHttpPostedFile(string fileName, string contentType, Stream inputStream)
        {
            FileNameOverride = fileName;
            ContentTypeOverride = contentType;
            _inputStream = inputStream;
        }

        public string FileNameOverride { get; set; }

        public string ContentTypeOverride { get; set; }

        public override string FileName
        {
            get { return FileNameOverride; }
        }

        public override string ContentType
        {
            get { return ContentTypeOverride; }
        }

        public override int ContentLength
        {
            get { return (int)_inputStream.Length; }
        }

        public override Stream InputStream
        {
            get { return _inputStream; }
        }
    }
}
