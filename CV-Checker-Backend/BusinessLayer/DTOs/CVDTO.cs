namespace BusinessLogic.DTOs
{
    public class CreateCVDTO
    {
        public Guid UserId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public Guid? TemplateId { get; set; }
        public string? Content { get; set; }
    }

    public class UpdateCVDTO
    {
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public Guid? TemplateId { get; set; }
        public string? Content { get; set; }
    }
}

