# Aruga: what changed (September 2026)

A plain-language list of everything that was changed, fixed or added, grouped
by who uses it. The work up to Sep 27 is on the `Aruga-Polished` git branch;
the beneficiary revision below is on `Aruga-Revision`.

---

## Beneficiary revision (Sep 28, 2026)

Changes asked for by Leveriza Health Center and by City Hall.

**Three user levels instead of four**
- **Admin / Doctor.** The Doctor is the highest authority, so a Doctor account now signs in to the Admin portal (all the admin pages).
- **Staff / Nurse.** A Nurse account signs in to the Staff portal, which now also does the vaccinating. The separate Doctor/Nurse portal is gone; old links to it open the Staff Dashboard.
- **Parent** is unchanged.
- Admin → User Management: new accounts are "Staff / Nurse" or "Admin / Doctor". Older accounts keep working: an "Administrator" account is Admin level, a "Staff" account is Staff level.
- Labels everywhere (sidebars, Audit Logs, Reports, dashboard counts) use the three level names.

**One vaccination room, with Call Next**
- Leveriza has one vaccination room and one person vaccinating, so stations and room assignment were removed (Admin → Vaccination Rooms and Staff → Doctor / Nurse are gone).
- Staff Dashboard → **Vaccination Room** card: **Call Next** calls the next family in line. The parent's phone shows a full-screen "It's your turn!" alert with a chime (and a vibration on Android) on any parent page, plus one SMS. The call is also in the parent's notifications.
- For the family inside: **Start Vaccinating** (per child), **Call Again** (alerts the phone again, no second SMS), **Not Here** (back to waiting, same number) and **Complete Visit**. The megaphone button on a waiting row calls that family out of turn.
- Only one family can be inside at a time. Today's vaccines can only be recorded for a family that was called in, and only a Staff / Nurse account can record them. The dose is always saved under the Nurse who is signed in.

**Each health worker sees only their own vaccination records**
- Staff → **My Vaccination Records** lists only the vaccines that Nurse gave. A child's full history is still on the child's record, and the Admin's Patient Management and Reports still count every dose.

**Barangays 19 and 21–40 only**
- Every barangay field is now a dropdown of Barangays 19 and 21–40 (Staff Patient Records, Admin Patient Management, Admin Add User). Families from other barangays can't be registered; the system refuses them even if typed in some other way.
- Older records with a barangay outside this list still open and can be edited; the old barangay is shown as "(outside Leveriza's area)" until someone picks a new one.
- The demo data now uses Barangays 19, 21, 24 and 33, with Pasay City addresses (Leveriza Health Center is in Pasay City).

**Admin → Patient Management: progress pop-up**
- Hovering (or tapping) the progress bar, e.g. 14/15, shows which vaccines the child hasn't had yet, with the due date, and overdue ones in red.

**Inventory summaries and working Reports**
- New **Inventory Summary** on Admin → Inventory, Staff → Inventory and Staff → Clinic Reports: per vaccine, doses on hand, minimum, received and used this month (or last month, this year, or any dates), expiring within 30 days, expired doses still on the shelf, average doses used per week and about how long the stock will last. It can be exported to Excel.
- Admin → Reports now shows the selected report right away and updates by itself when you pick another report or change a filter (before, it stayed empty until "Generate Report" was clicked, so it looked like it did nothing).
- New inventory reports: **Inventory Summary** and **Stock Received**. Inventory reports now show totals at the top (doses on hand, batches, low stock, expiring, expired).
- The "Available Doses" count on Admin → Inventory no longer includes expired doses.

**Data Privacy consent — Data Privacy Act of 2012 (Republic Act No. 10173), asked by City Hall**
- Registering a parent/guardian (Staff Patient Records, Admin Patient Management, Admin Add User) needs a ticked **Data Privacy consent** box. "Read the notice" shows the full Data Privacy Notice. The system refuses a registration without it, and the date is saved.
- The first time a parent signs in, they must read the notice and tick **I agree** before using the portal (or sign out). This is asked once; the date is saved and appears in the Audit Logs.
- The notice says plainly that the health center shares the information with Aruga and its student developers, what is kept, why, who can see it, and the parent's rights under the Data Privacy Act.

