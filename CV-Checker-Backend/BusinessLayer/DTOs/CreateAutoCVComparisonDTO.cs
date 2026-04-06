namespace BusinessLogic.DTOs
{
    public class CreateAutoCVComparisonDTO
    {
        public Guid CVId { get; set; }
        public Guid JobOfferId { get; set; }
        public Guid UserId { get; set; }
    }
}
