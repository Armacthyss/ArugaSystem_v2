using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;

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

        // POST: api/VaccineInventory/stock-check/send
        // Sends the check to the Admission Staff and Administrator now
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