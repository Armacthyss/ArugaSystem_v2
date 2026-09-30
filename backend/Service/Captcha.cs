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
    // The characters are drawn as bent lines with random tilt, colours and
    // noise, not as text, so the picture holds nothing a bot can simply read.
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

        // Each character as strokes on a 4 x 6 grid (x right, y down)
        private static readonly Dictionary<char, double[][]> Glyphs = new()
        {
            ['A'] = new[] { new double[] { 0,6, 2,0, 4,6 }, new double[] { 0.8,3.8, 3.2,3.8 } },
            ['C'] = new[] { new double[] { 4,1, 3,0, 1,0, 0,1, 0,5, 1,6, 3,6, 4,5 } },
            ['D'] = new[] { new double[] { 0,0, 0,6, 2.5,6, 4,4.5, 4,1.5, 2.5,0, 0,0 } },
            ['E'] = new[] { new double[] { 4,0, 0,0, 0,6, 4,6 }, new double[] { 0,3, 3,3 } },
            ['F'] = new[] { new double[] { 4,0, 0,0, 0,6 }, new double[] { 0,3, 3,3 } },
            ['H'] = new[] { new double[] { 0,0, 0,6 }, new double[] { 4,0, 4,6 }, new double[] { 0,3, 4,3 } },
            ['K'] = new[] { new double[] { 0,0, 0,6 }, new double[] { 4,0, 0,3.5 }, new double[] { 1.4,2.6, 4,6 } },
            ['M'] = new[] { new double[] { 0,6, 0,0, 2,3.5, 4,0, 4,6 } },
            ['N'] = new[] { new double[] { 0,6, 0,0, 4,6, 4,0 } },
            ['P'] = new[] { new double[] { 0,6, 0,0, 3,0, 4,1, 4,2.5, 3,3.5, 0,3.5 } },
            ['R'] = new[] { new double[] { 0,6, 0,0, 3,0, 4,1, 4,2.5, 3,3.5, 0,3.5 }, new double[] { 2,3.5, 4,6 } },
            ['T'] = new[] { new double[] { 0,0, 4,0 }, new double[] { 2,0, 2,6 } },
            ['U'] = new[] { new double[] { 0,0, 0,5, 1,6, 3,6, 4,5, 4,0 } },
            ['V'] = new[] { new double[] { 0,0, 2,6, 4,0 } },
            ['W'] = new[] { new double[] { 0,0, 1,6, 2,2.5, 3,6, 4,0 } },
            ['X'] = new[] { new double[] { 0,0, 4,6 }, new double[] { 4,0, 0,6 } },
            ['Y'] = new[] { new double[] { 0,0, 2,3, 4,0 }, new double[] { 2,3, 2,6 } },
            ['3'] = new[] { new double[] { 0,0.6, 1,0, 3,0, 4,1, 4,2, 3,3, 1.5,3 }, new double[] { 3,3, 4,4, 4,5, 3,6, 1,6, 0,5.4 } },
            ['4'] = new[] { new double[] { 3,6, 3,0, 0,4, 4,4 } },
            ['7'] = new[] { new double[] { 0,0, 4,0, 1.5,6 } },
            ['9'] = new[] { new double[] { 4,3, 1,3, 0,2, 0,1, 1,0, 3,0, 4,1, 4,5, 3,6, 0.5,6 } },
        };

        private static readonly string[] Inks = { "#1f3b2d", "#2d3a26", "#3b2f6b", "#6b2f2f", "#1f4b6b", "#4b3b1f" };

        private const int Width = 200, Height = 64;

        private static string Draw(string text)
        {
            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\">");
            sb.Append($"<rect width=\"{Width}\" height=\"{Height}\" fill=\"#f7f3ea\"/>");

            // Background noise: faint curves and dots
            for (int i = 0; i < 6; i++)
                sb.Append($"<path d=\"M{N(R(0, 20))} {N(R(0, Height))} Q{N(R(40, 160))} {N(R(-20, Height + 20))} {N(R(180, Width))} {N(R(0, Height))}\" stroke=\"{Ink(0.35)}\" stroke-width=\"{N(R(1, 2))}\" fill=\"none\"/>");
            for (int i = 0; i < 40; i++)
                sb.Append($"<circle cx=\"{N(R(0, Width))}\" cy=\"{N(R(0, Height))}\" r=\"{N(R(0.6, 1.6))}\" fill=\"{Ink(0.4)}\"/>");

            // The characters
            double step = (Width - 30) / (double)text.Length;
            for (int i = 0; i < text.Length; i++)
            {
                double scale = R(5.2, 6.4);
                double x = 18 + i * step + R(-2, 2);
                double y = (Height - 6 * scale) / 2 + R(-4, 4);
                double angle = R(-18, 18);
                double cx = x + 2 * scale, cy = y + 3 * scale;
                string ink = Inks[RandomNumberGenerator.GetInt32(Inks.Length)];

                sb.Append($"<g transform=\"rotate({N(angle)} {N(cx)} {N(cy)})\" stroke=\"{ink}\" stroke-width=\"{N(R(2.6, 3.4))}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\">");
                foreach (var stroke in Glyphs[text[i]])
                {
                    sb.Append("<path d=\"");
                    for (int p = 0; p < stroke.Length; p += 2)
                    {
                        double px = x + (stroke[p] + R(-0.22, 0.22)) * scale;
                        double py = y + (stroke[p + 1] + R(-0.22, 0.22)) * scale;
                        sb.Append(p == 0 ? "M" : " L").Append(N(px)).Append(' ').Append(N(py));
                    }
                    sb.Append("\"/>");
                }
                sb.Append("</g>");
            }

            // Two lines across the characters
            for (int i = 0; i < 2; i++)
                sb.Append($"<path d=\"M0 {N(R(15, Height - 15))} C{N(R(40, 80))} {N(R(0, Height))} {N(R(120, 160))} {N(R(0, Height))} {Width} {N(R(15, Height - 15))}\" stroke=\"{Ink(0.7)}\" stroke-width=\"1.6\" fill=\"none\"/>");

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
