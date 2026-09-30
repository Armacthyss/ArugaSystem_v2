using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;

namespace AndroidWebAPI.Services
{
    // Sends the Email and SMS messages the system promises parents and staff
    // (Forgot Password codes, vaccine reminders, announcements...).
    //
    // Both channels are switched on from configuration, so no password or API
    // key ever has to live in the code. Put them in appsettings.json (it is
    // ignored by git; see appsettings.example.json).
    //
    //   "Email": { "Host": "smtp.gmail.com", "Port": 587,
    //              "Username": "arugahealthcenter@gmail.com",
    //              "Password": "<16-letter Gmail App Password>",
    //              "FromName": "Leveriza Health Center" }
    //
    //   "Sms":   { "Provider": "TextBee",           (or "Semaphore")
    //              "TextBeeApiKey": "<key from textbee.dev>",
    //              "TextBeeDeviceId": "",           (optional: which phone)
    //              "SemaphoreApiKey": "", "SenderName": "",
    //              "DailyLimit": 50 }               (TextBee free plan: 50/day)
    //
    // TextBee sends the texts through an Android phone with a SIM (the
    // clinic's phone, running the TextBee app), so each text costs whatever
    // the SIM's load/promo charges. Semaphore is a paid SMS gateway.
    //
    // When a channel isn't set up, the message is written to the backend
    // console instead (Development only), so everything can still be tested
    // on a laptop. On a real server an unconfigured channel counts as "not sent".
    public class MessageSender
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<MessageSender> _logger;
        private readonly bool _isDevelopment;

        // Texts sent today, to stay under the provider's daily limit
        private static readonly object SmsCountLock = new();
        private static DateTime _smsCountDay = DateTime.Today;
        private static int _smsCountToday;

        public MessageSender(IConfiguration config, IHttpClientFactory httpFactory, ILogger<MessageSender> logger, IHostEnvironment env)
        {
            _config = config;
            _httpFactory = httpFactory;
            _logger = logger;
            _isDevelopment = env.IsDevelopment();
        }

        public bool EmailEnabled =>
            !string.IsNullOrWhiteSpace(_config["Email:Host"]) &&
            !string.IsNullOrWhiteSpace(_config["Email:Username"]) &&
            !string.IsNullOrWhiteSpace(_config["Email:Password"]);

        // "TextBee" or "Semaphore": the one named in Sms:Provider, otherwise
        // whichever has a key filled in (TextBee first).
        public string? SmsProvider
        {
            get
            {
                bool textBee = !string.IsNullOrWhiteSpace(_config["Sms:TextBeeApiKey"]);
                bool semaphore = !string.IsNullOrWhiteSpace(_config["Sms:SemaphoreApiKey"]);
                var chosen = _config["Sms:Provider"];
                if (string.Equals(chosen, "Semaphore", StringComparison.OrdinalIgnoreCase)) return semaphore ? "Semaphore" : null;
                if (string.Equals(chosen, "TextBee", StringComparison.OrdinalIgnoreCase)) return textBee ? "TextBee" : null;
                return textBee ? "TextBee" : semaphore ? "Semaphore" : null;
            }
        }

        public bool SmsEnabled => SmsProvider != null;

        public int SmsDailyLimit => int.TryParse(_config["Sms:DailyLimit"], out var n) && n > 0 ? n : 50;

        // Texts handed to the SMS provider today (since the API started), for
        // the Super Admin dashboard
        public static int SmsSentToday
        {
            get { lock (SmsCountLock) return _smsCountDay == DateTime.Today ? _smsCountToday : 0; }
        }

        // Reserves one text from today's allowance; false when it's used up
        private bool TakeSmsAllowance()
        {
            lock (SmsCountLock)
            {
                if (_smsCountDay != DateTime.Today) { _smsCountDay = DateTime.Today; _smsCountToday = 0; }
                if (_smsCountToday >= SmsDailyLimit) return false;
                _smsCountToday++;
                return true;
            }
        }

