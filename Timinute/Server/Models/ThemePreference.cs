namespace Timinute.Server.Models
{
    // System is first (CLR default = 0) so it aligns with the DB-default
    // configured in ApplicationDbContext. Without this alignment EF can't
    // distinguish "user set Theme = Light" from "Theme is unset" because
    // both look like the CLR default of the enum.
    public enum ThemePreference
    {
        System,
        Light,
        Dark
    }
}
