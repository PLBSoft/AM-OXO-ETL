namespace ExcelETL.WebAPI.Contracts;

// Same shape as the inline "errors" body OxoController's 422 response already returns -- a caller
// can reuse one deserialization type for both. Sheet/BlockIdentifier/Code/Message/ExtractedValue
// mirror GeneratedFileWarning (Domain), which is itself a snapshot of ExtractionError -- kept as a
// separate WebAPI-layer type rather than exposing the Domain record directly over HTTP, same
// boundary-separation convention as every other *Response type in this folder.
public sealed record GeneratedFileWarningResponse(
    string Sheet, string BlockIdentifier, string Code, string Message, string? ExtractedValue);
