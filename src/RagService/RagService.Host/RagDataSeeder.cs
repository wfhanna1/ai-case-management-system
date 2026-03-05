using RagService.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace RagService.Host;

/// <summary>
/// Seeds synthetic document embeddings into Qdrant on startup in Development.
/// Generates 200+ cases across 4 template types with realistic clustering.
/// Idempotent: checks if data already exists before seeding.
/// </summary>
public sealed class RagDataSeeder : IHostedService
{
    private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorStorePort _vectorStore;
    private readonly ILogger<RagDataSeeder> _logger;

    // Must match DemoTenants in ApiService
    private static readonly Guid AlphaTenantId = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001");
    private static readonly Guid BetaTenantId = Guid.Parse("b2c3d4e5-0000-0000-0000-000000000002");

    public RagDataSeeder(
        IEmbeddingPort embeddingPort,
        IVectorStorePort vectorStore,
        ILogger<RagDataSeeder> logger)
    {
        _embeddingPort = embeddingPort;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Check if already seeded by trying to search for a known document
        var checkResult = await _vectorStore.SearchAsync(AlphaTenantId, new float[384], 1, cancellationToken);
        if (checkResult.IsSuccess && checkResult.Value.Count > 0)
        {
            _logger.LogInformation("RAG data already seeded, skipping.");
            return;
        }

        _logger.LogInformation("Seeding synthetic RAG data...");

        var cases = GenerateSyntheticCases();
        var count = 0;

        foreach (var c in cases)
        {
            var textContent = BuildTextContent(c);
            var embeddingResult = await _embeddingPort.GenerateEmbeddingAsync(textContent, cancellationToken);
            if (embeddingResult.IsFailure)
            {
                _logger.LogWarning("Failed to generate embedding for {DocId}: {Error}",
                    c.DocumentId, embeddingResult.Error.Message);
                continue;
            }

            var result = await _vectorStore.UpsertAsync(
                c.DocumentId, c.TenantId, embeddingResult.Value, c.Metadata, cancellationToken);

            if (result.IsSuccess)
                count++;
        }

        _logger.LogInformation("Seeded {Count} document embeddings into Qdrant.", count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string BuildTextContent(SyntheticCase c)
    {
        var parts = new List<string> { c.TemplateType, c.SubjectName };
        foreach (var (key, value) in c.Metadata)
            parts.Add($"{key}: {value}");
        return string.Join(". ", parts);
    }

    private static List<SyntheticCase> GenerateSyntheticCases()
    {
        var cases = new List<SyntheticCase>();
        var rng = new Random(42); // deterministic

        // Child Welfare cases (~60 primary + follow-ups)
        var cwNames = new[]
        {
            "Emma Thompson", "Liam Johnson", "Olivia Garcia", "Noah Martinez",
            "Ava Williams", "Ethan Brown", "Sophia Jones", "Mason Davis",
            "Isabella Wilson", "Logan Miller", "Mia Taylor", "Lucas Anderson",
            "Charlotte Thomas", "Aiden Jackson", "Amelia White", "Elijah Harris",
            "Harper Martin", "James Robinson", "Evelyn Clark", "Benjamin Lewis",
            "Abigail Walker", "Alexander Hall", "Emily Allen", "Henry Young",
            "Elizabeth King", "Sebastian Wright", "Sofia Lopez", "Jack Hill",
            "Aria Scott", "Owen Green", "Riley Foster", "Hannah Barnes",
            "Nora Ross", "Caleb Price", "Zoey Bennett", "Carter Wood",
            "Penelope Brooks", "Jayden Kelly", "Lily Sanders", "Dylan Reed",
            "Chloe Hughes", "Grayson Watson", "Layla Cruz", "Leo Flores",
            "Scarlett Simmons", "Asher Ramirez", "Grace Peterson", "Mateo Powell",
            "Ella Long", "Lincoln Butler",
        };
        var cwReasons = new[]
        {
            "Physical abuse suspected", "Educational neglect reported", "Unsafe living conditions",
            "Parental substance abuse", "Abandonment concern", "Domestic violence exposure",
            "Inadequate supervision", "Medical neglect", "Emotional abuse",
            "Truancy and school avoidance", "Failure to thrive", "Sexual abuse allegation",
        };

        foreach (var (name, idx) in cwNames.Select((n, i) => (n, i)))
        {
            cases.Add(CreateCase(AlphaTenantId, "ChildWelfare", name,
                new Dictionary<string, string>
                {
                    ["ChildName"] = name,
                    ["Age"] = rng.Next(2, 17).ToString(),
                    ["ReasonForReferral"] = cwReasons[idx % cwReasons.Length],
                    ["SafetyConcern"] = rng.Next(2) == 0 ? "Yes" : "No",
                    ["PreviousCPS"] = rng.Next(3) == 0 ? "Yes" : "No",
                }, idx));

            // Add a second document for some cases
            if (idx % 3 == 0)
            {
                cases.Add(CreateCase(AlphaTenantId, "ChildWelfare", name,
                    new Dictionary<string, string>
                    {
                        ["ChildName"] = name,
                        ["ReportType"] = "Follow-up Assessment",
                        ["Author"] = $"Dr. {cwNames[(idx + 5) % cwNames.Length].Split(' ')[1]}",
                    }, idx + 1000));
            }
        }

        // Adult Protective Services (~40 primary + follow-ups)
        var apNames = new[]
        {
            "Robert Chen", "Margaret Williams", "William Anderson", "Dorothy Martinez",
            "Richard Taylor", "Betty Thomas", "Joseph Jackson", "Helen White",
            "Thomas Harris", "Ruth Martin", "Charles Robinson", "Frances Clark",
            "Daniel Lewis", "Evelyn Walker", "Matthew Hall", "Jean Allen",
            "Anthony Young", "Martha King", "Mark Wright", "Doris Lopez",
            "Donald Hill", "Marie Scott", "Paul Green", "Gloria Adams",
            "Steven Baker", "Virginia Nelson", "Kenneth Carter", "Rose Mitchell",
            "George Perez", "Shirley Roberts", "Edward Turner", "Lillian Phillips",
            "Frank Campbell", "Teresa Parker", "Jerry Evans", "Joan Edwards",
            "Dennis Collins", "Alice Stewart", "Walter Sanchez", "Judith Morris",
        };
        var apTypes = new[]
        {
            "Physical abuse", "Financial exploitation", "Self-neglect",
            "Emotional abuse", "Caregiver neglect", "Sexual abuse",
        };

        foreach (var (name, idx) in apNames.Select((n, i) => (n, i)))
        {
            var tenantId = idx % 5 == 0 ? BetaTenantId : AlphaTenantId;
            cases.Add(CreateCase(tenantId, "AdultProtective", name,
                new Dictionary<string, string>
                {
                    ["ClientName"] = name,
                    ["AbuseType"] = apTypes[idx % apTypes.Length],
                    ["LivingSituation"] = idx % 3 == 0 ? "Alone" : idx % 3 == 1 ? "With family" : "Assisted living",
                    ["CanCommunicate"] = rng.Next(4) != 0 ? "Yes" : "No",
                }, idx + 2000));

            if (idx % 4 == 0)
            {
                cases.Add(CreateCase(tenantId, "AdultProtective", name,
                    new Dictionary<string, string>
                    {
                        ["ClientName"] = name,
                        ["ReportType"] = "Investigation Report",
                        ["Investigator"] = $"Agent {rng.Next(100, 999)}",
                    }, idx + 3000));
            }
        }

        // Housing Assistance (~40 primary)
        var haNames = new[]
        {
            "Sarah Mitchell", "Kevin Perez", "Laura Roberts", "Brandon Turner",
            "Jessica Phillips", "Tyler Campbell", "Amanda Parker", "Ryan Evans",
            "Melissa Edwards", "Justin Collins", "Nicole Stewart", "Aaron Sanchez",
            "Stephanie Morris", "Nathan Rogers", "Rebecca Reed", "Joshua Cook",
            "Lauren Morgan", "Zachary Bell", "Ashley Murphy", "Dylan Bailey",
            "Samantha Rivera", "Caleb Cooper", "Victoria Richardson", "Sean Cox",
            "Brittany Howard", "Travis Ward", "Heather Torres", "Kyle Peterson",
            "Megan Gray", "Dustin Ramirez", "Christina James", "Derrick Watson",
            "Courtney Brooks", "Shane Kelly", "Amber Price", "Cody Bennett",
            "Tiffany Wood", "Brett Barnes", "Jamie Ross", "Corey Foster",
        };
        var haAssist = new[]
        {
            "Rental assistance", "Emergency shelter", "Transitional housing",
            "Mortgage assistance", "Utility assistance",
        };

        foreach (var (name, idx) in haNames.Select((n, i) => (n, i)))
        {
            var tenantId = idx % 4 == 0 ? BetaTenantId : AlphaTenantId;
            cases.Add(CreateCase(tenantId, "HousingAssistance", name,
                new Dictionary<string, string>
                {
                    ["ApplicantName"] = name,
                    ["HouseholdSize"] = rng.Next(1, 8).ToString(),
                    ["MonthlyIncome"] = (rng.Next(800, 4000)).ToString(),
                    ["HousingStatus"] = idx % 3 == 0 ? "Homeless" : idx % 3 == 1 ? "Renting" : "Living with others",
                    ["AssistanceType"] = haAssist[idx % haAssist.Length],
                    ["Employed"] = rng.Next(2) == 0 ? "Yes" : "No",
                }, idx + 4000));
        }

        // Mental Health Referral (~40 primary + follow-ups)
        var mhNames = new[]
        {
            "David Kim", "Rachel Patel", "Michael Nguyen", "Kimberly Shah",
            "Christopher Lee", "Jennifer Yamamoto", "Andrew Singh", "Elizabeth Tanaka",
            "Daniel Park", "Maria Fernandez", "Brian Wu", "Christine Gupta",
            "Gregory Chang", "Patricia Mehta", "Jeffrey Huang", "Catherine Sharma",
            "Eric Suzuki", "Michelle Pham", "Ronald Nakamura", "Deborah Das",
            "Timothy Takahashi", "Sandra Rao", "Kevin Watanabe", "Lisa Banerjee",
            "Scott Hayashi", "Amy Matsuda", "Carlos Reyes", "Priya Agarwal",
            "Jordan Sato", "Anita Kapoor", "Marcus Tran", "Fatima Ahmad",
            "Derek Yoshida", "Nina Joshi", "Russell Chung", "Maya Desai",
            "Adrian Morales", "Sakura Okada", "Tony Valdez", "Meena Reddy",
        };
        var mhSymptoms = new[]
        {
            "Severe anxiety and panic attacks", "Major depressive episode",
            "Bipolar disorder management", "PTSD from trauma",
            "Schizophrenia stabilization", "OCD symptom management",
            "Eating disorder assessment", "Substance use disorder evaluation",
            "Grief counseling referral", "Autism spectrum evaluation",
        };
        var mhUrgency = new[] { "Routine", "Urgent", "Emergency" };

        foreach (var (name, idx) in mhNames.Select((n, i) => (n, i)))
        {
            var tenantId = idx % 3 == 0 ? BetaTenantId : AlphaTenantId;
            cases.Add(CreateCase(tenantId, "MentalHealthReferral", name,
                new Dictionary<string, string>
                {
                    ["PatientName"] = name,
                    ["Symptoms"] = mhSymptoms[idx % mhSymptoms.Length],
                    ["Urgency"] = mhUrgency[idx % mhUrgency.Length],
                    ["PreviousTreatment"] = rng.Next(2) == 0 ? "Yes" : "No",
                    ["SuicidalIdeation"] = rng.Next(10) == 0 ? "Yes" : "No",
                }, idx + 5000));

            // Add follow-up documents for some
            if (idx % 5 == 0)
            {
                cases.Add(CreateCase(tenantId, "MentalHealthReferral", name,
                    new Dictionary<string, string>
                    {
                        ["PatientName"] = name,
                        ["ReportType"] = "Psychiatric Evaluation",
                        ["Provider"] = $"Dr. {mhNames[(idx + 3) % mhNames.Length].Split(' ')[1]}",
                    }, idx + 6000));
            }
        }

        return cases;
    }

    private static SyntheticCase CreateCase(
        Guid tenantId, string templateType, string subjectName,
        Dictionary<string, string> metadata, int seedIndex)
    {
        // Deterministic document ID from seed index
        var bytes = new byte[16];
        BitConverter.GetBytes(seedIndex).CopyTo(bytes, 0);
        BitConverter.GetBytes(0xDEAD).CopyTo(bytes, 4);
        var documentId = new Guid(bytes);

        return new SyntheticCase(documentId, tenantId, templateType, subjectName, metadata);
    }

    private sealed record SyntheticCase(
        Guid DocumentId,
        Guid TenantId,
        string TemplateType,
        string SubjectName,
        Dictionary<string, string> Metadata);
}
