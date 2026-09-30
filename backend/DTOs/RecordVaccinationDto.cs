using System;

namespace AndroidWebAPI.DTOs
{
    public class RecordVaccinationDto
    {
        public Guid ChildID { get; set; }

        public int VaccineID { get; set; }

        public int DoseNumber { get; set; }

        public DateTime VaccinationDate { get; set; }

        public int? InventoryID { get; set; }

        public Guid? AdministeredByUserID { get; set; }

        public string? NurseObservation { get; set; }

        // "Left thigh", "Right upper arm", "Mouth (oral)"... (Services/InjectionSites.cs)
        public string? InjectionSite { get; set; }
    }
}