namespace ExcelETL.Application.Extraction.Oxo.Elements;

// Lot 084: the names ElementSheetExtractionService knows. Block fields fill the IsolementPivot;
// header fields give the equipment repère and the zone. Every other field declared in a block is
// only read by the point rules.
public static class ElementFieldNames
{
    public const string Identification = "Identification";
    public const string Designation = "Designation";
    public const string TypeElement = "TypeElement";
    public const string PositionALaPose = "PositionALaPose";
    public const string CouleurEtiquette = "CouleurEtiquette";

    public const string RepereEchoHeader = SharedHeaderFieldNames.RepereEcho;
    public const string ZoneHeader = "zone";
}
