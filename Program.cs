using MediVault.Constants;
using MediVault.Data;
using MediVault.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
        options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment();
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MediVaultContext>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<IntegrityService>();
builder.Services.AddScoped<RecordAccessService>();
builder.Services.AddScoped<AuditQueryService>();
builder.Services.AddScoped<PdfReportService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddTransient<IEmailSender, DevEmailSender>();
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
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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
        "script-src 'self' 'unsafe-inline';";

    await next();
});
app.UseRouting();
app.UseAuthentication();
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
