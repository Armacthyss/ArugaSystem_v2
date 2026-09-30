namespace AndroidWebAPI.Services
{
    // Where a dose can be given, as the Nurse picks it when recording a
    // vaccination (asked for by City Hall, so a swelling can be matched to
    // the vaccine given there). Same list as frontend src/utils/injectionSites.js.
    public static class InjectionSites
    {
        public static readonly string[] All =
        {
            "Left thigh", "Right thigh",
            "Left upper arm", "Right upper arm",
            "Mouth (oral)",
        };

        // The saved spelling of a site, null when none was picked, or
        // false when it isn't one of the sites above.
        public static bool TryNormalize(string? site, out string? normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(site)) return true;
            normalized = All.FirstOrDefault(s => string.Equals(s, site.Trim(), StringComparison.OrdinalIgnoreCase));
            return normalized != null;
        }

        // "in the left thigh" / "by mouth", for messages to parents
        public static string Phrase(string site) =>
            site == "Mouth (oral)" ? "by mouth" : $"in the {site.ToLowerInvariant()}";
    }
}
