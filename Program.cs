using MediVault.Constants;
using MediVault.Data;
using MediVault.Services;
using MediVault.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<MediVaultContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsAssembly("MediVault")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MediVaultContext>()
    .AddSignInManager<CustomSignInManager>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<IntegrityService>();
builder.Services.AddScoped<RecordAccessService>();
builder.Services.AddScoped<AuditQueryService>();
builder.Services.AddScoped<PdfReportService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddTransient<IEmailSender, GmailEmailSender>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MediVaultContext>();
        await context.Database.MigrateAsync();
        await EnsureDatabaseSchemaAsync(context);
        
        // Enable all existing users
        var usersToEnable = await context.Users.Where(u => !u.IsEnabled).ToListAsync();
        if (usersToEnable.Any())
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Found {Count} users that need to be enabled", usersToEnable.Count);
            
            foreach (var user in usersToEnable)
            {
                user.IsEnabled = true;
                logger.LogInformation("Enabling user: {Email}", user.Email);
            }
            
            var updatedCount = await context.SaveChangesAsync();
            logger.LogInformation("Successfully enabled {Count} existing users", updatedCount);
        }
        
        await DbSeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var requestPath = exceptionFeature?.Path ?? context.Request.Path.Value ?? "unknown";

            logger.LogError(exceptionFeature?.Error, "Unhandled exception while processing {RequestPath}.", requestPath);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            if (context.Request.Path.StartsWithSegments("/api") || WantsJsonResponse(context.Request))
            {
                context.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = "The request could not be completed because an unexpected server error occurred."
                };

                await context.Response.WriteAsJsonAsync(problemDetails);
                return;
            }

            context.Response.Redirect("/Home/Error");
        });
    });
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com;";

    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<UserEnabledMiddleware>();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && context.Request.Path == "/")
    {
        context.Response.Redirect("/Dashboard");
        return;
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static bool WantsJsonResponse(HttpRequest request)
{
    var acceptHeader = request.Headers.Accept.ToString();
    return acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase)
        || acceptHeader.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
}

static async Task EnsureDatabaseSchemaAsync(MediVaultContext context)
{
    await context.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH('PatientRecords', 'AssignedDoctorId') IS NULL
        BEGIN
            ALTER TABLE [PatientRecords] ADD [AssignedDoctorId] nvarchar(max) NULL;
        END

        IF COL_LENGTH('PatientRecords', 'AssignedNurseId') IS NULL
        BEGIN
            ALTER TABLE [PatientRecords] ADD [AssignedNurseId] nvarchar(max) NULL;
        END

        IF COL_LENGTH('PatientRecords', 'IntegrityHash') IS NULL
        BEGIN
            ALTER TABLE [PatientRecords] ADD [IntegrityHash] nvarchar(max) NOT NULL
                CONSTRAINT [DF_PatientRecords_IntegrityHash] DEFAULT(N'');
        END

        IF COL_LENGTH('AuditLogs', 'UserEmail') IS NULL
        BEGIN
            ALTER TABLE [AuditLogs] ADD [UserEmail] nvarchar(256) NOT NULL
                CONSTRAINT [DF_AuditLogs_UserEmail] DEFAULT(N'');
        END

        UPDATE al
            SET al.UserEmail = COALESCE(au.Email, al.UserId)
        FROM [AuditLogs] al
        LEFT JOIN [AspNetUsers] au ON au.Id = al.UserId
        WHERE al.UserEmail = N''
        """);

    await context.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[Appointments]', N'U') IS NULL
        BEGIN
            CREATE TABLE [Appointments] (
                [Id] int NOT NULL IDENTITY,
                [PatientRecordId] int NOT NULL,
                [ScheduledAt] datetime2 NOT NULL,
                [Purpose] nvarchar(120) NOT NULL,
                [Status] nvarchar(40) NOT NULL,
                [CreatedByUserId] nvarchar(max) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Appointments_PatientRecords_PatientRecordId] FOREIGN KEY ([PatientRecordId]) REFERENCES [PatientRecords] ([Id]) ON DELETE CASCADE
            );
        END

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Appointments_PatientRecordId' AND object_id = OBJECT_ID(N'[Appointments]'))
        BEGIN
            CREATE INDEX [IX_Appointments_PatientRecordId] ON [Appointments] ([PatientRecordId]);
        END

        IF OBJECT_ID(N'[BillingRecords]', N'U') IS NULL
        BEGIN
            CREATE TABLE [BillingRecords] (
                [Id] int NOT NULL IDENTITY,
                [PatientRecordId] int NOT NULL,
                [Description] nvarchar(160) NOT NULL,
                [Amount] decimal(18,2) NOT NULL,
                [Status] nvarchar(40) NOT NULL,
                [CreatedByUserId] nvarchar(max) NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                CONSTRAINT [PK_BillingRecords] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_BillingRecords_PatientRecords_PatientRecordId] FOREIGN KEY ([PatientRecordId]) REFERENCES [PatientRecords] ([Id]) ON DELETE CASCADE
            );
        END

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BillingRecords_PatientRecordId' AND object_id = OBJECT_ID(N'[BillingRecords]'))
        BEGIN
            CREATE INDEX [IX_BillingRecords_PatientRecordId] ON [BillingRecords] ([PatientRecordId]);
        END
        """);
}
