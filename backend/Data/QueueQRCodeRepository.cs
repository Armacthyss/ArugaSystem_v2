using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Repositories
{
    public class QueueQRCodeRepository : IQueueQRCodeRepository
    {
        private readonly AppDbContext _context;

        public QueueQRCodeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<QueueQRCode?> GetTodayAsync(DateTime date)
        {
            return await _context.QueueQRCodes
                .FirstOrDefaultAsync(qr =>
                    qr.QRDate == date.Date);
        }

        public async Task<QueueQRCode?> GetActiveAsync()
        {
            return await _context.QueueQRCodes
                .FirstOrDefaultAsync(qr =>
                    qr.IsActive);
        }

        public async Task<QueueQRCode> CreateAsync(QueueQRCode qrCode)
        {
            _context.QueueQRCodes.Add(qrCode);

            await _context.SaveChangesAsync();

            return qrCode;
        }

        public async Task UpdateAsync(QueueQRCode qrCode)
        {
            _context.QueueQRCodes.Update(qrCode);

            await _context.SaveChangesAsync();
        }

        public async Task DeactivateAllAsync()
        {
            var activeCodes = await _context.QueueQRCodes
                .Where(qr => qr.IsActive)
                .ToListAsync();

            foreach (var qr in activeCodes)
            {
                qr.IsActive = false;
            }

            await _context.SaveChangesAsync();
        }
    }
}