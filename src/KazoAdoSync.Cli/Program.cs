using System.Text.RegularExpressions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Octokit;
using ReverseMarkdown;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// ---------------------------------------------------------------------------
// KazoAdoSync.Cli – Program.cs
// Synchronizes Azure DevOps Tasks to GitHub Issues with strict AB# traceability.
// ---------------------------------------------------------------------------

var configPath = args.Length > 0 ? args[0] : "config.yml";
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Configuration file not found: {configPath}");
    return 1;
}

// ── 1. Load configuration ────────────────────────────────────────────────
var yamlContent = await File.ReadAllTextAsync(configPath);
var deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();
var config = deserializer.Deserialize<SyncConfig>(yamlContent);

var adoPat    = Environment.GetEnvironmentVariable(config.EnvMapping.DevopsTokenVar)
                ?? throw new InvalidOperationException($"Missing env var {config.EnvMapping.DevopsTokenVar}");
var ghToken   = Environment.GetEnvironmentVariable(config.EnvMapping.GithubTokenVar)
                ?? throw new InvalidOperationException($"Missing env var {config.EnvMapping.GithubTokenVar}");
var adoOrgUrl = Environment.GetEnvironmentVariable("ADO_ORG_URL")
                ?? throw new InvalidOperationException("Missing env var ADO_ORG_URL");
var adoProject = Environment.GetEnvironmentVariable("ADO_PROJECT")
                 ?? throw new InvalidOperationException("Missing env var ADO_PROJECT");
var ghOwner   = Environment.GetEnvironmentVariable("GH_REPO_OWNER")
                ?? throw new InvalidOperationException("Missing env var GH_REPO_OWNER");
var ghRepo    = Environment.GetEnvironmentVariable("GH_REPO_NAME")
                ?? throw new InvalidOperationException("Missing env var GH_REPO_NAME");

// ── 2. Load issue template ───────────────────────────────────────────────
var templatePath = config.SyncSettings.TemplatePath ?? "template.md";
if (!File.Exists(templatePath))
{
    Console.Error.WriteLine($"Template file not found: {templatePath}");
    return 1;
}
var templateBody = await File.ReadAllTextAsync(templatePath);

// ── 3. Connect to Azure DevOps ───────────────────────────────────────────
var adoCredentials = new VssBasicCredential(string.Empty, adoPat);
var adoConnection  = new VssConnection(new Uri(adoOrgUrl), adoCredentials);
var witClient      = adoConnection.GetClient<WorkItemTrackingHttpClient>();

// ── 4. Connect to GitHub ─────────────────────────────────────────────────
var ghClient = new GitHubClient(new ProductHeaderValue("KazoAdoSync"))
{
    Credentials = new Credentials(ghToken)
};

// ── 5. Query ADO for tasks with the watch tag but not the processed tag ──
var watchTag     = config.SyncSettings.WatchTag;
var processedTag = config.SyncSettings.ProcessedTag;
var sourceField  = config.SyncSettings.DefaultSourceField ?? "Notes";

var wiql = new Wiql
{
    Query = $@"
        SELECT [System.Id]
        FROM WorkItems
        WHERE [System.WorkItemType] = 'Task'
          AND [System.Tags] CONTAINS '{watchTag}'
          AND [System.Tags] NOT CONTAINS '{processedTag}'
          AND [System.State] <> 'Closed'
        ORDER BY [System.CreatedDate] DESC"
};

var queryResult = await witClient.QueryByWiqlAsync(wiql, adoProject);
Console.WriteLine($"Found {queryResult.WorkItems.Count()} work item(s) to process.");

// ── 6. Process each work item ────────────────────────────────────────────
var htmlConverter = new ReverseMarkdown.Converter();

