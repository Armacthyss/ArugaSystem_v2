# Aruga API (ASP.NET Core 8)

Backend of Aruga: A Pediatric Health Record and Automated Vaccination
Lifecycle System, for Leveriza Health Center.

## Run it

1. Install the **.NET 8 SDK** and **SQL Server** (Express is fine) with SSMS.
2. Set up the database: see [`Database/README.md`](Database/README.md).
3. Copy `appsettings.example.json` to **`appsettings.json`** and fill in:
   - `ConnectionStrings:DefaultConnection`: your SQL Server (e.g. `Server=localhost;...` or `Server=MOLI;...`)
   - `Jwt:Key`: any random text of 32+ characters (all teammates can use the same one)
   - `Email` / `Sms`: optional, see below

   `appsettings.json` is in `.gitignore`, so passwords in it never go to GitHub.
4. Start it:
   ```
   dotnet run
   ```
   The API listens on **http://localhost:57147** (Swagger: http://localhost:57147/swagger).

Then start the frontend (`frontend` folder): `npm install` once, then `npm run dev`, and open http://localhost:5173.

## Using Aruga on phones (QR check-in)

`npm run dev` also makes the website reachable from phones on the same
network. It prints two addresses:

```
➜  Local:   http://localhost:5173/
➜  Network: http://192.168.1.3:5173/
```

Phones use the **Network** address. Only the website needs to be reachable:
it passes every `/api` request on to the API on this computer
(`frontend/vite.config.js`).

1. Put the laptop and the phones on the **same network**. School and café
   Wi-Fi often block devices from reaching each other, so the safest choice
   is a **mobile hotspot**: connect the laptop and the phones to one phone's
   hotspot (or turn on Windows' *Mobile hotspot* on the laptop).
2. Start the API (`dotnet run`) and the website (`npm run dev`) **after**
   joining that network.
3. If Windows asks whether Node.js may use the network, tick **Private and
   Public** and click **Allow**.
4. On the laptop, open **Staff → Check-in QR**. The QR points at the laptop's
   network address (shown under "Address phones open"), even when Aruga is
   open as `localhost`. If the laptop is on more than one network, pick the one
   the phones are on.
5. Parents check in either way:
   - In Aruga: **Check-in → Scan the Clinic QR Code**. On this local (http)
     address the phone's camera opens to take a photo of the QR, and Aruga
     reads the code from the photo. (Phones only allow a live camera view
     inside a website on https.)
   - Or with the **phone's camera app**: scan the QR, tap the link, sign in.

   Then they pick their children. Typing the 6-letter code under the QR also
   works.
6. Check-in only works on a vaccination day during check-in hours. For a demo
   at another time, first add that date under System Admin → Operating Hours
   → Add Exception (open, with hours that cover the demo).

If a phone can't open the page: make sure it's on the same network, type the
Network address into the phone's browser, and check that Windows Firewall
allowed Node.js. For a real deployment the site would run on a server with its
own address and https; then the Scan button shows a live camera view instead of
taking a photo.

The Vue DevTools button is off so it doesn't cover pages; to show it, start
the website with `$env:VUE_DEVTOOLS="on"; npm run dev` (PowerShell).

## Email and SMS

The system sends Forgot Password codes, vaccine reminders (14, 7, 5, 3 and 1 day
before, then 1, 5, 14 and 30 days after a missed dose), "vaccine given" and
"record updated" confirmations, stock notices, the weekly stock check, and
announcements. Everything always appears in the app; email and SMS go out once
they are set up.

| Message | App | Email | SMS |
|---|:-:|:-:|:-:|
| Reminders 14 / 7 / 5 / 3 / 1 days before, and 1 / 5 / 14 / 30 days after a missed dose | ✓ per dose | ✓ per dose | ✓ one text per child per step, listing the visit's vaccines |
| "Vaccine given", with the next due date | ✓ per dose | ✓ per dose | ✓ one text when the visit is finished: vaccines given + next vaccination |
| "Record updated" (what changed, so the parent can report a mistake) | ✓ | ✓ | ✓ |
| Vaccine out of stock / back in stock | ✓ | ✓ | ✓ |
| Health center closed (admin adds a closure under Operating Hours) | ✓ all parents | families due that day | families due that day |
| Announcements | ✓ | if ticked | if ticked |
| Forgot Password code | | email or SMS, the person chooses | |
| New-account and password-reset emails | | ✓ | |
| Weekly stock check (to Staff and Admin) | ✓ | ✓ | |

Texts are combined so one visit (often 3–4 vaccines) doesn't become 3–4 texts.
`Sms:DailyLimit` (default 50) stops texts for the rest of the day once reached,
most urgent first ("due tomorrow", "missed"); the app and email copies still go
out. Each text uses the load of the phone running TextBee, so keep it loaded
(an all-network unli-text promo is best).

**Email: Gmail (free, about 500 emails a day)**
1. Sign in to arugahealthcenter@gmail.com and turn on **2-Step Verification**
   (Google Account → Security).
2. Google Account → Security → **App passwords** → create one named "Aruga".
   Google shows a 16-letter password once.
3. Paste it in `appsettings.json` → `Email:Password` (the address is already in
   `Email:Username`). Restart the API. Don't send this password to anyone and
   don't commit it; `appsettings.json` is already ignored by git.

(EmailJS was also considered: its free plan is 200 emails a month with EmailJS
branding and needs a template set up on their site. Gmail directly is free,
has a much higher limit, and is already built in.)

**SMS: TextBee (free: 300 texts a month, 50 a day)**
TextBee sends texts through an Android phone with a SIM, so use the clinic's
phone with a load or unli-text promo.
1. Create an account at https://textbee.dev and copy your **API key**.
2. Install the TextBee app on the Android phone, sign in / scan the QR shown on
   the website, and allow it to send SMS. Keep the phone on and online.
3. Put the API key in `appsettings.json` → `Sms:TextBeeApiKey`
   (`Sms:Provider` is already "TextBee"). Restart the API.
   Only if you register more than one phone: put the phone's device ID in
   `Sms:TextBeeDeviceId`.

Semaphore (paid per message) also works: set `Sms:Provider` to "Semaphore" and
fill `Sms:SemaphoreApiKey`.

Before turning SMS on, make sure the parents in the database have real mobile
numbers: texts go to whatever number is on file.

While a channel isn't set up, the message (including Forgot Password codes)
is printed in this console window instead, so you can still test everything.
Demo accounts (…@demo.aruga.ph and the DemoSeed parents) and test records
(anyone whose email ends in @example.com or contains @demo.) never receive real
email or SMS, because their made-up numbers could belong to real people.

## Weekly stock check

Every **Wednesday at 8:00 AM** (change with `Inventory:StockCheckDay`), the
Admission Staff and the Administrator get the stock check in their bell and by
email: doses on hand vs. doses due this week, which vaccines to re-stock, and
about how many to order (enough for two weeks plus the minimum stock, in case
the pharmacy can only deliver next week). The same table is on the Staff
Inventory page any time, with a "Send to Staff & Admin now" button.

## Who can do what (enforced by the API, not just the screens)

| Role | Can |
|---|---|
| Parent | See only their own children, schedule, records and notifications; check in with the clinic QR |
| Admission Staff | Register families, manage the queue and who works in each room, check-in QR, stock check and stock notices, receive new batches |
| Doctor / Nurse | Record vaccinations and remarks for the patient at their station |
| System Administrator | Accounts, vaccines and schedule rules, inventory, clinic hours, vaccination rooms, announcements, audit logs |

Every call except login and Forgot Password needs a signed-in user.