**For the team**
- The database gets two new columns (`Parents.ConsentRecordedAt`, `Parents.PrivacyConsentAt`). The API adds them by itself the first time it starts, so no script needs to be run. `Schema.sql` has them for new databases.
- Checked on a copy of the database: 40 API checks (levels, Call Next, recording rules, own records, barangays, consent, inventory summary) and a browser walk-through of the Nurse, the parent on a phone, and the Admin / Doctor.

---

## Full-system check (Sep 27, 2026)

Every page for all four user levels was opened, and every main action was
done once on a copy of the database: registering a family, check-in (Staff
and the parent's QR), sending to a station, vaccinating, stock going down,
the parent seeing the record, user/room/vaccine/inventory management,
closures, announcements, reports, password changes and Forgot Password.
These were fixed:

- **Check-in QR ignored hours changed later in the day.** Today's QR kept the hours it was made with, so if the admin opened the day late (an Operating Hours exception, e.g. for a demo after 11 AM), parents were still told "Check-in for today closed at 11:00 AM". The QR now always follows today's Operating Hours.
- **Admin → Inventory: Edit Batch, Deactivate and Activate failed** ("ID mismatch"). Fixed.
- **Editing a batch moved its expiry date one day earlier** (a time-zone bug: Dec 31 was saved as Dec 30 on every save). Fixed; the manufacturing date had the same problem.
- **Editing a batch can no longer undo doses given in the meantime.** If a nurse gave a dose while the admin had the Inventory page open, saving the batch put the old stock count back. The stock count now only changes when doses are given or a batch is received.
- **Staff → Patient Records: "Make Primary" now saves.** Before, it only changed the screen and was lost on refresh.
- **Staff → Patient Records: "Vaccination reminders notify: Primary Contact Only / All Linked Accounts" now saves** and controls who gets reminders. Before, it did nothing and always showed "Primary Contact Only" even though every linked account was being notified. Changing the primary contact while "Primary Contact Only" is on moves the reminders to the new primary. Both changes are in the Audit Logs.
- **Link / Transfer guardian: the window now closes after saving.** It stayed open after a successful save, which looked like it had failed (and invited a second click).
- **Doctor/Nurse → My Account: removed the "Email Alerts / SMS Alerts / Daily Reports" switches.** They were only saved in the browser and did nothing (Aruga sends reminders to parents, not to health workers). The card now says what is actually sent.
- **Staff → Vaccine Schedule:** the "Child Name" search box no longer overlaps the status filter.
- **Staff → Inventory:** a new batch without a received date is dated today even before 8 AM (it was dated the day before).
- **Login page:** the line "This login is used by parents, healthworkers, staff, and system administrators" is replaced with "© 2026 Aruga · Leveriza Health Center. All rights reserved." (the year updates by itself).

---

## Newest fixes (parent portal)

- **Clinic hours and vaccination hours are shown separately.**
  - Parents see both: **Clinic hours: Mon–Fri · 8:00 AM – 5:00 PM** and **Vaccination hours: Mon, Wed, Fri · 8:00 AM – 12:00 PM** (check-in until 11:00 AM), in the same note on the Schedule and Check-in pages.
  - Operating Hours (admin) is now labeled as the **vaccination** schedule: due dates, check-in and reminders follow it. The clinic hours are a setting (`Clinic:Hours` in `appsettings.json`).
  - On a Tuesday or Thursday, parents see "No vaccinations today" (not "clinic closed"); "Health center closed" is only shown on a closure day. The Schedule calendar highlights only vaccination days, and the Staff calendar marks other days "No vaccines".
  - When a day is closed (new hours, a holiday, or a removed special opening day), doses due that day move to the next vaccination day automatically.
- **Parents are told when the health center closes** (e.g. a typhoon).
  - Under Operating Hours → Add Exception, the admin can now close **several days at once** ("Until" date).
  - Saving a closure notifies **every parent** in the app. Families with a child **due on a closed day** also get an **email and SMS** with the child's new date, e.g. "Leveriza Health Center is CLOSED on Mon, Oct 12 (Typhoon). Angela's Penta moved to Wed, Oct 14."
  - SMS goes only to those families, so one closure doesn't use up the free SMS plan. For a message to everyone by email/SMS, use Announcements.
  - The admin sees how many parents were notified and how many doses moved, and the closure is recorded in the Audit Logs. Editing only the reason doesn't send the notice again.
- **Clearer "Today's Priority Ticket" on the Overview.**
  - It no longer shows a big "Now serving #002" next to "Not checked in", which looked like the parent's own ticket.
  - It now says when the clinic is closed today or when no child is linked yet, and shows "Now serving" only on clinic days.
  - Checked-in parents see which child the ticket is for.
- **Birth weight and height show on the parent Overview.** They were saved but never loaded on the parent side. If none was entered, it says "Not recorded".
  - Staff and admin forms now reject impossible values (weight 0.5–7 kg, height 25–65 cm), which catches kg/cm mix-ups and missing decimal points.
- **Relatives who bring the child (grandmother, aunt, uncle...) are shown with their relationship.**
  - Staff can already register a relative (with or without a portal login) and link them to the child as Grandmother, Aunt, Uncle, etc. Only linked people can check a child in, which is how staff know the person is allowed to bring the child.
  - The **queue** (Staff Queue Management, Staff dashboard and bell, Doctor/Nurse queue) now shows who checked in with their relationship, e.g. "Lourdes Luna (Grandmother)".
  - The **Doctor/Nurse patient page** lists every linked parent/guardian with their relationship (the table shows "+1" when there is more than one); the **vaccination screen** shows who checked the child in and all guardians; the **printed vaccination card** lists all guardians.
  - In the **parent portal profile**, each child shows "You are Isabela's Grandmother" and everyone linked to the child (before, only Mother/Father/Guardian filled in).
  - Demo data: **lourdes.luna@demo.aruga.ph** is Isabela Cruz's grandmother (second guardian), to show this at the defense.
- **More messages go by SMS, combined so they don't use up the load.**
  - **Every reminder** (14 / 7 / 5 / 3 / 1 days before, and 1 / 5 / 14 / 30 days after a missed dose) now also goes by SMS: **one text per child per step**, listing all of that visit's vaccines, e.g. "Mia's vaccines (Penta 3, OPV 3, IPV 1, PCV 3) are due tomorrow, Mon, Sep 28." The app and email still get one notice per dose.
  - **When a visit is finished** (Complete Visit, or Staff marking it Completed), parents get one text: the vaccines given today and the next vaccination date and vaccines.
  - **"Record updated"** now also goes by SMS, with what changed ("Allergies: Egg (mild rash)"), so a parent can report a mistake.
  - The most urgent texts are sent first, so if the daily limit (50) is reached, "due tomorrow" and "missed" texts still go out.
  - Reminders for a later dose (e.g. Penta 2) wait while an earlier dose of the same vaccine is still missing; the overdue reminder for the earlier dose covers it.
  - Demo and test accounts still never get real texts; the text they would get is written to the API window instead, so it can be shown.
- **Aruga works on phones** (for QR check-in, and the whole parent portal).
  - Pages no longer call `http://localhost:57147` (on a phone that means the phone itself). They call `/api` on the address the site was opened from, and the website passes those calls on to the API on the laptop (`frontend/vite.config.js`). `npm run dev` now also listens on the Wi-Fi.
  - The Staff **Check-in QR** points at the laptop's network address even when staff open Aruga as `localhost`, and shows that address so parents can type it.
  - **Scan the Clinic QR Code** button on the Check-in tab works on phones. Phones only allow a live camera view inside a website on https, so on the local (http) address the button opens the phone's camera to take a photo of the QR, and Aruga reads the code from the photo (tested with tilted, far-away and 24-megapixel photos). On https (or `localhost` on the laptop) it shows the live scanner. If the camera can't be opened, the next tap takes a photo instead. Scanning with the phone's camera app and typing the code still work.
  - Parent pages fit a phone screen: the tabs share the width, the search box moves to its own line, and the Schedule list and calendar stack.
  - The audit log records the phone's address and device (e.g. "Safari on iOS") instead of the laptop's.
  - The floating Vue DevTools button is off by default. Setup steps: `backend/README.md` → "Using Aruga on phones".
- **Doctors and Nurses can update a child's allergies and existing conditions** (Patients page → Medical Records → "Edit allergies / conditions").
  - Only those two fields; the rest of the profile stays with the Admission Staff and the Administrator.
  - "Existing Conditions" is now saved (before, the box always said "None reported" because nothing stored it). Database scripts add the `ExistingConditions` column.
  - The change is recorded in the Audit Logs and the parent gets a "record updated" notice. Allergies and conditions show in the red warning on the vaccination screen, in the Staff patient drawer and in Admin Patient Management.
- **Staff Patient Records: the "…" menu and parent logins.**
  - The "…" menu now opens in front of the table instead of being cut off by it.
  - **Deactivate Login / Reactivate Login** and **Reset Password** now work (they did nothing before). A deactivated parent can't sign in ("Account is inactive"); their children's records and reminders are unaffected. Reset shows a temporary password to give the parent (also emailed if they have a real email), and they must choose a new one at sign-in.
  - The Status column shows the real login status: Active, Inactive, or **No Login** (a contact-only guardian). Staff can change **parent** logins only; staff and health-worker accounts stay with the Administrator.
  - The **Archive** buttons were removed: nothing behind them was built, and archiving would need changes across the whole system.
- **Every child has a full schedule.** Children added straight into the database had none, so their Schedule and Records only listed doses already given.
  - The system now builds the missing schedule when it starts: given doses count as done, the 28-day rule applies to the rest, and past dates show as overdue.
  - The Overview marks overdue doses "Overdue" instead of "Scheduled".
- **Fixed:**
  - doses entered from the Yellow Book before the earlier fixes weren't ticked off on the schedule;
  - doses past their date were never marked "Missed" (now checked every morning);
  - texts now use plain characters, so each SMS fits in 160 characters;
  - test records (email ending in @example.com or containing @demo.) no longer get texts either, only emails were blocked before; their made-up numbers could belong to real people.

## Latest changes (after the group's feedback)

- **Family No. and Barangay instead of Patient ID.** The health center identifies families by these, so:
  - Staff Patient Records, the Doctor/Nurse Patients list, admin Patient Management and the printed vaccination card show **Family No.** and **Barangay** instead of the system ID;
  - search works by Family No. or barangay;
  - Family No. is now saved (the registration form had the box, but the value was thrown away) and can be edited;
  - when a child is registered under a parent who already has a child here, the Family No., barangay and address are filled in from the sibling.
- **Rooms are named Room 1, Room 2, Room 3.**
  - The test stations (101, 102, 103, Station A–C) were removed.
  - A new admin page, **Vaccination Rooms**, adds, renames or removes rooms (a room with a patient in it can't be removed).
  - Staff still choose who works in each room every day on their dashboard.
- **Weekly stock check (Wednesdays).**
  - Every Wednesday at 8:00 AM the **Admission Staff and the Administrator** get a stock check in their bell and by email. It shows, per vaccine: doses on hand vs. doses due this week, what to re-stock, and about how many to order. The order covers two weeks plus the minimum stock, because when the pharmacy is out the supplies come the following week.
  - The same table is on the Staff **Inventory** page any time, with a "Send to Staff & Admin now" button.
  - The instant "running low" alert (a batch drops below its minimum) now also goes to the Admission Staff.
  - When a new batch is received, parents who were told the vaccine was out of stock are told right away that it's back.
- **Free SMS through TextBee.**
  - Texts are sent through the clinic's Android phone (free plan: 300 a month, 50 a day).
  - To fit that, only the messages where timing matters go by SMS: "due tomorrow", "missed yesterday", out of stock / back in stock, password codes, and announcements when ticked. Everything else still goes in the app and by email.
  - A daily limit stops texts once it's reached.
  - Email stays on Gmail: it's free and allows about 500 a day, where EmailJS gives 200 a month. Setup steps are in `backend/README.md`.
- **Removed:**
  - the unused self-registration page (`/registration`), since staff register families;
  - the four outdated analysis files in `frontend/` (`API_DISCREPANCIES.md`, `FRONTEND_ANALYSIS.md`, `INTEGRATION_GUIDE.md`, `PROJECT_ANALYSIS.md`).
- **Fixed:**
  - `DemoSeed_Remove.sql` still referred to two deleted tables and would have failed;
  - a long notification (e.g. many changed fields) could make saving fail, because the in-app copy has a 500-character limit;
  - the Check-in QR page showed an error on days the clinic is closed; it now just explains that the clinic is closed.

---

## 1. The biggest changes at a glance

| What | Before | Now |
|---|---|---|
| Doctor / Nurse portal | Separate Doctor folder, many pages were placeholders | One **Healthcare Worker** portal for Doctors and Nurses; every page uses real data |
| Stations | Only "Doctor Carlos" could be put at a station; anyone could record any child's vaccine | Staff pick a Doctor or Nurse for each station; **only the worker at the patient's station can record today's vaccine** |
| Doctor Diagnosis | A diagnosis field on every vaccination | Replaced by **Remarks** (reactions / complications after the vaccine), editable later, with history |
| QR check-in | Switched off ("demo" button, the QR tables didn't exist) | **Works**: staff show today's QR; parents scan it (or type the 6-letter code) to get a queue number |
| Forgot Password | Page existed, backend didn't | **Works**: 6-digit code by email or SMS, 10-minute expiry, limited tries |
| SMS / Email (Objective 3) | In-app notifications only | Reminders, "vaccine given", "record updated", stock notices, announcements and account emails go out **by email and SMS** as well (once set up; see §8) |
| Vaccine stock | Staff/admin saw low stock | Also: parents of children due for an **out-of-stock vaccine are told not to come yet**, then told when it's back |
| Security | Most of the API could be used without logging in | Every request needs a login; **each role can only do its own job**; **parents can only see their own family** |
| Database | Duplicate rules, unused tables, missing QR tables | Cleaned up; one script builds the whole database on a new computer |

---

## 2. Parent / Guardian

- **Overview, Schedule, Records** use the child's real vaccination timeline from the server (before, each page calculated dates on its own, using wrong vaccine IDs).
- **Check-in**
  - Scan the QR at the clinic entrance with the phone camera, or type the code printed under it.
  - If they scan while logged out, they come back to Check-in after logging in.
  - After checking in, the page shows the queue number, how many families are ahead, and **which station to go to** when called ("Please go to Room 2 (Nurse Mark Anthony)"). It updates by itself.
  - Clinic hours shown on the page come from the admin's Operating Hours (they were hard-coded before).
- **Records → Download PDF** opens a printable immunization card (patient info, all doses with lot numbers, who gave them).
- **Notifications** now include: reminders before each dose (14, 7, 5, 3, 1 days), missed-dose follow-ups (1, 5, 14, 30 days), "vaccine given" (with the next due date), "your child's record was updated" (lists what changed), stock notices, and announcements.
- **Forgot Password** on the login page works (see §1).
- If a login session expires (after 8 hours), the app goes back to the login page with a message instead of showing empty pages.

## 3. Admission Staff

- **Dashboard**
  - Each station has a dropdown to choose the Doctor/Nurse working there today.
  - A patient can only be sent to a station that has someone at it.
  - The queue table shows check-in time, station and health worker.
  - "Remove from Queue" really deletes the entry; "Mark Done" asks for confirmation and frees the station.
  - Stations left "busy" from an earlier day free themselves automatically.
  - **Stock alerts:** an out-of-stock vaccine has a "Notify parents with a child due for it" button.
  - Quick action **Check-in QR** (was a disabled "coming soon" button).
- **Check-in QR page** (new, also in the sidebar)
  - Shows today's QR and code, with a Print button.
  - A switch sets whether parents must scan it.
  - A new code is made each clinic day, so an old photo of the QR can't be used to queue from home.
- **Patient Records**
  - "Next Vaccine" and "Vaccination Status" now come from the real schedule. They were calculated in the browser assuming Mon/Wed/Fri clinic days, so they could disagree with what parents saw.
  - Editing a child:
    - **allergies are kept** (they used to be erased on every save)
    - changing the **birth date now works** and moves every upcoming dose
    - you can edit sex and allergies
    - the parent is notified of the change
  - Editing a parent uses separate First / Middle / Last name boxes (two-word first names like "Mark Anthony" were being split wrongly).
  - The table shows each child's **Family No. and Barangay** (see "Latest changes") instead of a system ID.
  - The temporary password rule for a new parent login matches the server's rule (upper, lower, number, symbol). The parent gets a welcome email.
  - A guardian with no email no longer breaks the page.
- **Vaccine Schedule:** "Doses Needed — Next 30 Days" now shows the stock on hand and warns "short by N".
- **Calendar:** shows, per day, children **due**, doses **given**, and days the clinic is **closed** (weekends and holidays). Click a day for the names.
- **Queue Management:** a visit can't be set to "In Progress" from here any more. That only happens by sending the patient to a station, so the station rules hold.
- **Account Settings** (gear icon):
  - Staff can update their own email, mobile number and address; name and role stay admin-only.
  - The password form shows readable errors.
- **Bell icon:** families waiting for a station, with a red dot when someone has waited 15+ minutes. Before, it listed "late" patients, a status the system never sets, so it was always empty.
- The separate old "Parents" and "Children" pages (`/staff/parents`, `/staff/children`) now open the matching tab of Patient Records. They were duplicates, and their Edit button did nothing.

## 4. Doctor / Nurse (Healthcare Worker)

- One shared portal: Home, Queue, Patients, Calendar, Vaccination Records, Reports, My Account. Old `/doctor/...` links still work.
- **Home and Queue**
  - Both show **My Station** and the patient currently there.
  - "Start Vaccinating" only appears for the patient the staff sent to your station.
- **Vaccination visit page**
  - Shows due and overdue doses and allergies, and records several doses in one visit.
  - Shows the next due date after each dose.
  - Blocked, with an explanation, if the patient isn't at your station. The server enforces the same rule.
  - Recording a dose deducts stock and applies the **28-day rule**: the next dose is never sooner than 28 days (or the vaccine's own interval) after the one just given, then moved to the next open clinic day.
- **Patients:** status and next dose from the real schedule; the remaining schedule; history with an editable **Remarks / Reactions** column (reactions are highlighted; every edit is logged).
- **My Account:** only contact information can be edited. Name and PRC license are changed by the Administrator (as requested).
- Low-stock alerts arrive in the bell.

## 5. System Administrator

- Every page uses live data: Dashboard, User Management, Patient Management, Vaccines, Inventory, Notifications, Operating Hours, Reports, Audit Logs.
- **User Management**
  - Create Doctor / Nurse / Staff / Parent accounts. This used to fail because of a database rule.
  - The temporary password is shown **and emailed** to the person.
  - "Reset Password" also emails the new temporary password.
  - Edit Profile lets the admin change names and PRC license.
  - Deactivated accounts can't log in.
- **Notifications:** "Create Announcement" can also go out **by email and/or SMS** (it was a disabled "Future" option). The page shows whether email and SMS are set up.
- **Inventory:** batches have a **Manufacturing Date** (from the paper's objectives), shown in the table and details.
- **Vaccines:** "Delete" works. It used to crash because the function was missing. A vaccine already used in records is protected: you're told to deactivate it instead.
- **Audit Logs**
  - Every important action is recorded with who, when, where from, and old/new values.
  - Recorded actions include logins, failed logins, password changes and resets, accounts, children, parents, vaccinations, remarks, stations, inventory, vaccine deletions, announcements, and QR settings.
- **Header:** the bell shows the admin's own alerts (e.g. low stock); the gear opens Change Password / Operating Hours / Log out (both did nothing before). The sidebar shows the real admin's name instead of a placeholder.

## 6. Automatic features (run by the server)

- **Every morning at 8:00**, reminders and follow-ups go to every linked parent who accepts notifications, in the app, by email and by SMS. They use:
  - the same due dates the app shows (before, the reminders computed their own dates, assuming Mon/Wed/Fri clinic days);
  - the clinic's real days and hours in the message.
- **Out-of-stock check** at the same time (see §1).
- **Birth-date correction:** the whole remaining schedule is recalculated.

## 7. Security

- The API refuses any request without a valid login, except Login and Forgot Password.
- Role rules on the server, not just hidden buttons:
  - only the Admin manages accounts, vaccines, schedule rules, clinic hours and announcements;
  - only Staff/Admin register families and run the queue and stations;
  - only Doctors/Nurses record vaccinations and remarks.
- A parent can only open their own profile, children, records, notifications and queue ticket (tested: other families' data returns "403 Forbidden").
- Passwords are stored as BCrypt hashes; reset codes are hashed too; 5 wrong logins lock an account for 15 minutes.
- Demo accounts never receive real emails or texts, because their made-up numbers could belong to real people.

## 8. Database

- A backup was made first: `ArugaSystemDB_before_cleanup_2026-09-26.bak` in SQL Server's Backup folder.
- `backend/Database/Cleanup_2026-09.sql` (already run on this laptop):
  - removed unused tables: `Queue` (old), `Personnel`, `Staff`, `PasswordOtps`, `PasswordResetTokens`, and an unused stored procedure;
  - removed copies of the same rule (3× the same foreign keys, 3× "unique email", 3× "unique username", 3× the same check) and gave keys readable names;
  - removed the Doctor Diagnosis columns (any text would have been copied into Remarks first; there was none);
  - added the QR check-in tables, the Forgot Password codes table, and `VaccineInventory.ManufacturingDate`;
  - admins now have UserType "Administrator" (was "Admission");
  - station names can be up to 50 characters;
  - indexes for the most common searches;
  - freed stale busy stations, and left each worker on one station only.
- `backend/Database/Schema.sql` (new): builds the complete database from nothing on a new computer.
  - Includes the vaccines, schedule rules, clinic hours, Room 1–3, and an `admin / Admin@2026` account.
  - Tested: it produces exactly the same structure as the cleaned database.
- `DemoSeed.sql` / `DemoSeed_Remove.sql`: sample data for demos and the beneficiary test (see `backend/Database/README.md`).
- The scripts are saved so that special characters (–, ½) come out right in SSMS and sqlcmd.

## 9. Other bugs fixed

- Check-ins sometimes looked like they failed (the server saved them, then crashed while replying).
- An admin changing their password on first login could be sent to the Staff dashboard.
- Change Password showed "Can't reach the server" for a wrong current password.
- A date helper on Patient Records converted dates to UTC, which moves Philippine dates one day earlier.
- Notification messages with several lines showed as one run-on line.
- The backend printed a password hash to the console on every start (removed); the console no longer floods with database queries, so important messages are readable.

## 10. Removed (all still in git history)

- The Doctor folder (merged into Healthcare) and `HealtcareSidebar.vue` (misspelled duplicate).
- Unused files that called API routes that don't exist:
  - `Login/Admin.vue`, `Login/StaffLogin.vue`, `Login/ParentManager.vue`, `Login/PageA.vue`, `Login/PageB.Vue`
  - `Admin/AdminHomepage.vue`, `SystemAdmin/PrintableRecord.vue`, `services/api.js`
  - `Staff/StaffChildRecord.vue`, `Staff/StaffParentRecord.vue`
- Backend models for the removed tables (`Personnel`, `Staff`, `BaseClass`) and `UpdateDiagnosisDto`.

## 11. How to run and test

1. Database: see `backend/Database/README.md`. This laptop is already done.
2. Backend: copy `backend/appsettings.example.json` → `appsettings.json` (keep your own connection string), then `dotnet run` in `backend`. **Restart it** after pulling these changes.
3. Frontend: `npm run dev` in `frontend`, open http://localhost:5173.
4. Demo logins (password `Aruga@2026`):
   - `demo.admin`, `demo.doctor`, `demo.nurse`, `demo.nurse2`, `demo.staff`
   - parents such as `maria.santos@demo.aruga.ph`
5. Run `DemoSeed.sql` **on the morning of the defense**, on a clinic day. On a weekend nothing is "due today" and check-in is closed; to demo on a closed day, add it under Operating Hours → Add Exception.
6. To turn on real emails, create a Gmail **App Password** for the clinic account and put it in `appsettings.json` (steps in `backend/README.md`). For free SMS, set up TextBee on the clinic's Android phone (same file). Until then, messages (including reset codes) are printed in the backend console window.

## 12. Things to update in your documentation

- Doctor Diagnosis → **Remarks** (Figure 20 already describes remarks).
- The **station** flow: staff assign a worker per station → patient is sent to a station → only that worker records the vaccine.
- QR check-in as built: a daily clinic QR + 6-letter code, optional per clinic.
- The paper says database operations use **stored procedures** and the backend uses **Node.js and C#**. The system actually uses C# (ASP.NET Core) with Entity Framework and Dapper, and no stored procedures.
- The scope mentions **growth charts** and **intake/triage forms**. The system stores birth height and weight only; there are no growth charts.
- Midwife is not a role.
