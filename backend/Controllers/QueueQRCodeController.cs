using AndroidWebAPI.Models;
using AndroidWebAPI.Repositories;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AndroidWebAPI.Controllers
{
    // Check-in QR for the clinic entrance. See QueueQRCodeService for how
    // the daily code works.
    [ApiController]
    [Route("api/[controller]")]
    public class QueueQRCodeController : ControllerBase
    {
        private readonly IQueueQRSettingRepository _settingRepository;
        private readonly IQueueQRCodeService _qrService;
        private readonly AuditService _audit;

        public QueueQRCodeController(
            IQueueQRSettingRepository settingRepository,
            IQueueQRCodeService qrService,
            AuditService audit)
        {
            _settingRepository = settingRepository;
            _qrService = qrService;
            _audit = audit;
        }

        // GET: api/QueueQRCode/settings
        // isEnabled = parents must scan the clinic QR (or type its code) to
        // check in from their phone. Staff can always add walk-ins by hand.
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var setting = await _settingRepository.GetAsync();

            return Ok(new
            {
                settingID = setting?.SettingID,
                isEnabled = setting?.IsEnabled ?? false,
                updatedAt = setting?.UpdatedAt
            });
        }

        // PUT: api/QueueQRCode/settings
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings(
            [FromBody] UpdateQueueQRSettingRequest request)
        {
            var setting =
                await _settingRepository.UpdateAsync(
                    request.IsEnabled);

            await _audit.LogAsync("Queue Management", "Update", "Check-in QR",
                request.IsEnabled
                    ? "Turned ON: parents must scan the clinic QR to check in."
                    : "Turned OFF: parents can check in without scanning the clinic QR.");

            return Ok(new
            {
                message = request.IsEnabled
                    ? "Parents now need to scan the clinic QR to check in."
                    : "Parents can now check in without scanning the clinic QR.",

                settingID = setting.SettingID,
                isEnabled = setting.IsEnabled,
                updatedAt = setting.UpdatedAt
            });
        }

        // POST: api/QueueQRCode/generate
        // Today's QR; the same one is returned all day.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateQRCode()
        {
            var (qrCode, error) = await _qrService.GenerateTodayAsync();

            // No code today (clinic closed, or past the cut-off) is a normal
            // answer, not an error: the page shows the reason.
            if (qrCode == null)
                return Ok(new { available = false, message = error });

            return Ok(Shape(qrCode));
        }

        // GET: api/QueueQRCode/active
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveQRCode()
        {
            var qrCode = await _qrService.GetActiveQRCodeAsync();

            if (qrCode == null)
                return NotFound(new { message = "There is no check-in QR for today yet." });

            return Ok(Shape(qrCode));
        }

        // GET: api/QueueQRCode/validate?code=ABC123
        // Used by the parent Check-in page right after scanning / typing.
        [HttpGet("validate")]
        public async Task<IActionResult> Validate([FromQuery] string? code)
        {
            var (ok, error) = await _qrService.ValidateAsync(code);
            if (!ok) return BadRequest(new { valid = false, message = error });
            return Ok(new { valid = true, message = "Welcome to Leveriza Health Center! Choose who you're checking in." });
        }

        private static object Shape(QueueQRCode qrCode) => new
        {
            available = true,
            qrCodeID = qrCode.QRCodeID,
            token = qrCode.Token,
            shortCode = qrCode.ShortCode,
            qrDate = qrCode.QRDate,
            validFrom = qrCode.ValidFrom,
            validUntil = qrCode.ValidUntil,
            isActive = qrCode.IsActive
        };
    }

    public class UpdateQueueQRSettingRequest
    {
        public bool IsEnabled { get; set; }
    }
}
