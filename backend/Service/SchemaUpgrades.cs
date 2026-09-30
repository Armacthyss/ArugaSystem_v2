using AndroidWebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // Small additions to the database that the API makes by itself at
    // startup, so a teammate's older ArugaSystemDB keeps working without
    // running a script first. Each step only runs when it is still missing.
    // Schema.sql already has all of them for new databases.
    public static class SchemaUpgrades
    {
        public static async Task ApplyAsync(AppDbContext context)
        {
            // Revision, Sep 2026: Data Privacy Act consent (see Parent.cs)
            await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Parents', 'ConsentRecordedAt') IS NULL
    ALTER TABLE dbo.Parents ADD ConsentRecordedAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Parents', 'PrivacyConsentAt') IS NULL
    ALTER TABLE dbo.Parents ADD PrivacyConsentAt DATETIME2 NULL;");

            // Oct 2026: where each dose was given (see VaccinationRecord.cs)
            await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.VaccinationRecords', 'InjectionSite') IS NULL
    ALTER TABLE dbo.VaccinationRecords ADD InjectionSite NVARCHAR(30) NULL;");
        }
    }
}
