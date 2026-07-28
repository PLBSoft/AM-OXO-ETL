namespace ExcelETL.Application.Home;

// Lot 054 (54.2): one generic wrapper for all four home-page indicators, rather than a bespoke type
// per field -- Value is meaningless (default) unless State is Known.
public sealed record HomeIndicatorValue<T>
{
    public HomeIndicatorState State { get; }
    public T? Value { get; }

    private HomeIndicatorValue(HomeIndicatorState state, T? value)
    {
        State = state;
        Value = value;
    }

    public static HomeIndicatorValue<T> Known(T value) => new(HomeIndicatorState.Known, value);

    public static HomeIndicatorValue<T> Absent() => new(HomeIndicatorState.Absent, default);

    public static HomeIndicatorValue<T> Unavailable() => new(HomeIndicatorState.Unavailable, default);
}
