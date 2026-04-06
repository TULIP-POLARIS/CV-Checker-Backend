using Microsoft.AspNetCore.Http;

namespace BusinessLogic.DTOs
{
    public class UploadCVPdfDTO
    {
        public Guid UserId { get; set; }
        public Guid? TemplateId { get; set; }
        public IFormFile? File { get; set; }
    }
}
