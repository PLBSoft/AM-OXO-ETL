using System.Text.Json;

namespace ExcelETL.BlazorAdmin.Editing;

// Generic tools for the P3 "single draft owned by the root" architecture (lot 074,
// docs/tickets/tickets-tdd-lot-074-pilote-brouillon-editeur-profil-export.md). A draft is a plain
// mutable data object (no behavior) mirroring a Domain aggregate's shape one-to-one; these two
// functions let a page/component clone one (for a later "Cancel" restore) or check whether one is
// still in its just-constructed state (for "an empty pending row is ignored, not an error") without
// any code specific to a given draft type.
//
// System.Text.Json is used deliberately over a hand-written deep-copy/equality per draft type -- the
// whole point of this file is that adding a new field to a draft class needs zero changes here.
public static class DraftJson
{
    private static readonly JsonSerializerOptions Options = new();

    // Deep, independent copy: mutating the clone never touches the original (lists included).
    public static T Clone<T>(T value) where T : notnull
    {
        var json = JsonSerializer.Serialize(value, Options);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    // True when `value` is indistinguishable, by its own serialized content, from a fresh instance of
    // its type built via the parameterless constructor -- i.e. nothing has been typed/added into it
    // yet. A property marked [JsonIgnore] (e.g. a draft's own display-only Error field) never affects
    // this comparison, since System.Text.Json never serializes it in the first place.
    public static bool IsPristine<T>(T value) where T : notnull, new()
    {
        var fresh = new T();
        return JsonSerializer.Serialize(value, Options) == JsonSerializer.Serialize(fresh, Options);
    }
}
