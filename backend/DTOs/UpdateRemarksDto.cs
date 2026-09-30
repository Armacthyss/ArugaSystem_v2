namespace AndroidWebAPI.DTOs
{
    // Body for PATCH /api/VaccinationRecords/{id}/remarks.
    // Empty/null Remarks clears them.
    public class UpdateRemarksDto
    {
        public string? Remarks { get; set; }
        public Guid? UpdatedByUserID { get; set; }
    }
}
