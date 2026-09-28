using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    // Vaccination stations (dbo.ClinicRooms).
    //
    // NOT USED since the beneficiary revision (Sep 2026): Leveriza has one
    // vaccination room and one person vaccinating, so the Nurse calls each
    // family in with Call Next (QueueController) instead of sending them to
    // a station. These endpoints and the ClinicRooms table are kept so older
    // databases and scripts keep working.
    //
    // ClinicRooms.AssignedDoctorID holds the stationed worker (any Doctor or
    // Nurse).
    [ApiController]
    [Route("api/[controller]")]
    public class VaccinationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuditService _audit;

        public VaccinationController(AppDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // GET /api/Vaccination/GetRooms
        // { roomId, roomName, workerId, doctorName, workerPosition, isOccupied,
        //   currentQueueId, currentQueueNumber, currentPatient }
        // `doctorName` keeps its old name so existing screens keep working.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("GetRooms")]
        public async Task<IActionResult> GetRooms()
        {
            // Room 2 before Room 10
            var rooms = (await _context.ClinicRooms.ToListAsync())
                .OrderBy(r => r.RoomNumber.Length).ThenBy(r => r.RoomNumber).ToList();

            var workerIds = rooms
                .Where(r => r.AssignedDoctorID.HasValue)
                .Select(r => r.AssignedDoctorID!.Value)
                .Distinct()
                .ToList();

            var workers = await _context.Users
                .Where(u => workerIds.Contains(u.UserID))
                .ToDictionaryAsync(u => u.UserID);

            // Today's visits currently at a station
            var today = DateTime.Today;
            var active = await _context.Queues
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .Where(q => q.QueueDate >= today && q.QueueDate < today.AddDays(1)
                            && q.Status == "InProgress" && q.AssignedRoomID != null)
                .ToListAsync();

            // A station is only busy while one of today's visits is still in
            // progress there. Free any left marked busy from an earlier day
            // (e.g. a visit nobody pressed "Mark Done" on).
            var stale = rooms.Where(r => (r.IsOccupied || r.CurrentChildID != null)
                                         && !active.Any(q => q.AssignedRoomID == r.RoomID)).ToList();
            if (stale.Count > 0)
            {
                foreach (var r in stale)
                {
                    r.IsOccupied = false;
                    r.CurrentChildID = null;
                }
                await _context.SaveChangesAsync();
            }

            var result = rooms.Select(r =>
            {
                User? worker = null;
                if (r.AssignedDoctorID.HasValue) workers.TryGetValue(r.AssignedDoctorID.Value, out worker);
                var visit = active.FirstOrDefault(q => q.AssignedRoomID == r.RoomID);

                return new
                {
                    roomId = r.RoomID,
                    roomName = r.RoomNumber,
                    workerId = worker?.UserID,
                    doctorName = worker != null ? $"{worker.FirstName} {worker.LastName}".Trim() : null,
                    workerPosition = worker?.Position,
                    isOccupied = r.IsOccupied,
                    currentQueueId = visit?.QueueID,
                    currentQueueNumber = visit?.QueueNumber,
                    currentPatient = visit != null
                        ? string.Join(", ", visit.QueueChildren.Select(qc => $"{qc.Child?.FirstName} {qc.Child?.LastName}".Trim()))
                        : null,
                };
            });

            return Ok(result);
        }

        // PUT /api/Vaccination/rooms/{roomId}/worker
        // Body: { userId }  (null = nobody at this station)
        // Puts a Doctor/Nurse at a station. A worker can only be at one
        // station at a time, so they're taken off any other (free) station.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPut("rooms/{roomId:int}/worker")]
        public async Task<IActionResult> SetWorker(int roomId, [FromBody] SetStationWorkerRequest request)
        {
            var room = await _context.ClinicRooms.FindAsync(roomId);
            if (room == null)
                return NotFound(new { message = "Station not found." });

            if (room.IsOccupied && room.AssignedDoctorID != request.UserId)
                return Conflict(new { message = "This station has a patient right now. Finish or reassign the visit before changing the health worker." });

            User? worker = null;
            if (request.UserId.HasValue)
            {
                worker = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId.Value);
                if (worker == null || (worker.Position != "Doctor" && worker.Position != "Nurse"))
                    return BadRequest(new { message = "Only a Doctor or Nurse can be assigned to a station." });

                var otherStations = await _context.ClinicRooms
                    .Where(r => r.RoomID != roomId && r.AssignedDoctorID == request.UserId)
                    .ToListAsync();

                if (otherStations.Any(r => r.IsOccupied))
                    return Conflict(new { message = $"{worker.FirstName} {worker.LastName} still has a patient at {otherStations.First(r => r.IsOccupied).RoomNumber}." });

                foreach (var other in otherStations) other.AssignedDoctorID = null;
            }

            // Who was here before, for the audit log's Old Value
            var previous = room.AssignedDoctorID.HasValue
                ? await _context.Users.FirstOrDefaultAsync(u => u.UserID == room.AssignedDoctorID.Value)
                : null;

            room.AssignedDoctorID = request.UserId;
            await _context.SaveChangesAsync();

            static string WorkerText(User? u) => u != null ? $"{u.Position} {u.FirstName} {u.LastName}" : "No health worker";

            await _audit.LogAsync("Queue", "Assign Station",
                $"Station {room.RoomNumber}",
                worker != null
                    ? $"Stationed {worker.Position} {worker.FirstName} {worker.LastName} at {room.RoomNumber}."
                    : $"Cleared the health worker from {room.RoomNumber}.",
                oldValue: WorkerText(previous),
                newValue: WorkerText(worker));

            return Ok(new { message = "Station updated.", roomId, workerId = request.UserId });
        }

        // ── Room list (System Administrator) ─────────────────────────────
        // The clinic's vaccination rooms: "Room 1", "Room 2", ...

        // POST /api/Vaccination/rooms   { name }
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPost("rooms")]
        public async Task<IActionResult> AddRoom([FromBody] RoomNameRequest request)
        {
            var (name, error) = await CheckRoomNameAsync(request.Name, null);
            if (error != null) return BadRequest(new { message = error });

            var room = new ClinicRoom { RoomNumber = name!, IsOccupied = false };
            _context.ClinicRooms.Add(room);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Queue", "Create", $"Room – {room.RoomNumber}", "Added a vaccination room.");
            return Ok(new { message = $"{room.RoomNumber} added.", roomId = room.RoomID, roomName = room.RoomNumber });
        }

        // PUT /api/Vaccination/rooms/{roomId}   { name }
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPut("rooms/{roomId:int}")]
        public async Task<IActionResult> RenameRoom(int roomId, [FromBody] RoomNameRequest request)
        {
            var room = await _context.ClinicRooms.FindAsync(roomId);
            if (room == null) return NotFound(new { message = "Room not found." });

            var (name, error) = await CheckRoomNameAsync(request.Name, roomId);
            if (error != null) return BadRequest(new { message = error });

            var old = room.RoomNumber;
            room.RoomNumber = name!;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Queue", "Update", $"Room – {room.RoomNumber}", "Renamed a vaccination room.",
                oldValue: old, newValue: room.RoomNumber);
            return Ok(new { message = "Room renamed.", roomId, roomName = room.RoomNumber });
        }

        // DELETE /api/Vaccination/rooms/{roomId}
        // Only when nobody is in it. Past visits keep their record without the room.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpDelete("rooms/{roomId:int}")]
        public async Task<IActionResult> DeleteRoom(int roomId)
        {
            var room = await _context.ClinicRooms.FindAsync(roomId);
            if (room == null) return NotFound(new { message = "Room not found." });

            var today = DateTime.Today;
            bool busy = await _context.Queues.AnyAsync(q => q.AssignedRoomID == roomId && q.Status == "InProgress"
                                                            && q.QueueDate >= today && q.QueueDate < today.AddDays(1));
            if (busy)
                return Conflict(new { message = $"{room.RoomNumber} has a patient right now. Try again after the visit is done." });

            if (await _context.ClinicRooms.CountAsync() <= 1)
                return Conflict(new { message = "The clinic needs at least one room." });

            await _context.Queues.Where(q => q.AssignedRoomID == roomId)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.AssignedRoomID, (int?)null));
            _context.ClinicRooms.Remove(room);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Queue", "Delete", $"Room – {room.RoomNumber}", "Removed a vaccination room.");
            return NoContent();
        }

        private async Task<(string? Name, string? Error)> CheckRoomNameAsync(string? raw, int? roomId)
        {
            var name = raw?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return (null, "Enter a room name, e.g. Room 4.");
            if (name.Length > 50) return (null, "Keep the room name under 50 characters.");
            if (await _context.ClinicRooms.AnyAsync(r => r.RoomNumber == name && r.RoomID != roomId))
                return (null, $"There is already a room called {name}.");
            return (name, null);
        }

        // POST /api/Vaccination/AssignRoom
        // Body: { queueId, roomId }
        // Sends a checked-in visit to a station: occupies the station and
        // marks the visit In Progress. The station must have a health
        // worker, since that worker is the one who will vaccinate.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost("AssignRoom")]
        public async Task<IActionResult> AssignRoom([FromBody] AssignRoomRequest request)
        {
            var room = await _context.ClinicRooms.FindAsync(request.RoomId);
            if (room == null)
                return NotFound(new { message = "Station not found." });

            if (!room.AssignedDoctorID.HasValue)
                return BadRequest(new { message = "No health worker is at this station yet. Assign a Doctor or Nurse to it first." });

            if (room.IsOccupied)
                return Conflict(new { message = "That station is already busy with another patient." });

            var queueEntry = await _context.Queues
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .FirstOrDefaultAsync(q => q.QueueID == request.QueueId);

            if (queueEntry == null)
                return NotFound(new { message = "Queue entry not found." });

            if (queueEntry.Status == "Completed")
                return BadRequest(new { message = "This visit is already completed." });

            // Free up any station this visit was previously sent to, so a
            // reassignment doesn't leave a stale station stuck as busy.
            if (queueEntry.AssignedRoomID.HasValue && queueEntry.AssignedRoomID != room.RoomID)
            {
                var oldRoom = await _context.ClinicRooms.FindAsync(queueEntry.AssignedRoomID.Value);
                if (oldRoom != null)
                {
                    oldRoom.IsOccupied = false;
                    oldRoom.CurrentChildID = null;
                }
            }

            room.IsOccupied = true;
            room.CurrentChildID = queueEntry.QueueChildren.FirstOrDefault()?.ChildID;

            queueEntry.Status = "InProgress";
            queueEntry.AssignedRoomID = room.RoomID;
            queueEntry.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Queue", "Assign Station",
                $"Queue #{queueEntry.QueueNumber} – {string.Join(", ", queueEntry.QueueChildren.Select(qc => qc.Child?.FirstName))}",
                $"Sent the patient to {room.RoomNumber}.");

            return Ok(new { message = "Assigned.", roomId = room.RoomID, queueId = queueEntry.QueueID });
        }

        // POST /api/Vaccination/CompleteSession/{roomId}
        // Staff "Mark Done": frees the station AND marks its visit Completed.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost("CompleteSession/{roomId}")]
        public async Task<IActionResult> CompleteSession(int roomId)
        {
            var room = await _context.ClinicRooms.FindAsync(roomId);
            if (room == null)
                return NotFound(new { message = "Station not found." });

            var queueEntry = await _context.Queues
                .FirstOrDefaultAsync(q => q.AssignedRoomID == roomId && q.Status == "InProgress");

            room.IsOccupied = false;
            room.CurrentChildID = null;

            if (queueEntry != null)
            {
                queueEntry.Status = "Completed";
                queueEntry.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Session completed.", roomId });
        }
    }

    public class AssignRoomRequest
    {
        public Guid QueueId { get; set; }
        public int RoomId { get; set; }
    }

    public class SetStationWorkerRequest
    {
        public Guid? UserId { get; set; }
    }

    public class RoomNameRequest
    {
        public string? Name { get; set; }
    }
}
