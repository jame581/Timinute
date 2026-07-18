namespace Timinute.Server.Services.Pat
{
    public interface IPatTokenService
    {
        const string TokenPrefix = "tmn_pat_";
        (string plaintext, string hash, string prefix) Generate();
        string Hash(string plaintext);
        bool FixedTimeEquals(string hashA, string hashB);
    }
}