foreach (var wiRef in queryResult.WorkItems)
{
    var workItem = await witClient.GetWorkItemAsync(
        adoProject, wiRef.Id,
        expand: WorkItemExpand.Relations);

    if (workItem == null) continue;

    var wiId   = workItem.Id!.Value;
    var wiUrl  = $"{adoOrgUrl}/{adoProject}/_workitems/edit/{wiId}";

    // ── 6a. Extract description (HTML → Markdown) ────────────────────
    var descriptionHtml = GetFieldString(workItem, $"Microsoft.VSTS.TCM.{sourceField}")
                       ?? GetFieldString(workItem, "System.Description")
                       ?? string.Empty;
    var wiDescription = htmlConverter.Convert(descriptionHtml);

    // ── 6b. Fetch parent description if available ────────────────────
    var parentDescription = string.Empty;
    var parentRelation = workItem.Relations?
        .FirstOrDefault(r => r.Rel == "System.LinkTypes.Hierarchy-Reverse");

    if (parentRelation != null)
    {
        var parentIdMatch = Regex.Match(parentRelation.Url, @"/(\d+)$");
        if (parentIdMatch.Success && int.TryParse(parentIdMatch.Groups[1].Value, out var parentId))
        {
            var parentWi = await witClient.GetWorkItemAsync(adoProject, parentId);
            var parentHtml = GetFieldString(parentWi, "System.Description") ?? string.Empty;
            parentDescription = htmlConverter.Convert(parentHtml);
        }
    }

    // ── 6c. Build the issue title (strict AB# prefix) ────────────────
    var issueTitle = $"AB#{wiId} – {config.SyncSettings.IssuePrefix}";
    var wiTitleField = GetFieldString(workItem, "System.Title");
    if (!string.IsNullOrWhiteSpace(wiTitleField))
    {
        issueTitle = $"AB#{wiId} – {wiTitleField}";
    }

    // ── 6d. Replace template placeholders ────────────────────────────
    var issueBody = ReplacePlaceholders(templateBody, wiId, wiUrl, wiDescription, parentDescription);

    // ── 6e. Check for duplicates on GitHub ───────────────────────────
    var existingIssues = await ghClient.Search.SearchIssues(
        new SearchIssuesRequest($"AB#{wiId} in:title repo:{ghOwner}/{ghRepo}")
        {
            Type = IssueTypeQualifier.Issue
        });

    if (existingIssues.TotalCount > 0)
    {
        Console.WriteLine($"  ⏭  Skipping WI {wiId} – GitHub issue already exists.");
        continue;
    }

    // ── 6f. Create the GitHub issue ──────────────────────────────────
    var newIssue = new NewIssue(issueTitle) { Body = issueBody };
    if (!string.IsNullOrWhiteSpace(config.SyncSettings.GithubLabel))
    {
        newIssue.Labels.Add(config.SyncSettings.GithubLabel);
    }

    var createdIssue = await ghClient.Issue.Create(ghOwner, ghRepo, newIssue);
    Console.WriteLine($"  ✅ Created GitHub issue #{createdIssue.Number} for WI {wiId}");

    // ── 6g. Tag work item as processed in ADO ────────────────────────
    var currentTags = GetFieldString(workItem, "System.Tags") ?? string.Empty;
    var updatedTags = string.IsNullOrWhiteSpace(currentTags)
        ? processedTag
        : $"{currentTags}; {processedTag}";

    var patchDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
    {
        new()
        {
            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Replace,
            Path      = "/fields/System.Tags",
            Value     = updatedTags
        }
    };
    await witClient.UpdateWorkItemAsync(patchDocument, wiId);

    // ── 6h. Add a comment to the ADO work item ──────────────────────
    var commentBody = $"GitHub Issue created: {createdIssue.HtmlUrl}";
    await witClient.AddCommentAsync(
        new CommentCreate { Text = commentBody },
        adoProject, wiId);

    Console.WriteLine($"  🔗 Tagged WI {wiId} with '{processedTag}' and added comment.");
}

Console.WriteLine("Sync complete.");
return 0;

// ═══════════════════════════════════════════════════════════════════════════
// Helper methods
// ═══════════════════════════════════════════════════════════════════════════

static string ReplacePlaceholders(
    string template, int wiId, string wiUrl,
    string wiDescription, string parentDescription)
{
    return template
        .Replace("{{WI_ID}}", wiId.ToString())
        .Replace("{{WI_URL}}", wiUrl)
        .Replace("{{WI_DESC}}", wiDescription)
        .Replace("{{PARENT_DESC}}", parentDescription);
}

static string? GetFieldString(WorkItem? wi, string fieldName)
{
    if (wi?.Fields == null) return null;
    return wi.Fields.TryGetValue(fieldName, out var value)
        ? value?.ToString()
        : null;
}

// ═══════════════════════════════════════════════════════════════════════════
// Configuration model
// ═══════════════════════════════════════════════════════════════════════════

public sealed class SyncConfig
{
    public SyncSettings SyncSettings { get; set; } = new();
    public EnvMapping EnvMapping { get; set; } = new();
}

public sealed class SyncSettings
{
    public string WatchTag { get; set; } = "AI-Ready";
    public string ProcessedTag { get; set; } = "IssueCreated";
    public string IssuePrefix { get; set; } = "Devops IA Task";
    public string? DefaultSourceField { get; set; } = "Notes";
    public string? GithubLabel { get; set; } = "devops-task";
    public string? TemplatePath { get; set; } = "template.md";
}

public sealed class EnvMapping
{
    public string GithubTokenVar { get; set; } = "GH_PAT_TOKEN";
    public string DevopsTokenVar { get; set; } = "ADO_PAT_TOKEN";
}
