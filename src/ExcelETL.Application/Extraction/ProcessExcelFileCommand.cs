namespace ExcelETL.Application.Extraction;

public sealed record ProcessExcelFileCommand(Guid ExtractionConfigId, Stream FileStream, string SourceFileName);

public sealed record ProcessExcelFileResult(Stream GeneratedFileStream, string GeneratedFileName);
