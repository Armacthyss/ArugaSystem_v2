public class CreateParentDto
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;

    // Nullable: a "contact-only" guardian (no portal access) can be
    // registered without an email.
    public string? Email { get; set; }

    public string ContactNo { get; set; } = string.Empty;
    public string? BarangayNo { get; set; }
    public string? Address { get; set; }

    // Optional client-supplied password. If omitted (and CreateLogin
    // is true), a temporary password is generated and returned to
    // the admin, same as before.
    public string? Password { get; set; }

    // Whether this guardian should get portal access (an Accounts row).
    // Defaults to true so existing callers that don't send this field
    // keep today's behavior.
    public bool CreateLogin { get; set; } = true;

    // Staff confirm the parent/guardian agreed to the Data Privacy Notice
    // (RA 10173). Registration is refused without it.
    public bool PrivacyConsent { get; set; }
}