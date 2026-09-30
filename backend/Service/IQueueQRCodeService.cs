using AndroidWebAPI.Models;

namespace AndroidWebAPI.Services
{
    public interface IQueueQRCodeService
    {
        // Today's check-in code (made on first request). Error explains why
        // there is none, e.g. the clinic is closed today.
        Task<(QueueQRCode? Code, string? Error)> GenerateTodayAsync();

        Task<QueueQRCode?> GetActiveQRCodeAsync();

        // Accepts the long token inside the QR or the 6-character code
        // printed under it. Error says why it can't be used right now.
        Task<(bool Ok, string? Error)> ValidateAsync(string? code);
    }
}
