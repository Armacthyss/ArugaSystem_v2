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
            if (BatchDateProblem(inventory) is string problem)
                return BadRequest(new { message = problem });
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
                    removed = lines.Sum(l => l.RemovedInPeriod),
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

            // Same checks as receiving a batch (an expired batch can still be
            // edited, e.g. to deactivate it)
            if (string.IsNullOrWhiteSpace(inventory.LotNumber))
                return BadRequest(new { message = "Enter the lot number." });
            if (BatchDateProblem(inventory) is string problem)
                return BadRequest(new { message = problem });
            inventory.LotNumber = inventory.LotNumber.Trim();

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
        // ADJUST STOCK (Oct 2026)
        // POST: api/VaccineInventory/1/adjust
        //   { mode: "remove", quantity: 2, reason: "Broken vial", note: "..." }
        //   { mode: "count",  quantity: 137, note: "Wednesday stock check" }
        // Doses that can't be given (broken, spoiled, expired, lost) or a
        // recount. Doctors and Nurses both handle the vials, so both can.
        // ========================================
        public static readonly string[] RemoveReasons =
            { "Broken vial", "Spoiled (cold chain)", "Expired, thrown away", "Lost", "Other" };

        public class AdjustStockDto
        {
            public string Mode { get; set; } = "remove";   // "remove" | "count"
            public int Quantity { get; set; }
            public string? Reason { get; set; }
            public string? Note { get; set; }
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost("{id}/adjust")]
        public async Task<IActionResult> AdjustStock(int id, [FromBody] AdjustStockDto dto, [FromServices] AppDbContext context)
        {
            var batch = await context.VaccineInventory.Include(b => b.Vaccine).FirstOrDefaultAsync(b => b.InventoryID == id);
            if (batch == null) return NotFound(new { message = "Batch not found." });

            var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
            if (note?.Length > 255) return BadRequest(new { message = "The note is too long (255 characters at most)." });

            int before = batch.CurrentQuantity;
            int after;
            string reason;
            if (dto.Mode == "count")
            {
                if (dto.Quantity < 0 || dto.Quantity > batch.InitialQuantity)
                    return BadRequest(new { message = $"The count must be between 0 and {batch.InitialQuantity} (the doses this batch came with)." });
                if (dto.Quantity == before)
                    return BadRequest(new { message = $"{before} is already the count on file, so nothing changes." });
                after = dto.Quantity;
                reason = "Recount";
            }
            else
            {
                reason = RemoveReasons.FirstOrDefault(r => r == dto.Reason) ?? "";
                if (reason == "") return BadRequest(new { message = "Choose why the doses can't be used." });
                if (reason == "Other" && note == null) return BadRequest(new { message = "Write a short note saying what happened." });
                if (dto.Quantity < 1) return BadRequest(new { message = "Enter how many doses to take off." });
                if (dto.Quantity > before) return BadRequest(new { message = $"Only {before} dose(s) are on file for this batch." });
                after = before - dto.Quantity;
            }

            var me = AndroidWebAPI.Services.AccessGuard.CallerId(User);
            var user = me == null ? null : await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == me);
            var who = user == null ? null : $"{user.FirstName} {user.LastName}".Trim();

            batch.CurrentQuantity = after;
            batch.UpdatedAt = DateTime.Now;
            context.StockAdjustments.Add(new StockAdjustment
            {
                InventoryID = batch.InventoryID,
                Change = after - before,
                QuantityBefore = before,
                QuantityAfter = after,
                Reason = reason,
                Note = note,
                AdjustedByUserID = me,
                AdjustedByName = who,
                AdjustedAt = DateTime.Now,
            });
            await context.SaveChangesAsync();

            int change = after - before;
            await _audit.LogAsync("Inventory", "Adjust Stock",
                $"Batch {batch.LotNumber} ({batch.Vaccine?.Abbreviation ?? batch.Vaccine?.VaccineName})",
                (change < 0 ? $"Took off {-change} dose(s): {reason}." : $"Recount added {change} dose(s).") + (note != null ? $" {note}" : ""),
                oldValue: $"Remaining: {before}",
                newValue: $"Remaining: {after}");

            return Ok(new { inventoryID = batch.InventoryID, currentQuantity = after, change });
        }

        // GET: api/VaccineInventory/1/adjustments — newest first
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("{id}/adjustments")]
        public async Task<IActionResult> GetAdjustments(int id, [FromServices] AppDbContext context)
        {
            var rows = await context.StockAdjustments.AsNoTracking()
                .Where(a => a.InventoryID == id)
                .OrderByDescending(a => a.AdjustedAt)
                .Select(a => new { a.AdjustmentID, a.Change, a.QuantityBefore, a.QuantityAfter, a.Reason, a.Note, a.AdjustedByName, a.AdjustedAt })
                .ToListAsync();
            return Ok(rows);
        }

        // A batch can't be made in the future or after it expires
        private static string? BatchDateProblem(VaccineInventory batch)
        {
            if (batch.ManufacturingDate is not DateTime made) return null;
            if (made.Date > DateTime.Today)
                return "The manufacturing date can't be in the future.";
            if (made.Date > batch.ExpirationDate.Date)
                return "The manufacturing date must be before the expiration date.";
            return null;
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