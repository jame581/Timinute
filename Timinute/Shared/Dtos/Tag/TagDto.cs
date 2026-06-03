namespace Timinute.Shared.Dtos.Tag
{
    public class TagDto
    {
        public string TagId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int TaskCount { get; set; }
    }
}
