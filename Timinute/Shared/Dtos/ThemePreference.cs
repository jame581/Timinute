using System.Text.Json.Serialization;

namespace Timinute.Shared.Dtos
{
    // Lives in Shared so both server (UserPreferences entity) and client
    // (ThemeService, Profile UI) can reference the same type.
    //
    // System is first (CLR default = 0) so it aligns with the DB-default
    // configured in ApplicationDbContext. Without this alignment EF can't
    // distinguish "user set Theme = Light" from "Theme is unset" because
    // both look like the CLR default of the enum.
    //
    // [JsonConverter] attached to the type so both server and client
    // serialize/deserialize as strings ("Light"/"Dark"/"System") without
    // each project needing its own JsonStringEnumConverter registration.
    // Without this the server emitted string but the client (no global
    // converter) tried to deserialize as int -> JsonException -> Profile
    // page silently fell back to "Could not load profile.".
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ThemePreference
    {
        System,
        Light,
        Dark
    }
}
