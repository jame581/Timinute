namespace Timinute.Server.Models
{
    public interface ISoftDeletable
    {
        DateTimeOffset? DeletedAt { get; set; }
    }
}