        // Returns true when the message was handed to the mail server (or, on a
        // development laptop without email set up, written to the console).
        // Made-up addresses used for testing. A person with one of these is a
        // test record, so their phone number is made up too and must not be
        // texted: it could belong to a real stranger.
        public static bool IsTestAddress(string? email) =>
            !string.IsNullOrWhiteSpace(email) &&
            (email.Contains("@demo.", StringComparison.OrdinalIgnoreCase) ||
             email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase));

        public async Task<bool> SendEmailAsync(string? to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to) || !to.Contains('@')) return false;

            // Test/demo addresses (…@demo.aruga.ph, …@demo.com) don't exist;
            // sending to them would only fill the clinic inbox with bounces.
            bool testAddress = IsTestAddress(to);

            if (!EmailEnabled || testAddress)
            {
                _logger.LogWarning("[{Reason}] To: {To} | {Subject}\n{Body}",
                    testAddress ? "Email not sent: test address" : "Email not set up", to, subject, body);
                return _isDevelopment;
            }

            try
            {
                var from = _config["Email:FromAddress"];
                if (string.IsNullOrWhiteSpace(from)) from = _config["Email:Username"]!;
                var fromName = _config["Email:FromName"] ?? "Leveriza Health Center";

                using var message = new MailMessage
                {
                    From = new MailAddress(from, fromName),
                    Subject = subject,
                    Body = Wrap(subject, body),
                    IsBodyHtml = true,
                };
                message.To.Add(to.Trim());

                using var client = new SmtpClient(_config["Email:Host"], int.TryParse(_config["Email:Port"], out var port) ? port : 587)
                {
                    EnableSsl = !string.Equals(_config["Email:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase),
                    Credentials = new NetworkCredential(_config["Email:Username"], _config["Email:Password"]),
                    Timeout = 15000,
                };
                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send email to {To}", to);
                return false;
            }
        }

        // demoRecipient: a demo account (DemoSeed.sql) whose made-up number could
        // belong to a real person, so the text is only printed, never sent.
        public async Task<bool> SendSmsAsync(string? number, string text, bool demoRecipient = false)
        {
            var normalized = NormalizePhNumber(number);
            if (normalized == null) return false;

            if (!SmsEnabled || demoRecipient)
            {
                _logger.LogWarning("[{Reason}] To: {Number}\n{Text}",
                    demoRecipient ? "SMS not sent: demo account" : "SMS not set up", normalized, text);
                return _isDevelopment || demoRecipient;
            }

            if (!TakeSmsAllowance())
            {
                _logger.LogWarning("[SMS not sent: daily limit of {Limit} reached] To: {Number}\n{Text}", SmsDailyLimit, normalized, text);
                return false;
            }

            // Plain characters keep a text at 160 characters per SMS; one "·" or
            // "–" would switch the whole message to Unicode (70 per SMS).
            text = text.Replace(" · ", ", ").Replace('·', '-').Replace('–', '-').Replace('—', '-')
                       .Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"')
                       .Replace("…", "...");
            var message = text.Length > 450 ? text[..447] + "..." : text;

            try
            {
                var client = _httpFactory.CreateClient();
                HttpResponseMessage res;

                if (SmsProvider == "TextBee")
                {
                    // https://textbee.dev: POST /api/v1/gateway/send-sms, header x-api-key
                    var body = new Dictionary<string, object>
                    {
                        ["recipients"] = new[] { "+63" + normalized[1..] },   // 09171234567 -> +639171234567
                        ["message"] = message,
                    };
                    var deviceId = _config["Sms:TextBeeDeviceId"];
                    if (!string.IsNullOrWhiteSpace(deviceId)) body["deviceId"] = deviceId;

                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.textbee.dev/api/v1/gateway/send-sms")
                    {
                        Content = JsonContent.Create(body),
                    };
                    request.Headers.Add("x-api-key", _config["Sms:TextBeeApiKey"]);
                    res = await client.SendAsync(request);
                }
                else
                {
                    var form = new Dictionary<string, string>
                    {
                        ["apikey"] = _config["Sms:SemaphoreApiKey"]!,
                        ["number"] = normalized,
                        ["message"] = message,
                    };
                    var sender = _config["Sms:SenderName"];
                    if (!string.IsNullOrWhiteSpace(sender)) form["sendername"] = sender;
                    res = await client.PostAsync("https://api.semaphore.co/api/v4/messages", new FormUrlEncodedContent(form));
                }

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogError("SMS to {Number} failed: {Status} {Body}", normalized, res.StatusCode, await res.Content.ReadAsStringAsync());
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send SMS to {Number}", normalized);
                return false;
            }
        }

        // 0917 123 4567 / +63 917 123 4567 / 639171234567  ->  09171234567
        public static string? NormalizePhNumber(string? number)
        {
            if (string.IsNullOrWhiteSpace(number)) return null;
            var digits = new string(number.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("63") && digits.Length == 12) digits = "0" + digits[2..];
            if (digits.Length == 10 && digits.StartsWith("9")) digits = "0" + digits;
            return digits.Length == 11 && digits.StartsWith("09") ? digits : null;
        }

        // "maria.santos@gmail.com" -> "m*********@gmail.com"
        public static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 1) return email;
            return email[0] + new string('*', at - 1) + email[at..];
        }

        // "09171234567" -> "0917*****67"
        public static string MaskPhone(string phone) =>
            phone.Length < 7 ? phone : phone[..4] + new string('*', phone.Length - 6) + phone[^2..];

        private static string Wrap(string title, string body)
        {
            var html = WebUtility.HtmlEncode(body).Replace("\n", "<br>");
            return $@"<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e7e5e4;border-radius:12px;overflow:hidden"">
  <div style=""background:#546b41;color:#fff;padding:16px 20px"">
    <div style=""font-size:12px;opacity:.85"">Leveriza Health Center · Aruga</div>
    <div style=""font-size:18px;font-weight:700"">{WebUtility.HtmlEncode(title)}</div>
  </div>
  <div style=""padding:20px;color:#292524;font-size:14px;line-height:1.6"">{html}</div>
  <div style=""padding:12px 20px;background:#fafaf9;color:#78716c;font-size:11px"">This is an automated message from the Aruga Pediatric Health Record System. Please do not reply.</div>
</div>";
        }
    }
}
