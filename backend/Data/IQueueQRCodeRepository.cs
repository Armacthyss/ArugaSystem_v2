using AndroidWebAPI.Models;

namespace AndroidWebAPI.Repositories
{
    public interface IQueueQRCodeRepository
    {
        Task<QueueQRCode?> GetTodayAsync(DateTime date);

        Task<QueueQRCode?> GetActiveAsync();

        Task<QueueQRCode> CreateAsync(QueueQRCode qrCode);

        Task UpdateAsync(QueueQRCode qrCode);

        Task DeactivateAllAsync();
    }
}