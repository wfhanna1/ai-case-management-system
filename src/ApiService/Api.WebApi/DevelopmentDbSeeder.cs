using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.WebApi;

/// <summary>
/// Seeds demo data on startup in the Development environment.
/// Registered as an IHostedService so Program.cs stays clean.
/// </summary>
public sealed class DevelopmentDbSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevelopmentDbSeeder> _logger;

    public DevelopmentDbSeeder(IServiceScopeFactory scopeFactory, ILogger<DevelopmentDbSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync(cancellationToken);

        await SeedDocumentsAsync(db);
        await SeedUsersAsync(db, hasher);
        await SeedFormTemplatesAsync(db);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedDocumentsAsync(IntakeDbContext db)
    {
        if (await db.Documents.IgnoreQueryFilters().AnyAsync())
            return;

        var alpha = new TenantId(DemoTenants.AlphaTenantId);
        var beta = new TenantId(DemoTenants.BetaTenantId);

        db.Documents.Add(IntakeDocument.Submit(alpha, "alpha-intake-form.pdf", "demo/alpha/alpha-intake-form.pdf"));
        db.Documents.Add(IntakeDocument.Submit(beta, "beta-claim-form.pdf", "demo/beta/beta-claim-form.pdf"));

        await db.SaveChangesAsync();
        _logger.LogInformation("Seeded demo documents for Alpha and Beta tenants");
    }

    private async Task SeedFormTemplatesAsync(IntakeDbContext db)
    {
        if (await db.FormTemplates.IgnoreQueryFilters().AnyAsync())
            return;

        var alpha = new TenantId(DemoTenants.AlphaTenantId);
        var beta = new TenantId(DemoTenants.BetaTenantId);

        // Seed templates for both tenants (fresh field instances per template to avoid EF tracking conflicts)
        foreach (var tenantId in new[] { alpha, beta })
        {
            db.FormTemplates.AddRange(
                FormTemplate.Create(tenantId, "Child Welfare Intake",
                    "Standard intake form for child welfare referrals and investigations",
                    TemplateType.ChildWelfare, CreateChildWelfareFields()),
                FormTemplate.Create(tenantId, "Adult Protective Services",
                    "Intake form for adult protective services cases including abuse and neglect reports",
                    TemplateType.AdultProtective, CreateAdultProtectiveFields()),
                FormTemplate.Create(tenantId, "Housing Assistance Application",
                    "Application form for housing assistance programs and emergency shelter services",
                    TemplateType.HousingAssistance, CreateHousingFields()),
                FormTemplate.Create(tenantId, "Mental Health Referral",
                    "Referral form for mental health services and psychiatric evaluation",
                    TemplateType.MentalHealthReferral, CreateMentalHealthFields())
            );
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Seeded form templates for Alpha and Beta tenants");
    }

    private static List<TemplateField> CreateChildWelfareFields() =>
    [
        new("Child Full Name", FieldType.Text, true, null),
        new("Date of Birth", FieldType.Date, true, null),
        new("Age", FieldType.Number, true, null),
        new("Gender", FieldType.Select, true, "[\"Male\",\"Female\",\"Non-binary\",\"Prefer not to say\"]"),
        new("Parent/Guardian Name", FieldType.Text, true, null),
        new("Home Address", FieldType.TextArea, true, null),
        new("Phone Number", FieldType.Text, true, null),
        new("Reason for Referral", FieldType.TextArea, true, null),
        new("Immediate Safety Concerns", FieldType.Checkbox, true, null),
        new("Previous CPS Involvement", FieldType.Checkbox, false, null)
    ];

    private static List<TemplateField> CreateAdultProtectiveFields() =>
    [
        new("Client Full Name", FieldType.Text, true, null),
        new("Date of Birth", FieldType.Date, true, null),
        new("Social Security Number (last 4)", FieldType.Text, false, null),
        new("Living Situation", FieldType.Select, true, "[\"Alone\",\"With family\",\"Assisted living\",\"Nursing home\",\"Homeless\"]"),
        new("Type of Abuse/Neglect", FieldType.Select, true, "[\"Physical\",\"Emotional\",\"Financial\",\"Neglect\",\"Sexual\",\"Self-neglect\"]"),
        new("Reporter Name", FieldType.Text, true, null),
        new("Reporter Relationship", FieldType.Text, true, null),
        new("Incident Description", FieldType.TextArea, true, null),
        new("Client Able to Communicate", FieldType.Checkbox, false, null)
    ];

    private static List<TemplateField> CreateHousingFields() =>
    [
        new("Applicant Full Name", FieldType.Text, true, null),
        new("Date of Application", FieldType.Date, true, null),
        new("Number of Household Members", FieldType.Number, true, null),
        new("Monthly Income", FieldType.Number, true, null),
        new("Current Housing Status", FieldType.Select, true, "[\"Renting\",\"Own home\",\"Homeless\",\"Temporary shelter\",\"Living with others\"]"),
        new("Type of Assistance Needed", FieldType.Select, true, "[\"Rental assistance\",\"Mortgage assistance\",\"Emergency shelter\",\"Transitional housing\"]"),
        new("Reason for Housing Need", FieldType.TextArea, true, null),
        new("Currently Employed", FieldType.Checkbox, false, null)
    ];

    private static List<TemplateField> CreateMentalHealthFields() =>
    [
        new("Patient Full Name", FieldType.Text, true, null),
        new("Date of Birth", FieldType.Date, true, null),
        new("Referring Provider", FieldType.Text, true, null),
        new("Primary Diagnosis", FieldType.Text, false, null),
        new("Urgency Level", FieldType.Select, true, "[\"Routine\",\"Urgent\",\"Emergency\"]"),
        new("Current Medications", FieldType.TextArea, false, null),
        new("Presenting Symptoms", FieldType.TextArea, true, null),
        new("Previous Mental Health Treatment", FieldType.Checkbox, false, null),
        new("Suicidal Ideation Screening", FieldType.Checkbox, true, null),
        new("Insurance Provider", FieldType.Text, false, null)
    ];

    private async Task SeedUsersAsync(IntakeDbContext db, IPasswordHasher passwordHasher)
    {
        if (await db.Users.AnyAsync())
            return;

        var hash = passwordHasher.Hash(DemoTenants.DemoPassword);
        var alpha = new TenantId(DemoTenants.AlphaTenantId);
        var beta = new TenantId(DemoTenants.BetaTenantId);

        db.Users.AddRange(
            User.Register(alpha, "admin@alpha.demo", hash, [UserRole.Admin]),
            User.Register(alpha, "worker@alpha.demo", hash, [UserRole.IntakeWorker]),
            User.Register(alpha, "reviewer@alpha.demo", hash, [UserRole.Reviewer]),
            User.Register(beta, "admin@beta.demo", hash, [UserRole.Admin]),
            User.Register(beta, "worker@beta.demo", hash, [UserRole.IntakeWorker]),
            User.Register(beta, "reviewer@beta.demo", hash, [UserRole.Reviewer])
        );

        await db.SaveChangesAsync();
        _logger.LogInformation("Seeded demo users for Alpha and Beta tenants");
    }
}
