namespace ExcelETL.Application.Diagnostics;

public sealed record SystemLogEntry(int Id, DateTime TimestampUtc, string Level, string Message, string? Exception);
