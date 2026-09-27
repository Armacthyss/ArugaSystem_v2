using System.Security.Cryptography;
using AndroidWebAPI.Models;
using AndroidWebAPI.Repositories;

namespace AndroidWebAPI.Services
{
    // Daily check-in QR (Figure 15 of the study).
    //
    // The Admission Staff show today's QR at the entrance (Staff Dashboard >
    // Check-in QR). Parents scan it with their phone, or type the 6-character
    // code printed under it, to join the queue from the Check-in page. A new
    // code is made each clinic day and only works from opening time until the
    // queue cut-off, so an old screenshot can't be used to queue from home.
    public class QueueQRCodeService : IQueueQRCodeService
    {
        // No 0/O or 1/I/L, so the code can't be misread when typed in
        private const string ShortCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        private readonly IQueueQRCodeRepository _qrRepository;
        private readonly IClinicOperatingScheduleRepository _scheduleRepository;

        public QueueQRCodeService(
            IQueueQRCodeRepository qrRepository,
            IClinicOperatingScheduleRepository scheduleRepository)
        {
            _qrRepository = qrRepository;
            _scheduleRepository = scheduleRepository;
        }

        public async Task<(QueueQRCode? Code, string? Error)> GenerateTodayAsync()
        {
            var now = DateTime.Now;
            var today = now.Date;

            // 1. Today's code already exists -> same code all day
            var existing = await _qrRepository.GetTodayAsync(today);
            if (existing != null)
                return (await WithCurrentHoursAsync(existing), null);

            // 2. Clinic hours for today (a schedule exception wins)
            var hours = await GetHoursAsync(today);
            if (hours == null)
                return (null, $"There are no vaccinations today ({today:dddd, MMMM d}), so there is no check-in QR. " +
                              "If vaccinations are given today, add today under Operating Hours first.");

            var (open, cutoff) = hours.Value;
            if (now.TimeOfDay > cutoff)
                return (null, $"Check-in for today closed at {Time(cutoff)}.");

            // 3. Make today's code
            var qrCode = new QueueQRCode
            {
                QRCodeID = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString("N"),
                ShortCode = MakeShortCode(),
                QRDate = today,
                ValidFrom = today.Add(open),
                ValidUntil = today.Add(cutoff),
                IsActive = true,
                CreatedAt = now
            };

            await _qrRepository.DeactivateAllAsync();
            return (await _qrRepository.CreateAsync(qrCode), null);
        }

        public async Task<QueueQRCode?> GetActiveQRCodeAsync()
        {
            var active = await _qrRepository.GetActiveAsync();
            return active != null && active.QRDate.Date == DateTime.Today ? await WithCurrentHoursAsync(active) : null;
        }

        // A code keeps the hours it was made with, but the administrator may
        // change today's hours afterwards (e.g. add an Operating Hours
        // exception to open late). Keep the code's window in step with them.
        private async Task<QueueQRCode> WithCurrentHoursAsync(QueueQRCode code)
        {
            var hours = await GetHoursAsync(code.QRDate.Date);
            if (hours == null) return code;

            var (open, cutoff) = hours.Value;
            var from = code.QRDate.Date.Add(open);
            var until = code.QRDate.Date.Add(cutoff);
            if (code.ValidFrom != from || code.ValidUntil != until)
            {
                code.ValidFrom = from;
                code.ValidUntil = until;
                await _qrRepository.UpdateAsync(code);
            }
            return code;
        }

        public async Task<(bool Ok, string? Error)> ValidateAsync(string? code)
        {
            code = ExtractCode(code);
            if (string.IsNullOrEmpty(code))
                return (false, "Scan the QR code at the clinic entrance, or type the code printed under it.");

            var today = await _qrRepository.GetTodayAsync(DateTime.Today);
            bool matches = today != null &&
                (string.Equals(today.Token, code, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(today.ShortCode, code, StringComparison.OrdinalIgnoreCase));

            if (!matches || today == null || !today.IsActive)
                return (false, "That isn't today's check-in code. Please scan the QR code posted at the clinic today.");

            if (await GetHoursAsync(DateTime.Today) == null)
                return (false, "There are no vaccinations today. Please come back on the next vaccination day.");
            today = await WithCurrentHoursAsync(today);

            var now = DateTime.Now;
            if (now < today.ValidFrom)
                return (false, $"Check-in opens at {today.ValidFrom:h:mm tt}.");
            if (now > today.ValidUntil)
                return (false, $"Check-in for today closed at {today.ValidUntil:h:mm tt}. Please come back on the next vaccination day.");

            return (true, null);
        }

        // The QR holds a link like https://…/ParentCheckin?code=TOKEN. Accept
        // that whole link, the token, or the short code.
        public static string ExtractCode(string? raw)
        {
            raw = raw?.Trim() ?? "";
            var at = raw.IndexOf("code=", StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                raw = raw[(at + 5)..];
                var end = raw.IndexOfAny(new[] { '&', '#' });
                if (end >= 0) raw = raw[..end];
                raw = Uri.UnescapeDataString(raw);
            }
            return raw.Replace(" ", "").Replace("-", "");
        }

        private async Task<(TimeSpan Open, TimeSpan Cutoff)?> GetHoursAsync(DateTime date)
        {
            var exception = await _scheduleRepository.GetExceptionAsync(date);
            if (exception != null)
                return exception.IsOpen ? (exception.OpeningTime, exception.QueueCutoffTime) : null;

            var schedule = await _scheduleRepository.GetByDayAsync((int)date.DayOfWeek);
            if (schedule == null || !schedule.IsOpen) return null;
            return (schedule.OpeningTime, schedule.QueueCutoffTime);
        }

        private static string MakeShortCode()
        {
            var chars = new char[6];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = ShortCodeAlphabet[RandomNumberGenerator.GetInt32(ShortCodeAlphabet.Length)];
            return new string(chars);
        }

        private static string Time(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt");
    }
}
