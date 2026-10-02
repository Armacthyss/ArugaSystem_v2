using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VaccineInventoryController : ControllerBase
    {
        private readonly VaccineInventoryRepository _repository;
        private readonly AndroidWebAPI.Services.AuditService _audit;

        public VaccineInventoryController(
            VaccineInventoryRepository repository,
            AndroidWebAPI.Services.AuditService audit)
        {
            _repository = repository;
            _audit = audit;
        }

        // ========================================
        // GET ALL
        // GET: api/VaccineInventory
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var inventory = await _repository.GetAllAsync();
            return Ok(inventory);
        }

        // ========================================
        // GET BY ID
        // GET: api/VaccineInventory/1
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var inventory = await _repository.GetByIdAsync(id);

            if (inventory == null)
                return NotFound();

            return Ok(inventory);
        }

        // ========================================
        // GET BY VACCINE
        // GET: api/VaccineInventory/vaccine/1
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("vaccine/{vaccineId}")]
        public async Task<IActionResult> GetByVaccine(int vaccineId)
        {
            var inventory = await _repository.GetByVaccineIdAsync(vaccineId);
            return Ok(inventory);
        }

        // ========================================
        // CREATE
        // POST: api/VaccineInventory
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] VaccineInventory inventory,
            [FromServices] AppDbContext context,
            [FromServices] AndroidWebAPI.Services.ParentNotifier notifier)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // A new batch: its doses are all still on hand
            if (string.IsNullOrWhiteSpace(inventory.LotNumber))
                return BadRequest(new { message = "Enter the lot number." });
            if (inventory.InitialQuantity <= 0)
                return BadRequest(new { message = "The number of doses received must be more than 0." });
            if (inventory.MinimumStock < 0)
                return BadRequest(new { message = "The minimum stock can't be negative." });
            if (inventory.ExpirationDate.Date < DateTime.Today)
                return BadRequest(new { message = "This batch has already expired. Check the expiration date." });
            if (inventory.ManufacturingDate is DateTime made && made.Date > inventory.ExpirationDate.Date)
                return BadRequest(new { message = "The manufacturing date must be before the expiration date." });
            inventory.LotNumber = inventory.LotNumber.Trim();
            inventory.CurrentQuantity = inventory.InitialQuantity;

            var created = await _repository.CreateAsync(inventory);

            await _audit.LogAsync("Inventory", "Receive Stock",
                $"Batch {created.LotNumber}",
                $"Received a new vaccine batch of {created.InitialQuantity} dose(s).",
                newValue: $"Remaining: {created.CurrentQuantity}");

            // Parents who were told this vaccine was out of stock hear right
            // away that it's back (instead of waiting for the next morning).
            await AndroidWebAPI.Services.StockNotices.RunAsync(context, notifier, created.VaccineID);

            return CreatedAtAction(
                nameof(Get),
                new { id = created.InventoryID },
                created);
        }

        // ========================================
        // WEEKLY STOCK CHECK
        // GET: api/VaccineInventory/stock-check
        // Per vaccine: on hand, due this week / two weeks, expiring soon,
        // re-stock yes/no and a suggested order (see Services/StockCheck.cs).
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("stock-check")]
        public async Task<IActionResult> GetStockCheck([FromServices] AppDbContext context, [FromServices] IConfiguration config)
        {
            var lines = await AndroidWebAPI.Services.StockCheck.BuildAsync(context);
            return Ok(new
            {
                checkDay = AndroidWebAPI.Services.StockCheck.CheckDay(config).ToString(),
                generatedAt = DateTime.Now,
                lines,
            });
        }

        // ========================================
        // INVENTORY SUMMARY
        // GET: api/VaccineInventory/summary?from=2026-09-01&to=2026-09-30
        // Per vaccine: on hand, received and used in the period, expiring,
        // expired, average weekly use and weeks of stock left
        // (see Services/InventorySummary.cs). Default period: this month.
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromServices] AppDbContext context,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? DateTime.Today;
            if (end < start) return BadRequest(new { message = "The end date is before the start date." });

            var lines = await AndroidWebAPI.Services.InventorySummary.BuildAsync(context, start, end);
            return Ok(new
            {
                from = start.Date,
                to = end.Date,
                generatedAt = DateTime.Now,
                totals = new
                {
                    onHand = lines.Sum(l => l.OnHand),
                    received = lines.Sum(l => l.ReceivedInPeriod),
                    used = lines.Sum(l => l.UsedInPeriod),
                    expiringSoon = lines.Sum(l => l.ExpiringSoon),
                    expiredOnShelf = lines.Sum(l => l.ExpiredOnShelf),
                    lowOrOut = lines.Count(l => l.Status != "Good"),
                },
                lines,
            });
        }

        // POST: api/VaccineInventory/stock-check/send
        // Sends the check to every Doctor and Nurse now
        // (it also goes out by itself every check day at 8:00 AM).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost("stock-check/send")]
        public async Task<IActionResult> SendStockCheck(
            [FromServices] AppDbContext context,
            [FromServices] AndroidWebAPI.Services.MessageSender sender)
        {
            int sent = await AndroidWebAPI.Services.StockCheck.SendAsync(context, sender);
            return Ok(new
            {
                sent,
                message = sent == 0
                    ? "Today's stock check was already sent."
                    : $"Stock check sent to {sent} staff/admin account(s).",
            });
        }

        // POST: api/VaccineInventory/alert-staff   { message }
        // The Admin / Doctor tells the Staff / Nurses to request or order
        // stock. Goes to every active Nurse's bell (low-stock alerts reach
        // the Doctor first; the Doctor decides what the staff should order).
        public class AlertStaffRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPost("alert-staff")]
        public async Task<IActionResult> AlertStaff(
            [FromServices] AppDbContext context,
            [FromBody] AlertStaffRequest request)
        {
            var text = (request?.Message ?? "").Trim();
            if (text.Length == 0)
                return BadRequest(new { message = "Please write what the staff should request or order." });
            if (text.Length > 500)
                return BadRequest(new { message = "Please keep the message under 500 characters." });

            var callerId = AndroidWebAPI.Services.AccessGuard.CallerId(User);
            var doctor = await context.Users.Where(u => u.UserID == callerId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim())
                .FirstOrDefaultAsync();

            var nurses = await context.Users
                .Where(u => u.AccountStatus == "Active" && (u.Position == "Nurse" || u.Position == "Staff"))
                .Select(u => u.UserID)
                .ToListAsync();
            if (nurses.Count == 0)
                return BadRequest(new { message = "There are no active Staff / Nurse accounts to alert." });

            foreach (var userId in nurses)
            {
                var n = new Notification
                {
                    NotificationID = Guid.NewGuid(),
                    UserID = userId,
                    Type = "StockRequest",
                    Title = $"Stock request from {(string.IsNullOrWhiteSpace(doctor) ? "the Doctor" : "Dr. " + doctor)}",
                    Message = text,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                };
                AndroidWebAPI.Services.ParentNotifier.FitToColumns(n);
                context.Notifications.Add(n);
            }
            await context.SaveChangesAsync();

            await _audit.LogAsync("Inventory", "Notify", "Staff / Nurses",
                $"Sent a stock request to {nurses.Count} staff: {text}", userId: callerId);

            return Ok(new { sent = nurses.Count, message = $"Alert sent to {nurses.Count} staff / nurse account(s)." });
        }

        // ========================================
        // UPDATE
        // PUT: api/VaccineInventory/1
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] VaccineInventory inventory)
        {
            // The batch ID is in the URL; a body without one means the same batch
            if (inventory.InventoryID == 0) inventory.InventoryID = id;
            if (id != inventory.InventoryID)
                return BadRequest("ID mismatch.");

            var existing = await _repository.GetByIdAsync(id);

            if (existing == null)
                return NotFound();

            // Dose counts only change when doses are given, so an edit made from
            // a page opened earlier can't undo a vaccination done in between.
            inventory.InitialQuantity = existing.InitialQuantity;
            inventory.CurrentQuantity = existing.CurrentQuantity;

            var updated = await _repository.UpdateAsync(inventory);

            await _audit.LogAsync("Inventory", "Adjust Inventory",
                $"Batch {inventory.LotNumber}",
                "Updated vaccine batch details.",
                oldValue: $"Remaining: {existing.CurrentQuantity}, Active: {existing.Status}",
                newValue: $"Remaining: {inventory.CurrentQuantity}, Active: {inventory.Status}");

            return Ok(updated);
        }

        // ========================================
        // DELETE
        // DELETE: api/VaccineInventory/1
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _repository.DeleteAsync(id);

            if (!success)
                return NotFound();

            await _audit.LogAsync("Inventory", "Delete", $"Inventory #{id}", "Deleted a vaccine batch record.");

            return Ok(new
            {
                message = "Inventory record deleted successfully."
            });
        }
    }
}