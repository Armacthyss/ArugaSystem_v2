namespace AndroidWebAPI.DTOs
{
    // Body for PATCH /api/Children/{id}/health-notes.
    // Empty/null clears the field.
    public class UpdateHealthNotesDto
    {
        public string? Allergies { get; set; }
        public string? ExistingConditions { get; set; }
    }
}
