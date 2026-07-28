namespace ExcelETL.Application.Home;

// Lot 054 (54.2): three distinct states, deliberately never collapsed into a zero/null. "Known" is a
// successfully read value (including a legitimate zero count). "Absent" applies only to the last-
// generation date: no generated file has ever been archived, a normal state, not a failure.
// "Unavailable" is a failed read -- the underlying store threw.
public enum HomeIndicatorState
{
    Known,
    Absent,
    Unavailable
}
