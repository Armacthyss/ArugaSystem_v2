# Aruga database scripts

Run these in SQL Server Management Studio (open the file, press **Execute**).

| Script | When to use it |
|---|---|
| `Schema.sql` | **New computer / empty SQL Server.** Creates `ArugaSystemDB` with every table, the 7 EPI vaccines and their schedule, vaccination hours (Mon, Wed, Fri 8 AM–12 PM, check-in until 11 AM), 3 vaccination rooms (Room 1–3), and one Admin / Doctor account: **admin / Admin@2026** (asks for a new password at first login; put the Doctor's real name and PRC license on it in User Management). |
| `Cleanup_2026-09.sql` | **You already have an older `ArugaSystemDB`.** Upgrades it to the same structure as `Schema.sql` without touching your patients or records, and changes the old Mon–Fri 8 AM–5 PM hours to Mon, Wed, Fri 8 AM–12 PM. Back up first (right-click the database → Tasks → Back Up...). |
| `DemoSeed.sql` | Optional. Adds sample families, children, vaccination history, stock and today's queue for demos. Run it **on the morning of the demo**: dates are computed from the day you run it. Safe to run again. |
| `DemoSeed_Remove.sql` | Removes everything `DemoSeed.sql` added. Your own records are left alone. |

Order on a new machine: `Schema.sql` → (optional) `DemoSeed.sql`.
Order on an existing database: back up → `Cleanup_2026-09.sql` → (optional) `DemoSeed.sql`.

Notes
- Every script switches to `ArugaSystemDB` by itself, so it doesn't matter which database is selected in SSMS.
- If your `appsettings.json` signs in with the SQL login `ArugaSystem` (`User Id=ArugaSystem`) instead of `Trusted_Connection=True`, `Schema.sql` gives that login access to the new database automatically.
- SSMS shows "Caution: Changing any part of an object name could break scripts..." while `Cleanup_2026-09.sql` gives constraints readable names. That is expected.
- From the command line, add `-I` so SQL Server accepts the scripts:
  `sqlcmd -S localhost -E -I -d ArugaSystemDB -i DemoSeed.sql`
- Demo logins (password **Aruga@2026**): Admin / Doctor `demo.doctor`, Staff / Nurse `demo.nurse`, `demo.nurse2`, and parents such as `maria.santos@demo.aruga.ph`. Each parent is asked to accept the Data Privacy Notice the first time they sign in.
- The API adds any new columns it needs by itself when it starts (e.g. the Data Privacy consent dates, Sep 2026), so an older database doesn't need a script for them. `lourdes.luna@demo.aruga.ph` is Isabela Cruz's grandmother (a second guardian), to show a relative bringing the child.
- Parents can only check in on a vaccination day (Mon, Wed, Fri) from 8:00 to 11:00 AM, and nothing is "due today" on other days. If the demo is on another day or later in the day, first add that date under System Admin → Operating Hours → Add Exception as **open**, with hours that cover the demo, then run `DemoSeed.sql`. The script prints a note when today is not a vaccination day.
- Children added straight into the database (not through the app) get their vaccination schedule automatically the next time the API starts.
