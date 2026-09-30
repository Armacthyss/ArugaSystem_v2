using AndroidWebAPI.Models;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Data
{
    public interface IVaccinationTimelineRepository
    {
        // CRUD
        Task<IEnumerable<VaccinationTimeline>> GetAllAsync();
        Task<VaccinationTimeline?> GetByIdAsync(Guid timelineId);
        Task<IEnumerable<VaccinationTimeline>> GetByChildAsync(Guid childId);

        Task AddAsync(VaccinationTimeline timeline);
        Task UpdateAsync(VaccinationTimeline timeline);
        Task DeleteAsync(Guid timelineId);

        Task<bool> ExistsAsync(Guid childId, int vaccineId, int doseNumber);

        // Business Logic (Current Sprint)
        Task GenerateTimelineAsync(Guid childId);

        Task MarkCompletedAsync(Guid timelineId);

        Task RegenerateTimelineAsync(Guid childId);

        // Recomputes every dose not yet given (after a birth-date correction)
        Task RescheduleChildAsync(Guid childId);

        // Builds the schedule of a child who has none yet (e.g. added straight
        // into the database); the second one does it for every such child.
        Task<bool> EnsureTimelineAsync(Guid childId);
        Task<int> EnsureAllTimelinesAsync();

        // Marks doses that have a vaccination record as given on the schedule.
        Task<int> LinkGivenDosesAsync();

        // Reschedules children whose upcoming doses fall on a closed day.
        Task<int> MoveDosesOffClosedDaysAsync();
        Task UpdateMissedVaccinationsAsync();

        Task<IEnumerable<VaccinationTimeline>> GetDueTodayAsync();

        Task<IEnumerable<VaccinationTimeline>> GetUpcomingAsync(int days);
        Task<TimelineSummaryDto> GetTimelineSummaryAsync(Guid childId);
    }
}   