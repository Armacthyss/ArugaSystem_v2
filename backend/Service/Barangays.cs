namespace AndroidWebAPI.Services
{
    // Leveriza Health Center only serves Barangays 19 and 21–40. Families
    // from any other barangay are outside its jurisdiction and can't be
    // registered (the frontend has the same list in utils/barangays.js).
    public static class Barangays
    {
        public static readonly int[] Served =
            new[] { 19 }.Concat(Enumerable.Range(21, 20)).ToArray();

        public const string Error =
            "Leveriza Health Center only serves Barangays 19 and 21–40. This barangay is outside its jurisdiction.";

        // Empty is allowed (not every form asks for it)
        public static bool IsServed(int? barangay) =>
            barangay == null || Served.Contains(barangay.Value);

        public static bool IsServed(string? barangay)
        {
            if (string.IsNullOrWhiteSpace(barangay)) return true;
            return int.TryParse(barangay.Trim(), out var n) && Served.Contains(n);
        }
    }
}
