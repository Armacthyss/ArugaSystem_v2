using AndroidWebAPI.Data;
using AndroidWebAPI.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;    
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AndroidWebAPI.DTOs;
using AndroidWebAPI.Repositories;
using AndroidWebAPI.Services;
var builder = WebApplication.CreateBuilder(args);

// Keep the console readable: hide the SQL text of every query, so messages
// like "[Email not set up]" and errors stand out.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Model.Validation", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Model", LogLevel.Error);


// ── Ports ────────────────────────────────────────────────────
builder.WebHost.UseUrls(
    "http://localhost:57147",
    "http://localhost:57148"
);

// ── Services ─────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddScoped<VaccineRepository>();
builder.Services.AddScoped<VaccineDoseRepository>();
builder.Services.AddScoped<VaccineInventoryRepository>();
builder.Services.AddScoped<IVaccinationTimelineRepository, VaccinationTimelineRepository>();
builder.Services.AddScoped<IQueueRepository, QueueRepository>();
builder.Services.AddScoped<IQueueQRSettingRepository, QueueQRSettingRepository>();
builder.Services.AddScoped<IQueueQRCodeRepository, QueueQRCodeRepository>();
builder.Services.AddScoped<IClinicOperatingScheduleRepository, ClinicOperatingScheduleRepository>();

builder.Services.AddScoped<
    IChildParentRelationshipRepository,
    ChildParentRelationshipRepository>();
builder.Services.AddScoped<IVaccinationRecordRepository, VaccinationRecordRepository>();

builder.Services.AddScoped<IVaccinationScheduleRuleRepository, VaccinationScheduleRuleRepository>();

builder.Services.AddScoped<IChildrenRepository, ChildrenRepository>();
builder.Services.AddScoped<ParentRepository>();          // ← only once
builder.Services.AddScoped<IAccountRepository, AccountRepositoryImpl>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<MessageSender>();   // Email + SMS (settings in appsettings.json)
builder.Services.AddScoped<ParentNotifier>();
builder.Services.AddDbContext<AppDbContext>(options =>   // ← only once
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!
                )
            )
        };
    });
// Every API call needs a signed-in user unless the endpoint says [AllowAnonymous]
// (login, Forgot Password). Role rules sit on the controllers themselves.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddHostedService<NotificationGeneratorService>();  // ← only once
// Queue QR Code
builder.Services.AddScoped<IQueueQRCodeService, QueueQRCodeService>();

// ── Build ─────────────────────────────────────────────────────
var app = builder.Build();

// New columns this version needs (added only if missing)
using (var scope = app.Services.CreateScope())
{
    try
    {
        await SchemaUpgrades.ApplyAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not update the database structure at startup.");
    }
}

// Every child needs a vaccination schedule, and no upcoming dose may sit on
// a day the clinic is closed (e.g. after the clinic hours were changed).
using (var scope = app.Services.CreateScope())
{
    var timelines = scope.ServiceProvider.GetRequiredService<IVaccinationTimelineRepository>();
    try
    {
        int built = await timelines.EnsureAllTimelinesAsync();
        int linked = await timelines.LinkGivenDosesAsync();
        int moved = await timelines.MoveDosesOffClosedDaysAsync();
        if (built + linked + moved > 0)
            app.Logger.LogInformation("Vaccination schedules: built {Built}, given doses linked {Linked}, moved off closed days {Moved}.", built, linked, moved);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Could not check the vaccination schedules at startup.");
    }
}

// The API runs on plain http://localhost:57147 (see UseUrls above), so there
// is no HTTPS port to redirect to.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowVueApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();