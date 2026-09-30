using AndroidWebAPI.Models;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Data
{
    public interface IVaccinationRecordRepository
    {
        Task<IEnumerable<VaccinationRecord>> GetAllAsync();
        Task<VaccinationRecord?> GetByIdAsync(Guid vaccinationRecordId);
       Task<IEnumerable<VaccinationRecordResponseDto>> GetByChildAsync(Guid childId);

        Task AddAsync(VaccinationRecord record);
        Task UpdateAsync(VaccinationRecord record);
        Task DeleteAsync(Guid vaccinationRecordId);
        Task<VaccinationRecord> CompleteVaccinationAsync(Guid vaccinationRecordId, CompleteVaccinationDto dto);

        Task<bool> AlreadyVaccinatedAsync(Guid childId, int vaccineId, int doseNumber);
        // Business Logic
        Task RecordVaccinationAsync(VaccinationRecord record);
   
        Task RecordHistoricalVaccinationsAsync(
        HistoricalVaccinationSubmissionDto submission);
    }
}