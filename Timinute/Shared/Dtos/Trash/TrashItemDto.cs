namespace Timinute.Shared.Dtos.Trash
{
    public class TrashItemDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public DateTimeOffset DeletedAt { get; set; }
        public int DaysRemaining { get; set; }
    }
}
