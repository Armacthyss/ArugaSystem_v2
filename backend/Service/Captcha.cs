using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace AndroidWebAPI.Services
{
    // Picture CAPTCHA for the public pages (Sign In, Forgot Password), so
    // bots can't keep guessing passwords or ask for reset codes (each texted
    // code uses the clinic SIM's load).
    //
    // Made here instead of Google reCAPTCHA / Cloudflare Turnstile: those need
    // an account and site keys, and they don't work when phones open the site
    // by the laptop's LAN address (http://192.168.x.x:5173), as in the demo.
    //
    // The characters are filled letter outlines, bent a little differently
    // each time, with random tilt, colours and noise. They are shapes, not
    // text, so the picture holds nothing a bot can simply read.
    // Each answer is kept 5 minutes and works once; a wrong or used answer
    // means a new picture.
    //
    // Switch off (e.g. for automated tests) with "Captcha": { "Enabled": false }.
    public class Captcha
    {
        private const int Length = 5;
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        private readonly IMemoryCache _cache;
        private readonly bool _enabled;

        public Captcha(IMemoryCache cache, IConfiguration config)
        {
            _cache = cache;
            _enabled = !string.Equals(config["Captcha:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
        }

        public bool Enabled => _enabled;

        public const string WrongAnswer = "The characters you typed don't match the picture. Please try the new one.";

        // A new challenge: its id and the picture (an SVG data URL)
        public (string Id, string Image) Create()
        {
            var answer = new StringBuilder();
            for (int i = 0; i < Length; i++)
                answer.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);

            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            _cache.Set(Key(id), answer.ToString(), Lifetime);

            var svg = Draw(answer.ToString());
            return (id, "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)));
        }

        // True when the answer matches. The challenge is used up either way.
        public bool Check(string? id, string? answer)
        {
            if (!_enabled) return true;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(answer)) return false;

            if (!_cache.TryGetValue(Key(id), out string? expected) || expected == null) return false;
            _cache.Remove(Key(id));

            var typed = new string(answer.Where(c => !char.IsWhiteSpace(c)).ToArray());
            return string.Equals(typed, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string Key(string id) => "captcha:" + id;

        // ── Drawing ───────────────────────────────────────────────
        // Letters and digits that can't be mistaken for each other
        // (no 0/O, 1/I/L, 2/Z, 5/S, 8/B).
        private const string Alphabet = "ACDEFHKMNPRTUVWXY3479";

        // Each character's outline from DejaVu Sans Bold (free font, Bitstream
        // Vera licence): (advance width, SVG path) at 100 units per em,
        // baseline at y = 0, capital height about 73. Every picture moves each
        // point a little, so the same letter never has the same outline twice.
        private static readonly Dictionary<char, (double Advance, string Path)> Glyphs = new()
        {
            ['A'] = (77.4, "M58 0L53.4 -13.3L24 -13.3L19.4 0L0.5 0L27.5 -72.9L49.9 -72.9L76.9 0L58 0M38.7 -55.8L28.7 -26.8L48.7 -26.8L38.7 -55.8"),
            ['C'] = (73.4, "M67 -19.1L67 -4Q61.8 -1.3 56.2 0Q50.6 1.4 44.5 1.4L44.5 1.4Q26.3 1.4 15.6 -8.8Q5 -18.9 5 -36.4L5 -36.4Q5 -53.9 15.6 -64Q26.3 -74.2 44.5 -74.2L44.5 -74.2Q50.6 -74.2 56.2 -72.9Q61.8 -71.5 67 -68.8L67 -68.8L67 -53.7Q61.8 -57.3 56.7 -58.9Q51.6 -60.6 46 -60.6L46 -60.6Q35.9 -60.6 30.2 -54.2Q24.4 -47.7 24.4 -36.4L24.4 -36.4Q24.4 -25.1 30.2 -18.7Q35.9 -12.2 46 -12.2L46 -12.2Q51.6 -12.2 56.7 -13.9Q61.8 -15.5 67 -19.1L67 -19.1"),
            ['D'] = (83, "M34.7 -58.7L28 -58.7L28 -14.2L34.7 -14.2Q46.2 -14.2 52.3 -19.9Q58.4 -25.6 58.4 -36.5L58.4 -36.5Q58.4 -47.4 52.3 -53Q46.3 -58.7 34.7 -58.7L34.7 -58.7M9.2 0L9.2 -72.9L29 -72.9Q45.6 -72.9 53.7 -70.5Q61.9 -68.2 67.7 -62.5L67.7 -62.5Q72.8 -57.6 75.3 -51.1Q77.8 -44.7 77.8 -36.5L77.8 -36.5Q77.8 -28.3 75.3 -21.8Q72.8 -15.3 67.7 -10.4L67.7 -10.4Q61.8 -4.7 53.6 -2.4Q45.4 0 29 0L29 0L9.2 0"),
            ['E'] = (68.3, "M9.2 0L9.2 -72.9L59.9 -72.9L59.9 -58.7L28 -58.7L28 -45.1L58 -45.1L58 -30.9L28 -30.9L28 -14.2L61 -14.2L61 0L9.2 0"),
            ['F'] = (68.3, "M9.2 0L9.2 -72.9L59.9 -72.9L59.9 -58.7L28 -58.7L28 -45.1L58 -45.1L58 -30.9L28 -30.9L28 0L9.2 0"),
            ['H'] = (83.7, "M9.2 0L9.2 -72.9L28 -72.9L28 -45.1L55.7 -45.1L55.7 -72.9L74.5 -72.9L74.5 0L55.7 0L55.7 -30.9L28 -30.9L28 0L9.2 0"),
            ['K'] = (77.5, "M9.2 0L9.2 -72.9L28 -72.9L28 -46.3L55.1 -72.9L76.9 -72.9L41.8 -38.4L80.5 0L57 0L28 -28.7L28 0L9.2 0"),
            ['M'] = (99.5, "M9.2 0L9.2 -72.9L33.1 -72.9L49.7 -33.9L66.4 -72.9L90.3 -72.9L90.3 0L72.5 0L72.5 -53.3L55.7 -14L43.8 -14L27 -53.3L27 0L9.2 0"),
            ['N'] = (83.7, "M9.2 0L9.2 -72.9L30.2 -72.9L56.7 -22.9L56.7 -72.9L74.5 -72.9L74.5 0L53.5 0L27 -50L27 0L9.2 0"),
            ['P'] = (73.3, "M9.2 0L9.2 -72.9L40.4 -72.9Q54.3 -72.9 61.7 -66.7Q69.2 -60.5 69.2 -49.1L69.2 -49.1Q69.2 -37.6 61.7 -31.5Q54.3 -25.3 40.4 -25.3L40.4 -25.3L28 -25.3L28 0L9.2 0M38.4 -59.3L28 -59.3L28 -38.9L38.4 -38.9Q43.8 -38.9 46.8 -41.6Q49.8 -44.2 49.8 -49.1L49.8 -49.1Q49.8 -54 46.8 -56.6Q43.8 -59.3 38.4 -59.3L38.4 -59.3"),
            ['R'] = (77, "M28 -40.6L35.9 -40.6Q41.8 -40.6 44.4 -42.8Q46.9 -45 46.9 -50L46.9 -50Q46.9 -55 44.4 -57.1Q41.8 -59.3 35.9 -59.3L35.9 -59.3L28 -59.3L28 -40.6M33.3 -27.6L28 -27.6L28 0L9.2 0L9.2 -72.9L37.9 -72.9Q52.3 -72.9 59 -68.1Q65.7 -63.2 65.7 -52.8L65.7 -52.8Q65.7 -45.6 62.2 -40.9Q58.7 -36.3 51.7 -34.1L51.7 -34.1Q55.6 -33.2 58.6 -30.1Q61.7 -27 64.8 -20.7L64.8 -20.7L75 0L55 0L46.1 -18.1Q43.4 -23.6 40.6 -25.6Q37.9 -27.6 33.3 -27.6L33.3 -27.6"),
            ['T'] = (68.2, "M0.5 -58.7L0.5 -72.9L67.7 -72.9L67.7 -58.7L43.5 -58.7L43.5 0L24.7 0L24.7 -58.7L0.5 -58.7"),
            ['U'] = (81.2, "M9.2 -29.2L9.2 -72.9L28 -72.9L28 -29.2Q28 -20.2 30.9 -16.3Q33.9 -12.4 40.6 -12.4L40.6 -12.4Q47.3 -12.4 50.3 -16.3Q53.2 -20.2 53.2 -29.2L53.2 -29.2L53.2 -72.9L72 -72.9L72 -29.2Q72 -13.7 64.3 -6.2Q56.5 1.4 40.6 1.4L40.6 1.4Q24.7 1.4 16.9 -6.2Q9.2 -13.7 9.2 -29.2L9.2 -29.2"),
            ['V'] = (77.4, "M27.5 0L0.5 -72.9L19.4 -72.9L38.7 -19.1L58 -72.9L76.9 -72.9L49.9 0L27.5 0"),
            ['W'] = (110.3, "M20.3 0L3 -72.9L21 -72.9L33.6 -19.9L46.1 -72.9L64.2 -72.9L76.7 -19.9L89.3 -72.9L107.2 -72.9L90 0L68.3 0L55.1 -55.4L42 0L20.3 0"),
            ['X'] = (77.1, "M74.1 -72.9L49.8 -37.2L75.1 0L55.5 0L38.5 -24.9L21.6 0L1.9 0L27.2 -37.2L2.9 -72.9L22.5 -72.9L38.5 -49.4L54.4 -72.9L74.1 -72.9"),
            ['Y'] = (72.4, "M26.8 -30.7L-1 -72.9L19.6 -72.9L36.2 -46.9L52.8 -72.9L73.4 -72.9L45.6 -30.7L45.6 0L26.8 0L26.8 -30.7"),
            ['3'] = (69.6, "M46.6 -39.3L46.6 -39.3Q54 -37.4 57.8 -32.7Q61.6 -28 61.6 -20.7L61.6 -20.7Q61.6 -9.9 53.3 -4.2Q45 1.4 29.1 1.4L29.1 1.4Q23.5 1.4 17.8 0.5Q12.2 -0.4 6.7 -2.2L6.7 -2.2L6.7 -16.7Q12 -14.1 17.2 -12.7Q22.4 -11.4 27.4 -11.4L27.4 -11.4Q34.9 -11.4 38.8 -14Q42.8 -16.6 42.8 -21.4L42.8 -21.4Q42.8 -26.4 38.7 -28.9Q34.7 -31.5 26.7 -31.5L26.7 -31.5L19.2 -31.5L19.2 -43.6L27.1 -43.6Q34.2 -43.6 37.6 -45.8Q41.1 -48 41.1 -52.6L41.1 -52.6Q41.1 -56.8 37.7 -59.1Q34.4 -61.4 28.2 -61.4L28.2 -61.4Q23.7 -61.4 19 -60.4Q14.4 -59.3 9.8 -57.3L9.8 -57.3L9.8 -71.1Q15.4 -72.7 20.8 -73.4Q26.3 -74.2 31.6 -74.2L31.6 -74.2Q45.8 -74.2 52.9 -69.6Q59.9 -64.9 59.9 -55.5L59.9 -55.5Q59.9 -49.1 56.5 -45Q53.2 -41 46.6 -39.3"),
            ['4'] = (69.6, "M36.8 -26.9L36.8 -57.4L16.2 -26.9L36.8 -26.9M4.5 -29.4L33.7 -72.9L54.6 -72.9L54.6 -26.9L65 -26.9L65 -13.3L54.6 -13.3L54.6 0L36.8 0L36.8 -13.3L4.5 -13.3L4.5 -29.4"),
            ['7'] = (69.6, "M6.7 -59.1L6.7 -72.9L61.6 -72.9L61.6 -62.3L33.2 0L14.9 0L41.8 -59.1L6.7 -59.1"),
            ['9'] = (69.6, "M10 -1.6L10 -1.6L10 -15.1Q14.5 -13 18.6 -11.9Q22.7 -10.9 26.7 -10.9L26.7 -10.9Q35.1 -10.9 39.8 -15.6Q44.5 -20.2 45.3 -29.4L45.3 -29.4Q42 -27 38.2 -25.7Q34.5 -24.5 30.1 -24.5L30.1 -24.5Q18.9 -24.5 12 -31Q5.2 -37.5 5.2 -48.2L5.2 -48.2Q5.2 -60 12.8 -67Q20.5 -74.1 33.3 -74.1L33.3 -74.1Q47.6 -74.1 55.4 -64.5Q63.2 -54.9 63.2 -37.3L63.2 -37.3Q63.2 -19.2 54.1 -8.9Q44.9 1.4 29 1.4L29 1.4Q23.9 1.4 19.2 0.7Q14.5 -0.1 10 -1.6M33.2 -36.7L33.2 -36.7Q38.1 -36.7 40.6 -39.9Q43.1 -43.1 43.1 -49.5L43.1 -49.5Q43.1 -55.9 40.6 -59.1Q38.1 -62.3 33.2 -62.3L33.2 -62.3Q28.3 -62.3 25.8 -59.1Q23.3 -55.9 23.3 -49.5L23.3 -49.5Q23.3 -43.1 25.8 -39.9Q28.3 -36.7 33.2 -36.7"),
        };

        // Dark, easy-to-read ink colours (Aruga greens, navy, plum, brown)
        private static readonly string[] Inks = { "#1f5132", "#2d3a26", "#1e3a5f", "#4a2c6b", "#6b2f2f", "#3f3a1f" };

        private const int Width = 320, Height = 72;

        private static string Draw(string text)
        {
            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\">");

            // No background of its own: the picture sits on the page's CAPTCHA
            // box. Faint curves and dots behind the characters.
            for (int i = 0; i < 5; i++)
                sb.Append($"<path d=\"M-10 {N(R(0, Height))} Q{N(R(Width * 0.25, Width * 0.75))} {N(R(-30, Height + 30))} {Width + 10} {N(R(0, Height))}\" stroke=\"{Ink(0.18)}\" stroke-width=\"{N(R(1, 2.5))}\" fill=\"none\"/>");
            for (int i = 0; i < 30; i++)
                sb.Append($"<circle cx=\"{N(R(0, Width))}\" cy=\"{N(R(0, Height))}\" r=\"{N(R(0.6, 1.8))}\" fill=\"{Ink(0.22)}\"/>");

            // Lay the characters out, then centre the word
            var scales = text.Select(_ => R(0.46, 0.52)).ToArray();
            var gaps = text.Select(_ => R(3, 8)).ToArray();
            double total = text.Select((c, i) => Glyphs[c].Advance * scales[i] + gaps[i]).Sum();
            // Wide words (M, W...) are made smaller so nothing is cut off
            double room = Width - 36;
            if (total > room)
            {
                for (int i = 0; i < scales.Length; i++) scales[i] *= room / total;
                total = room;
            }
            double x = (Width - total) / 2;

            for (int i = 0; i < text.Length; i++)
            {
                var (advance, path) = Glyphs[text[i]];
                double s = scales[i];
                double baseline = Height / 2.0 + 73 * s / 2 + R(-3, 3);
                double angle = R(-14, 14) * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
                double skew = Math.Tan(R(-12, 12) * Math.PI / 180);
                // rotate around the middle of the letter
                double cx = x + advance * s / 2, cy = baseline - 73 * s / 2;

                (double, double) Map(double gx, double gy)
                {
                    gx += R(-1.6, 1.6); gy += R(-1.6, 1.6);          // wobble every point
                    double px = x + (gx - gy * skew) * s, py = baseline + gy * s;
                    double dx = px - cx, dy = py - cy;
                    return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
                }

                sb.Append("<path d=\"");
                var tokens = System.Text.RegularExpressions.Regex.Matches(path, "[MLQCZ]|-?[0-9.]+");
                int t = 0;
                while (t < tokens.Count)
                {
                    string cmd = tokens[t++].Value;
                    int pairs = cmd switch { "M" or "L" => 1, "Q" => 2, "C" => 3, _ => 0 };
                    sb.Append(cmd);
                    for (int p = 0; p < pairs; p++)
                    {
                        var (mx, my) = Map(double.Parse(tokens[t].Value, CultureInfo.InvariantCulture), double.Parse(tokens[t + 1].Value, CultureInfo.InvariantCulture));
                        t += 2;
                        sb.Append(p == 0 ? "" : " ").Append(N(mx)).Append(' ').Append(N(my));
                    }
                }
                sb.Append($"\" fill=\"{Inks[RandomNumberGenerator.GetInt32(Inks.Length)]}\"/>");

                x += advance * s + gaps[i];
            }

            // Two thin lines through the characters
            for (int i = 0; i < 2; i++)
                sb.Append($"<path d=\"M-5 {N(R(22, Height - 22))} C{N(R(Width * 0.2, Width * 0.4))} {N(R(5, Height - 5))} {N(R(Width * 0.6, Width * 0.8))} {N(R(5, Height - 5))} {Width + 5} {N(R(22, Height - 22))}\" stroke=\"{Ink(0.55)}\" stroke-width=\"{N(R(1.2, 1.8))}\" fill=\"none\"/>");

            sb.Append("</svg>");
            return sb.ToString();
        }

        private static double R(double min, double max) =>
            min + (max - min) * (RandomNumberGenerator.GetInt32(10_000) / 10_000.0);

        private static string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Ink(double opacity)
        {
            var c = Inks[RandomNumberGenerator.GetInt32(Inks.Length)];
            return $"{c}{(int)(opacity * 255):x2}";
        }
    }
}
