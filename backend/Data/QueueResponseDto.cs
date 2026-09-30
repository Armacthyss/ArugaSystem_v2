namespace AndroidWebAPI.DTOs
{
    public class QueueResponseDto
    {
        public Guid QueueID { get; set; }
        public int QueueNumber { get; set; }
        public string? BarangayNo { get; set; }
        public string RequestBy { get; set; } = string.Empty;

        // How the person who checked in is related to the child(ren) in this
        // visit, e.g. "Grandmother", so staff know who brought the child.
        public string? RequestByRelationship { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime QueueDate { get; set; }

        // When the family checked in (Queues.CreatedAt)
        public DateTime CheckedInAt { get; set; }

        // Station the Admission Staff sent this visit to, and the health
        // worker currently stationed there. Null until a station is assigned.
        public int? AssignedRoomID { get; set; }
        public string? StationName { get; set; }
        public Guid? AssignedWorkerID { get; set; }
        public string? AssignedWorkerName { get; set; }

        public List<QueueChildResponseDto> Children { get; set; }
            = new List<QueueChildResponseDto>();
    }

    public class QueueChildResponseDto
    {
        public Guid ChildID { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}