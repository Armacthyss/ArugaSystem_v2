using AndroidWebAPI.Models;
using AndroidWebAPI.Repositories;
using AndroidWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly IQueueRepository _queueRepository;
        private readonly AppDbContext _context;

        public QueueController(IQueueRepository queueRepository, AppDbContext context)
        {
            _queueRepository = queueRepository;
            _context = context;
        }

        // ============================================================
        // DOCTOR DASHBOARD BOARD — added for the Doctor/Nurse module.
        // Operates on the same Queues/QueueChildren tables as the rest of
        // this controller, going through _context directly (EF) instead
        // of IQueueRepository since these need shapes/updates the existing
        // repository methods don't provide (a flattened per-child board,
        // and "call next"/"complete" state transitions).
        //
        // A Queues row is one parent visit and can have several
        // QueueChildren. The board below shows one row per child, but
        // call-next/complete act on the whole visit (Queues.Status) —
        // completing marks every child in that visit as done together.
        // ============================================================

        // Waiting, or called into the room and not finished yet
        private static bool IsOpen(Queue q) =>
            q.Status == "Waiting" || q.Status == "InProgress" || q.Status == "In Progress";

        private static DateTime TodayStart => DateTime.Today;
        private static DateTime TodayEnd => DateTime.Today.AddDays(1);

        // GET /api/Queue/board
        // Polled by the Doctor dashboard to show who's waiting / in progress.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("board")]
        public async Task<IActionResult> GetBoard()
        {
            var queues = await _context.Queues
                .Include(q => q.Parent)
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .Where(q => q.QueueDate >= TodayStart && q.QueueDate < TodayEnd
                            && (q.Status == "Waiting" || q.Status == "InProgress"))
                .OrderBy(q => q.QueueNumber)
                .ToListAsync();

            var board = queues
                .SelectMany(q => q.QueueChildren.DefaultIfEmpty(), (q, qc) => new
                {
                    QueueID = q.QueueID,
                    QueueNumber = q.QueueNumber,
                    QueueStatus = q.Status,
                    AssignedRoomID = q.AssignedRoomID,
                    ChildId = qc != null ? (Guid?)qc.ChildID : null,
                    ChildName = qc?.Child != null ? $"{qc.Child.FirstName} {qc.Child.LastName}" : null,
                    ParentName = q.Parent != null ? $"{q.Parent.FirstName} {q.Parent.LastName}" : null,
                    // Not tracked per-child in this schema yet — the Doctor
                    // modal falls back to a manual pick when these are null.
                    RecordId = (Guid?)null,
                    VaccineName = (string?)null,
                    DoseNumber = (int?)null,
                });

            return Ok(board);
        }

        // ── Call Next ────────────────────────────────────────────────
        // Leveriza has one vaccination room and one person vaccinating. The
        // Nurse presses "Call Next": the next family in line becomes
        // "InProgress" (= called into the room) and the parent who checked
        // in is alerted on their phone (a banner with a sound on the
        // Check-in page, an in-app notification, and one SMS).

        // PATCH /api/Queue/call-next
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPatch("call-next")]
        public async Task<IActionResult> CallNext(
            [FromServices] AndroidWebAPI.Services.ParentNotifier notifier,
            [FromServices] AndroidWebAPI.Services.AuditService audit)
        {
            var next = await _context.Queues
                .Where(q => q.QueueDate >= TodayStart && q.QueueDate < TodayEnd && q.Status == "Waiting")
                .OrderBy(q => q.QueueNumber)
                .FirstOrDefaultAsync();

            if (next == null) return NotFound(new { message = "Nobody is waiting right now." });
            return await CallAsync(next, notifier, audit);
        }

        // PATCH /api/Queue/{queueId}/call
        // Calls one family out of order (e.g. the next in line stepped out),
        // or, for the family already in the room, alerts them again
        // ("Call Again": in-app alert only, no second SMS).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPatch("{queueId:guid}/call")]
        public async Task<IActionResult> Call(
            Guid queueId,
            [FromServices] AndroidWebAPI.Services.ParentNotifier notifier,
            [FromServices] AndroidWebAPI.Services.AuditService audit)
        {
            var visit = await _context.Queues.FirstOrDefaultAsync(q => q.QueueID == queueId);
            if (visit == null) return NotFound(new { message = "Queue entry not found." });
            if (visit.QueueDate < TodayStart || visit.QueueDate >= TodayEnd)
                return BadRequest(new { message = "Only today's queue can be called." });
            if (visit.Status == "Completed")
                return BadRequest(new { message = "This visit is already completed." });
            return await CallAsync(visit, notifier, audit);
        }

        private async Task<IActionResult> CallAsync(
            Queue visit,
            AndroidWebAPI.Services.ParentNotifier notifier,
            AndroidWebAPI.Services.AuditService audit)
        {
            bool again = visit.Status == "InProgress";

            // One family in the room at a time
            if (!again)
            {
                var inRoom = await _context.Queues
                    .Where(q => q.QueueDate >= TodayStart && q.QueueDate < TodayEnd
                                && q.Status == "InProgress" && q.QueueID != visit.QueueID)
                    .Select(q => q.QueueNumber)
                    .FirstOrDefaultAsync();
                if (inRoom != 0)
                    return Conflict(new { message = $"Queue #{inRoom} is still in the vaccination room. Press Complete Visit for them first." });
            }

            visit.Status = "InProgress";
            visit.UpdatedAt = DateTime.Now;

            var parent = await _context.Parents.FirstOrDefaultAsync(p => p.ParentID == visit.ParentID);
            var children = await _context.QueueChildren
                .Where(qc => qc.QueueID == visit.QueueID)
                .Select(qc => qc.Child!.FirstName)
                .ToListAsync();
            string names = children.Count == 0 ? "your child" : string.Join(" and ", children);

            if (parent != null)
            {
                var tally = new AndroidWebAPI.Services.ParentNotifier.Delivery();
                await notifier.NotifyAsync(parent, new Notification
                {
                    Type = "Called",
                    Title = $"Queue #{visit.QueueNumber}: it's your turn",
                    Message = $"Please bring {names} inside the vaccination room now.",
                    ScheduledDate = DateTime.Today,
                    IsRead = false,
                }, tally,
                email: false,
                sms: !again,
                smsText: $"Leveriza Health Center: Queue #{visit.QueueNumber}, it's your turn. Please bring {names} inside the vaccination room now.");
            }

            await _context.SaveChangesAsync();

            await audit.LogAsync("Queue", again ? "Call Again" : "Call Next",
                $"Queue #{visit.QueueNumber} – {string.Join(", ", children)}",
                again ? "Alerted the family again to come into the vaccination room." : "Called the family into the vaccination room.");

            return Ok(new
            {
                message = again ? $"Queue #{visit.QueueNumber} was alerted again." : $"Queue #{visit.QueueNumber} was called in.",
                queueID = visit.QueueID,
                queueNumber = visit.QueueNumber,
            });
        }

        // PATCH /api/Queue/{queueId}/back-to-waiting
        // The called family didn't come in (e.g. stepped out): put them back
        // in line so the Nurse can call the next one.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPatch("{queueId:guid}/back-to-waiting")]
        public async Task<IActionResult> BackToWaiting(Guid queueId, [FromServices] AndroidWebAPI.Services.AuditService audit)
        {
            var visit = await _context.Queues.FirstOrDefaultAsync(q => q.QueueID == queueId);
            if (visit == null) return NotFound(new { message = "Queue entry not found." });
            if (visit.Status != "InProgress")
                return BadRequest(new { message = "Only the family in the room can be sent back to waiting." });

            visit.Status = "Waiting";
            visit.AssignedRoomID = null;
            visit.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await audit.LogAsync("Queue", "Update", $"Queue #{visit.QueueNumber}",
                "Sent the family back to waiting (they did not come in when called).");
            return Ok(new { message = $"Queue #{visit.QueueNumber} is waiting again." });
        }

        // PATCH /api/Queue/{queueId}/complete
        // "Complete Visit": marks the visit done so the Nurse can call the
        // next family, and texts each child's parents what was given.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPatch("{queueId}/complete")]
        public async Task<IActionResult> Complete(Guid queueId, [FromServices] AndroidWebAPI.Services.ParentNotifier notifier)
        {
            var entry = await _context.Queues.FindAsync(queueId);
            if (entry == null) return NotFound();

            bool wasCompleted = entry.Status == "Completed";
            entry.Status = "Completed";
            entry.UpdatedAt = DateTime.Now;

            if (entry.AssignedRoomID.HasValue)
            {
                var room = await _context.ClinicRooms.FindAsync(entry.AssignedRoomID.Value);
                if (room != null)
                {
                    room.IsOccupied = false;
                    room.CurrentChildID = null;
                }
            }

            await _context.SaveChangesAsync();

            // One text per child: vaccines given today and the next date
            if (!wasCompleted)
                await AndroidWebAPI.Services.VisitSummary.SendAsync(_context, notifier, entry.QueueID);

            return Ok(new { message = "Visit completed.", queueID = entry.QueueID });
        }

        // Maps visits to the response DTO, filling in station + worker.
        private async Task<List<QueueResponseDto>> ToDtosAsync(IEnumerable<Queue> queues)
        {
            var list = queues.ToList();

            var roomIds = list.Where(q => q.AssignedRoomID.HasValue)
                .Select(q => q.AssignedRoomID!.Value).Distinct().ToList();
            var rooms = await _context.ClinicRooms
                .Where(r => roomIds.Contains(r.RoomID))
                .ToDictionaryAsync(r => r.RoomID);

            var workerIds = rooms.Values.Where(r => r.AssignedDoctorID.HasValue)
                .Select(r => r.AssignedDoctorID!.Value).Distinct().ToList();
            var workers = await _context.Users
                .Where(u => workerIds.Contains(u.UserID))
                .ToDictionaryAsync(u => u.UserID);

            // Relationship of the person who checked in to each child
            var parentIds = list.Select(q => q.ParentID).Distinct().ToList();
            var links = await _context.ChildParentRelationships
                .Where(r => parentIds.Contains(r.ParentID) && r.Status == "Active")
                .Select(r => new { r.ParentID, r.ChildID, r.RelationshipType })
                .ToListAsync();

            string? ParentRelationshipTo(Guid parentId, Guid childId) => links
                .Where(l => l.ParentID == parentId && l.ChildID == childId && !string.IsNullOrWhiteSpace(l.RelationshipType))
                .Select(l => l.RelationshipType)
                .FirstOrDefault();

            return list.Select(q =>
            {
                ClinicRoom? room = null;
                if (q.AssignedRoomID.HasValue) rooms.TryGetValue(q.AssignedRoomID.Value, out room);
                User? worker = null;
                if (room?.AssignedDoctorID != null) workers.TryGetValue(room.AssignedDoctorID.Value, out worker);

                var parentName = $"{q.Parent?.FirstName} {q.Parent?.LastName}".Trim();

                // Who brought each child: the person staff typed in (e.g. a
                // sibling the parent sent), otherwise the parent on file.
                var children = q.QueueChildren
                    .Select(qc => new QueueChildResponseDto
                    {
                        ChildID = qc.ChildID,
                        Name = $"{qc.Child?.FirstName} {qc.Child?.LastName}".Trim(),
                        BroughtBy = !string.IsNullOrWhiteSpace(qc.BroughtByName) ? qc.BroughtByName : parentName,
                        BroughtByRelationship = !string.IsNullOrWhiteSpace(qc.BroughtByName)
                            ? qc.BroughtByRelationship
                            : ParentRelationshipTo(q.ParentID, qc.ChildID),
                    })
                    .ToList();

                // One person for the whole visit: "Ana Castillo" + "Sibling".
                // Different people: "Ana Castillo (Sibling), Rosa Flores (Aunt)".
                var people = children
                    .GroupBy(c => c.BroughtBy)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Relationship = string.Join(" / ", g.Select(c => c.BroughtByRelationship)
                            .Where(r => !string.IsNullOrWhiteSpace(r)).Distinct()),
                    })
                    .ToList();
                string requestBy = parentName;
                string? requestByRelationship = null;
                if (people.Count == 1)
                {
                    requestBy = people[0].Name;
                    requestByRelationship = people[0].Relationship == "" ? null : people[0].Relationship;
                }
                else if (people.Count > 1)
                {
                    requestBy = string.Join(", ", people.Select(p => p.Relationship == "" ? p.Name : $"{p.Name} ({p.Relationship})"));
                }

                return new QueueResponseDto
                {
                    QueueID = q.QueueID,
                    QueueNumber = q.QueueNumber,
                    BarangayNo = q.Parent?.BarangayNo,
                    RequestBy = requestBy,
                    RequestByRelationship = requestByRelationship,
                    ParentName = parentName,
                    Status = q.Status,
                    QueueDate = q.QueueDate,
                    CheckedInAt = q.CreatedAt,
                    AssignedRoomID = q.AssignedRoomID,
                    StationName = room?.RoomNumber,
                    AssignedWorkerID = worker?.UserID,
                    AssignedWorkerName = worker != null ? $"{worker.FirstName} {worker.LastName}".Trim() : null,
                    Children = children
                };
            }).ToList();
        }

        // GET: api/Queue/today
        // Same response shape as GetAll (grouped-by-visit, with children),
        // but filtered to today server-side instead of pulling every queue
        // entry the clinic has ever had. Use this for "today's queue" views
        // (e.g. Staff Dashboard); Queue Management uses GetDay, and GetAll
        // is for the full history (Reports).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            var queues = await _context.Queues
                .Include(q => q.Parent)
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .Where(q => q.QueueDate >= TodayStart && q.QueueDate < TodayEnd)
                .OrderBy(q => q.QueueNumber)
                .ToListAsync();

            return Ok(await ToDtosAsync(queues));
        }

        // GET: api/Queue/day?date=2026-09-26
        // Queue Management: the log of one day (who came, who brought each
        // child, how it ended), and the nearest earlier/later days that had
        // check-ins so the page's arrows skip days the clinic was closed.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("day")]
        public async Task<IActionResult> GetDay([FromQuery] DateTime? date)
        {
            var day = (date ?? DateTime.Today).Date;
            var next = day.AddDays(1);

            var queues = await _context.Queues
                .Include(q => q.Parent)
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .Where(q => q.QueueDate >= day && q.QueueDate < next)
                .OrderBy(q => q.QueueNumber)
                .ToListAsync();

            var previousDate = await _context.Queues.Where(q => q.QueueDate < day)
                .Select(q => (DateTime?)q.QueueDate).MaxAsync();
            var nextDate = await _context.Queues.Where(q => q.QueueDate >= next)
                .Select(q => (DateTime?)q.QueueDate).MinAsync();

            return Ok(new
            {
                date = day,
                previousDate = previousDate?.Date,
                nextDate = nextDate?.Date,
                entries = await ToDtosAsync(queues),
            });
        }

       // GET: api/Queue
[Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
[HttpGet]
public async Task<IActionResult> GetAll()
{
    var queues = await _queueRepository.GetAllAsync();

    return Ok(await ToDtosAsync(queues));
}

        // GET: api/Queue/{id}
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var queue = await _queueRepository.GetByIdAsync(id);

            if (queue == null)
                return NotFound();

            return Ok((await ToDtosAsync(new[] { queue }))[0]);
        }

        // GET: api/Queue/my-status/{parentId}
        // Parent dashboard "Priority Ticket": the parent's own queue number
        // today, the number currently being served, and how many visits
        // are still ahead of them. Always 200 — checkedIn=false when the
        // parent hasn't checked in today.
        [HttpGet("my-status/{parentId}")]
        public async Task<IActionResult> GetMyStatus(Guid parentId)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, parentId)) return Forbid();
            var todays = await _context.Queues
                .Where(q => q.QueueDate >= TodayStart && q.QueueDate < TodayEnd)
                .OrderBy(q => q.QueueNumber)
                .ToListAsync();

            // A family can have a second entry today (a child brought in
            // after their first visit was called): show the one still open.
            var mine = todays.Where(q => q.ParentID == parentId)
                .OrderBy(q => IsOpen(q) ? 0 : 1)
                .ThenByDescending(q => q.QueueNumber)
                .FirstOrDefault();
            var serving = todays.FirstOrDefault(q => q.Status == "InProgress" || q.Status == "In Progress");

            int? position = null;
            if (mine != null && mine.Status == "Waiting")
            {
                position = todays.Count(q => q.Status == "Waiting" && q.QueueNumber < mine.QueueNumber) + 1;
            }

            // Called in by the Nurse (Call Next). calledAt changes on every
            // "Call Again", so the Check-in page knows to sound the alert again.
            bool called = mine?.Status == "InProgress";

            var childIds = mine == null
                ? new List<Guid>()
                : await _context.QueueChildren.Where(qc => qc.QueueID == mine.QueueID).Select(qc => qc.ChildID).ToListAsync();

            return Ok(new
            {
                checkedIn = mine != null,
                myQueueNumber = mine?.QueueNumber,
                myStatus = mine?.Status,
                nowServingNumber = serving?.QueueNumber,
                positionInLine = position,
                called,
                calledAt = called ? mine!.UpdatedAt : null,
                stationName = called ? "the vaccination room" : null,
                childIDs = childIds,
            });
        }

        // GET: api/Queue/parent/{parentId}
        [HttpGet("parent/{parentId}")]
        public async Task<IActionResult> GetParentQueue(Guid parentId)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, parentId)) return Forbid();
            var queue = await _queueRepository.GetParentQueueAsync(
                parentId,
                DateTime.Today
            );

            if (queue == null)
                return NotFound(new
                {
                    message = "Parent does not have a queue entry for today."
                });

            return Ok((await ToDtosAsync(new[] { queue }))[0]);
        }

        // POST: api/Queue
        // Parents check in from the Check-in page (with today's clinic QR
        // code when that's switched on); Admission Staff add walk-ins by hand.
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateQueueRequest request,
            [FromServices] AndroidWebAPI.Services.IQueueQRCodeService qrService,
            [FromServices] IQueueQRSettingRepository qrSettings,
            [FromServices] AndroidWebAPI.Services.AuditService audit)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, request.ParentID)) return Forbid();
            if (request.ChildIDs == null || request.ChildIDs.Count == 0)
            {
                return BadRequest(new
                {
                    message = "At least one child must be selected."
                });
            }

            // Every child must belong to this parent
            var linkedChildren = await _context.ChildParentRelationships
                .Where(r => r.ParentID == request.ParentID && r.Status == "Active")
                .Select(r => r.ChildID)
                .ToListAsync();
            if (request.ChildIDs.Any(id => !linkedChildren.Contains(id)))
            {
                return BadRequest(new
                {
                    message = "One of the selected children isn't linked to this parent."
                });
            }

            // A parent checking in from their own phone must scan today's QR
            bool isParent = User.IsInRole("Parent");
            if (isParent && (await qrSettings.GetAsync())?.IsEnabled == true)
            {
                var (ok, error) = await qrService.ValidateAsync(request.QrCode);
                if (!ok) return BadRequest(new { message = error });
            }

            // Someone other than the parent brought the child: staff type
            // their name and relationship here instead of registering them
            // (they may never come back). Parents checking in themselves
            // are always the one who brought the child.
            string? broughtByName = isParent ? null : request.BroughtByName?.Trim();
            string? broughtByRelationship = isParent ? null : request.BroughtByRelationship?.Trim();
            if (string.IsNullOrEmpty(broughtByName))
            {
                broughtByName = null;
                broughtByRelationship = null;
            }
            else
            {
                if (string.IsNullOrEmpty(broughtByRelationship))
                    return BadRequest(new { message = "Enter how the person who brought the child is related to them." });
                if (broughtByName.Length > 100 || broughtByRelationship.Length > 50)
                    return BadRequest(new { message = "The name or relationship is too long." });
            }

            var today = DateTime.Today;
            var childIds = request.ChildIDs.Distinct().ToList();

            var todays = await _context.Queues
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .Where(q => q.ParentID == request.ParentID && q.QueueDate >= TodayStart && q.QueueDate < TodayEnd)
                .OrderBy(q => q.QueueNumber)
                .ToListAsync();

            // A parent checking in from their phone: one entry a day
            if (isParent && todays.Count > 0)
            {
                var existingQueue = todays.OrderBy(q => IsOpen(q) ? 0 : 1).ThenByDescending(q => q.QueueNumber).First();
                return Conflict(new
                {
                    message = "This parent already has a queue entry for today.",
                    queueID = existingQueue.QueueID,
                    queueNumber = existingQueue.QueueNumber
                });
            }

            // Staff: a child can only be in today's queue once
            var already = todays
                .SelectMany(q => q.QueueChildren.Select(qc => new { q.QueueNumber, qc.ChildID, qc.Child }))
                .FirstOrDefault(x => childIds.Contains(x.ChildID));
            if (already != null)
            {
                return Conflict(new
                {
                    message = $"{already.Child?.FirstName} {already.Child?.LastName} is already in today's queue (Q-{already.QueueNumber:000}).".Trim()
                });
            }

            // The family is still waiting (e.g. a sibling brought one child,
            // an aunt brings another later): add to that visit so they keep
            // its place and go in together. If that visit was already called
            // or finished, the child gets a new number at the end of the line.
            var queue = todays.FirstOrDefault(q => q.Status == "Waiting");
            bool joined = queue != null;
            if (queue == null)
            {
                queue = new Queue
                {
                    QueueID = Guid.NewGuid(),
                    ParentID = request.ParentID,
                    QueueNumber = await _queueRepository.GetNextQueueNumberAsync(today),
                    QueueDate = today,
                    Status = "Waiting",
                    CreatedAt = DateTime.Now
                };
                _context.Queues.Add(queue);
            }

            foreach (var childId in childIds)
            {
                _context.QueueChildren.Add(new QueueChild
                {
                    QueueChildID = Guid.NewGuid(),
                    QueueID = queue.QueueID,
                    ChildID = childId,
                    BroughtByName = broughtByName,
                    BroughtByRelationship = broughtByRelationship,
                    CreatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();

            var saved = await _queueRepository.GetByIdAsync(queue.QueueID);

            // Audit trail when someone other than the parent brought the
            // child, or a child joined the family's waiting visit
            if (saved != null && (broughtByName != null || joined))
            {
                var childNames = string.Join(", ", saved.QueueChildren
                    .Where(qc => childIds.Contains(qc.ChildID))
                    .Select(qc => $"{qc.Child?.FirstName} {qc.Child?.LastName}".Trim()));
                var parentName = $"{saved.Parent?.FirstName} {saved.Parent?.LastName}".Trim();
                var description = (joined ? $"Added to the family's waiting visit. " : "")
                    + (broughtByName != null
                        ? $"Brought in by {broughtByName} ({broughtByRelationship}) on behalf of {parentName}."
                        : $"Brought in by {parentName}.");
                await audit.LogAsync("Queue", "Check-in",
                    $"Queue #{saved.QueueNumber} – {childNames}", description);
            }

            // Return the same DTO as every other endpoint here. Returning the
            // raw entity serialized its navigation properties in a loop
            // (Queue -> QueueChildren -> Queue ...) and threw AFTER the row
            // was saved, so check-ins looked like they failed with a 500.
            var dto = (await ToDtosAsync(new[] { saved! }))[0];
            if (joined) return Ok(dto);
            return CreatedAtAction(nameof(GetById), new { id = queue.QueueID }, dto);
        }

        // PUT: api/Queue/{id}/status
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] UpdateQueueStatusRequest request,
            [FromServices] AndroidWebAPI.Services.ParentNotifier notifier)
        {
            var queue = await _queueRepository.GetByIdAsync(id);

            if (queue == null)
                return NotFound();

            // Past days are a record (Queue Management): kept as they ended
            if (queue.QueueDate < TodayStart)
                return BadRequest(new { message = "Entries from past days can't be changed." });

            bool finishing = request.Status == "Completed" && queue.Status != "Completed";

            // "In Progress" means "called into the vaccination room", which
            // also alerts the parent, so it is only set by Call Next.
            if (request.Status == "InProgress" && queue.Status != "InProgress")
            {
                return BadRequest(new
                {
                    message = "To bring a family into the vaccination room, use Call Next (or Call) on the Dashboard."
                });
            }

            queue.Status = request.Status;
            queue.UpdatedAt = DateTime.Now;

            // Leaving "In Progress" frees the station. Going back to Waiting
            // also clears the assignment, so staff re-assign a station later.
            if (request.Status != "InProgress" && queue.AssignedRoomID.HasValue)
            {
                await FreeRoomAsync(queue.AssignedRoomID.Value);
                if (request.Status == "Waiting") queue.AssignedRoomID = null;
            }

            await _queueRepository.UpdateAsync(queue);

            // Visit finished: one text per child with today's vaccines and the next date
            if (finishing)
                await AndroidWebAPI.Services.VisitSummary.SendAsync(_context, notifier, queue.QueueID);

            // Map to the same DTO every other endpoint here returns —
            // returning the raw entity serializes its Parent/QueueChildren
            // navigation properties, which can hit a circular reference
            // (Parent -> Queues -> ... ) and throw *after* the update has
            // already been saved, surfacing as a 500 even though the write
            // succeeded.
            return Ok((await ToDtosAsync(new[] { queue }))[0]);
        }

        // DELETE: api/Queue/{id}
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var queue = await _queueRepository.GetByIdAsync(id);

            if (queue == null)
                return NotFound();

            // Only today's queue can be changed ("Remove from Queue" on the
            // Dashboard); past days stay as the clinic's record of visits.
            if (queue.QueueDate < TodayStart)
                return BadRequest(new { message = "Entries from past days can't be removed." });

            if (queue.AssignedRoomID.HasValue && queue.Status == "InProgress")
            {
                await FreeRoomAsync(queue.AssignedRoomID.Value);
                await _context.SaveChangesAsync();
            }

            await _queueRepository.DeleteAsync(id);

            return NoContent();
        }

        private async Task FreeRoomAsync(int roomId)
        {
            var room = await _context.ClinicRooms.FindAsync(roomId);
            if (room != null)
            {
                room.IsOccupied = false;
                room.CurrentChildID = null;
            }
        }
    }

    // ============================================
    // REQUEST DTOs
    // ============================================

    public class CreateQueueRequest
    {
        public Guid ParentID { get; set; }

        public List<Guid> ChildIDs { get; set; }
            = new List<Guid>();

        // Today's check-in code from the clinic QR (parents only)
        public string? QrCode { get; set; }

        // Staff only: the person who brought the child when it isn't the
        // parent (name + relationship to the child). Leave empty otherwise.
        public string? BroughtByName { get; set; }
        public string? BroughtByRelationship { get; set; }
    }

    public class UpdateQueueStatusRequest
    {
        public string Status { get; set; } = "Waiting";
    }
}