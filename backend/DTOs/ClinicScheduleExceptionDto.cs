using System;

namespace AndroidWebAPI.DTOs
{
    public class ClinicScheduleExceptionDto
    {
        public DateTime ExceptionDate { get; set; }

        // Optional last date, to add the same exception to several days at
        // once (e.g. a typhoon closing the health center Mon–Wed). New
        // exceptions only; an edit changes one date.
        public DateTime? EndDate { get; set; }

        public bool IsOpen { get; set; }

        public TimeSpan OpeningTime { get; set; }

        public TimeSpan ClosingTime { get; set; }

        public TimeSpan QueueCutoffTime { get; set; }

        public string? Reason { get; set; }
    }
}