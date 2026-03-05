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

        await SeedUsersAsync(db, hasher);
        await SeedFormTemplatesAsync(db);
        await SeedDocumentsAndCasesAsync(db);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedDocumentsAndCasesAsync(IntakeDbContext db)
    {
        if (await db.Cases.IgnoreQueryFilters().AnyAsync())
            return;

        var alpha = new TenantId(DemoTenants.AlphaTenantId);
        var beta = new TenantId(DemoTenants.BetaTenantId);

        // Remove any previously seeded bare documents so we start fresh.
        var existingDocs = await db.Documents.IgnoreQueryFilters().ToListAsync();
        if (existingDocs.Count > 0)
        {
            db.Documents.RemoveRange(existingDocs);
            await db.SaveChangesAsync();
        }

        // Alpha Clinic cases -- 5 subjects with multiple documents at various stages.
        var alphaCases = new (string name, (string file, string[] fields, DocumentStatus status)[] docs)[]
        {
            ("Alice Johnson", [
                ("intake-alice-johnson.pdf",   ["ClientName:Alice Johnson", "DateOfBirth:1985-03-14", "CaseNumber:CW-2024-0451", "Address:123 Oak St"],         DocumentStatus.Finalized),
                ("report-alice-johnson.pdf",   ["PatientName:Alice Johnson", "ReportTitle:Follow-up Assessment", "Author:Dr. Lee", "Date:2024-11-20"],           DocumentStatus.PendingReview),
                ("form-alice-johnson.pdf",     ["SubjectName:Alice Johnson", "FormType:Housing", "SubmittedBy:Case Worker", "SubmissionDate:2024-12-01"],        DocumentStatus.PendingReview),
            ]),
            ("Bob Martinez", [
                ("intake-bob-martinez.pdf",    ["ClientName:Bob Martinez", "DateOfBirth:1972-08-22", "CaseNumber:AP-2024-0312", "Address:456 Pine Ave"],         DocumentStatus.InReview),
                ("report-bob-martinez.pdf",    ["PatientName:Bob Martinez", "ReportTitle:Initial Screening", "Author:Dr. Patel", "Date:2024-10-05"],             DocumentStatus.PendingReview),
            ]),
            ("Carol Chen", [
                ("intake-carol-chen.pdf",      ["ClientName:Carol Chen", "DateOfBirth:1990-01-30", "CaseNumber:MH-2024-0189", "Address:789 Maple Dr"],           DocumentStatus.Finalized),
                ("report-carol-chen.pdf",      ["PatientName:Carol Chen", "ReportTitle:Psychiatric Evaluation", "Author:Dr. Kim", "Date:2024-09-15"],            DocumentStatus.Finalized),
                ("form-carol-chen.pdf",        ["SubjectName:Carol Chen", "FormType:MentalHealth", "SubmittedBy:Intake Desk", "SubmissionDate:2024-09-10"],      DocumentStatus.InReview),
            ]),
            ("David Nguyen", [
                ("intake-david-nguyen.pdf",    ["ClientName:David Nguyen", "DateOfBirth:1965-11-03", "CaseNumber:HA-2024-0078", "Address:321 Elm Blvd"],         DocumentStatus.PendingReview),
                ("form-david-nguyen.pdf",      ["SubjectName:David Nguyen", "FormType:HousingAssistance", "SubmittedBy:Front Desk", "SubmissionDate:2024-11-28"],DocumentStatus.PendingReview),
            ]),
            ("Eva Williams", [
                ("intake-eva-williams.pdf",    ["ClientName:Eva Williams", "DateOfBirth:1998-06-17", "CaseNumber:CW-2024-0523", "Address:654 Birch Ln"],         DocumentStatus.PendingReview),
            ]),
        };

        foreach (var (subjectName, docs) in alphaCases)
        {
            var @case = Case.Create(alpha, subjectName);
            foreach (var (file, fields, targetStatus) in docs)
            {
                var doc = CreateProcessedDocument(alpha, file, fields, targetStatus);
                doc.AssignToCase(@case.Id);
                @case.LinkDocument(doc);
                db.Documents.Add(doc);
            }
            db.Cases.Add(@case);
        }

        // Beta Hospital -- 1 case
        var betaCase = Case.Create(beta, "Frank Davis");
        var betaDoc = CreateProcessedDocument(beta,
            "intake-frank-davis.pdf",
            ["ClientName:Frank Davis", "DateOfBirth:1958-04-09", "CaseNumber:AP-2024-0401", "Address:100 River Rd"],
            DocumentStatus.PendingReview);
        betaDoc.AssignToCase(betaCase.Id);
        betaCase.LinkDocument(betaDoc);
        db.Documents.Add(betaDoc);
        db.Cases.Add(betaCase);

        // Also add a couple of unassigned documents (no name field extracted, no case).
        db.Documents.Add(CreateProcessedDocument(alpha, "unknown-scan-001.pdf",
            ["DocumentTitle:Untitled Scan", "Date:2024-12-01", "Content:Illegible handwriting"],
            DocumentStatus.PendingReview));
        db.Documents.Add(CreateProcessedDocument(alpha, "damaged-form.pdf",
            ["DocumentTitle:Damaged Document", "Date:2024-11-15", "Content:Partial text only"],
            DocumentStatus.PendingReview));

        await db.SaveChangesAsync();
        _logger.LogInformation(
            "Seeded {CaseCount} cases with {DocCount} documents for demo tenants",
            alphaCases.Length + 1,
            alphaCases.Sum(c => c.docs.Length) + 3);
    }

    private static IntakeDocument CreateProcessedDocument(
        TenantId tenantId, string fileName, string[] fieldPairs, DocumentStatus targetStatus)
    {
        var doc = IntakeDocument.Submit(tenantId, fileName, $"demo/{fileName}");

        var fields = fieldPairs.Select(pair =>
        {
            var parts = pair.Split(':', 2);
            return new ExtractedField(parts[0], parts[1], 0.7 + Random.Shared.NextDouble() * 0.25);
        }).ToList();

        doc.MarkProcessing();
        doc.MarkCompleted(fields);
        doc.MarkPendingReview();

        if (targetStatus is DocumentStatus.InReview or DocumentStatus.Finalized)
            doc.StartReview(new UserId(Guid.NewGuid()));

        if (targetStatus is DocumentStatus.Finalized)
            doc.Finalize(new UserId(Guid.NewGuid()));

        return doc;
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
