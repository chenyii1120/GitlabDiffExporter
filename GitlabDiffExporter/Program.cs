using Newtonsoft.Json;
using System.Text.RegularExpressions;

class Program
{
    public static Settings setting;
    public static string ProjectName;
    private static void GenerateDiffReport(string responseContent)
    {
        var compareData = JsonConvert.DeserializeObject<CompareData>(responseContent);

        if (compareData == null)
        {
            Console.WriteLine("Deserialize failed, compareData is null.");
            return;
        }

        if (compareData.Diffs == null)
        {
            Console.WriteLine("There's no diff, compareData.Diffs is null.");
            return;
        }

        var diffs = compareData.Diffs;

        if (diffs.Count == 0)
        {
            Console.WriteLine("No diff found, maybe the commit hash does not exist or there is no change in the range.");
        }
        else
        {
            string htmlContent = """
             <html>
                 <head>
                     <title>Commit Diff</title>
                     <style>
                         body { font-family: Arial, sans-serif; }
                         .diff { margin-bottom: 20px; border: 1px solid #ccc; }
                         .file-header { padding: 10px; background-color: #f7f7f7; border-bottom: 1px solid #ccc; font-weight: bold; }
                         .diff-content { display: table; width: 100%; }
                         .line-num { width: 50px; text-align: right; background: #f0f0f0; }
                         .line { padding: 5px; white-space: pre-wrap; word-break: break-word; font-family: Consolas, 'Courier New', monospace; }
                         .line.added { background: #e6ffed; color: #22863a; }
                         .line.removed { background: #ffeef0; color: #b31d28; }
                         .line.context { background: #f8f8f8; }
                         .line.highlight { background: #ffddc1; }
                         .diff-side { display: table-cell; padding: 5px; width: 45%; }
                         .diff-table { width: 100%; border-collapse: collapse; table-layout: fixed; }
                         .diff-table td, .diff-table th { padding: 5px; border: 1px solid #ccc; }
                         .back-to-top { position: fixed; bottom: 20px; right: 20px; width: 20px; height: 20px; text-align: center; line-height: 20px; background-color: #f7f7f7; border: 1px solid #ccc; border-radius: 50%; }
                         .commits { border-collapse: collapse; width: 100%; margin: 10px 0; }
                         .commits th, .commits td { border: 1px solid #dddddd; padding: 8px; text-align: left; }
                         .commits th { background-color: #f2f2f2; }
                         .commits tr:nth-child(even) { background-color: #f9f9f9; }
                         .commits td { margin: 5px; }
                         .commits th { font-weight: bold; }
                         .commits td, .commits th { text-align: center; }
                     </style>
                 </head>
             <body>
                 <div class='back-to-top'><a href='#top'>^</a></div>
                 <h1 id='top'>Diff between commits</h1>
             """;

            if (setting.WithCommitJourney)
            {
                string journey = "<div><h3>Commits</h3><table class='commits'>";
                journey += "<tr><th>Commit ID</th><th>Author</th><th>Title</th><th>Created At</th></tr>";

                foreach (var commit in compareData.Commits)
                {
                    string commited_at = DateTime.Parse(commit.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss");
                    journey += $"<tr><td>{commit.ShortId}</td><td>{commit.Author}</td><td style='text-align: left;'>{commit.Title}</td><td>{commited_at}</td></tr>";
                }
                journey += $"</table><hr /></div>";
                htmlContent += journey;
            }

            if (setting.WithMenu)
            {
                string menu = "<div><h3>Modified files</h3><ul>";
                foreach (var diff in diffs)
                {
                    string filePath = diff.NewPath;
                    menu += $"<li><a href='#{filePath}'>{filePath}</a></li>";
                }
                menu += $"</ul></div>";
                htmlContent += menu;
            }

            DateTime now = DateTime.Now;
            string now_str = now.ToString("yyyy-MM-dd HH:mm:ss");
            string now_str_underline = now.ToString("yyyyMMdd_HHmmss");
            htmlContent += $"<p>Total modified files: {diffs.Count}</p>";
            htmlContent += $"<p>Report generated at: {now_str}</p>";
            foreach (var diff in diffs)
            {
                string filePath = diff.NewPath;
                htmlContent += $"<div class='diff' id='{filePath}'><div class='file-header'>{filePath}</div><div class='diff-content'>";
                htmlContent += "<table class='diff-table'>";

                string[] lines = diff.DiffContent.Split('\n');
                int leftLineNum = 1, rightLineNum = 1;
                int oldLineNumber = 1;
                int newLineNumber = 1;

                foreach (string line in lines)
                {
                    if (line.StartsWith("@@"))
                    {
                        var matches = Regex.Matches(line, @"-(\d+),\d+ \+(\d+),\d+");
                        if (matches.Count > 0)
                        {
                            oldLineNumber = int.Parse(matches[0].Groups[1].Value);
                            newLineNumber = int.Parse(matches[0].Groups[2].Value);
                        }

                        htmlContent +=
                            $"<tr><td colspan='4' class='line context'>{System.Web.HttpUtility.HtmlEncode(line)}</td></tr>";
                    }
                    else if (line.StartsWith("+"))
                    {
                        htmlContent += $"<tr><td class='line-num'></td><td class='line context'></td>";
                        htmlContent +=
                            $"<td class='line-num'>{newLineNumber}</td><td class='line added'>{System.Web.HttpUtility.HtmlEncode(line)}</td></tr>";
                        newLineNumber++;
                    }
                    else if (line.StartsWith("-"))
                    {
                        htmlContent +=
                            $"<tr><td class='line-num'>{oldLineNumber}</td><td class='line removed'>{System.Web.HttpUtility.HtmlEncode(line)}</td>";
                        htmlContent += $"<td class='line-num'></td><td class='line context'></td></tr>";
                        oldLineNumber++;
                    }
                    else
                    {
                        htmlContent +=
                            $"<tr><td class='line-num'>{oldLineNumber}</td><td class='line context'>{System.Web.HttpUtility.HtmlEncode(line)}</td>";
                        htmlContent +=
                            $"<td class='line-num'>{newLineNumber}</td><td class='line context'>{System.Web.HttpUtility.HtmlEncode(line)}</td></tr>";
                        oldLineNumber++;
                        newLineNumber++;
                    }
                }

                htmlContent += "</table></div></div>";
            }
            htmlContent += "</body></html>";

            string fileName = $"{now_str_underline}_{ProjectName}_commit_diff.html";
            File.WriteAllText(fileName, htmlContent);
            Console.WriteLine($"Report generated successfully -> {fileName}");
        }
    }
    
    static async Task Main(string[] args)
    {
        setting = Settings.Load();
        GitlabClient client = new GitlabClient(setting.GitlabAccessToken);

        // 取得專案列表
        Dictionary<int, string> projDict = await client.GetProjects();
        
        // 使用者選擇專案
        Console.WriteLine("Project ID: Project Name");
        foreach (var proj in projDict)
        {
            Console.WriteLine($"{proj.Key}: {proj.Value}");
        }

        Console.WriteLine("==========================================");
        Console.WriteLine("Please insert project ID:");
        int projId = int.Parse(Console.ReadLine());
        ProjectName = projDict[projId];
        
        // 輸入較新的 commit hash
        Console.WriteLine("Please insert the newest commit hash you want to compare:");
        string newCommitHash = Console.ReadLine();

        // 輸入較舊的 commit hash
        Console.WriteLine("Please insert the oldest commit hash you want to compare:");
        string oldCommitHash = Console.ReadLine();

        string responseContent = await client.GetDiffResponse(projId, oldCommitHash, newCommitHash);
        GenerateDiffReport(responseContent);
        Pause();
    }

    public static void Pause()
    {
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}

public class GitlabClient : HttpClient
{
    private static string gitlabUrl = Program.setting.GitlabUrl;
    private static int apiVersion = Program.setting.GitlabApiVersion;
    private static string projectUrl = $"{gitlabUrl}/api/v{apiVersion}/projects";

    public GitlabClient(string accessToken) : base()
    {
        this.DefaultRequestHeaders.Add("PRIVATE-TOKEN", accessToken);
        if (!Program.setting.Debug) return;
        Console.WriteLine(accessToken);
        Console.WriteLine(gitlabUrl);
    }

    public async Task<string> GetResponse(string url)
    {
        try
        {
            HttpResponseMessage response = await GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                if (Program.setting.Debug) Console.WriteLine($"Response content：{responseContent}");
                return responseContent;
            }

            Console.WriteLine($"Cannot get content：{(int)response.StatusCode} - {response.ReasonPhrase}");
            string errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response error content：{errorContent}");
            throw new Exception($"Cannot get content：{(int)response.StatusCode} - {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occured：{ex.Message}");
            Console.WriteLine($"Error message：{ex.StackTrace}");
        }

        return "";
    }

    public async Task<Dictionary<int, string>> GetProjects()
    {
        string responseContent = await GetResponse(projectUrl);
        var ProjData = JsonConvert.DeserializeObject<List<Project>>(responseContent);
        Dictionary<int, string> projDict = new Dictionary<int, string>();

        if (ProjData == null)
        {
            Console.WriteLine("Deserialize failed, the response content is null。");
            return projDict;
        }

        if (ProjData.Count == 0)
        {
            Console.WriteLine("Sorry, we can't find any project");
        }
        else
        {
            foreach (var proj in ProjData)
            {
                projDict.Add(proj.ProjId, proj.ProjName);
            }
        }

        return projDict;
    }

    public async Task<string> GetDiffResponse(int projectId, string oldCommitHash, string newCommitHash)
    {
        string compareUrl = $"{projectUrl}/{projectId}/repository/compare?from={oldCommitHash}&to={newCommitHash}";
        if (Program.setting.Debug) Console.WriteLine($"API URL: {compareUrl}");
        return await GetResponse(compareUrl);
    }
}


// 定義 JSON 解析用的類
public class CompareData
{
    public List<Diff> Diffs { get; set; }
    public List<Commit> Commits { get; set; }
}

public class Commit
{
    [JsonProperty("short_id")]
    public string ShortId { get; set; }
    
    [JsonProperty("author_name")]
    public string Author { get; set; }
    
    [JsonProperty("title")]
    public string Title { get; set; }
    [JsonProperty("created_at")]
    public string CreatedAt { get; set; }
}

public class Diff
{
    [JsonProperty("new_path")] public string NewPath { get; set; }

    [JsonProperty("diff")] public string DiffContent { get; set; }
}

public class Project
{
    [JsonProperty("id")] public int ProjId { get; set; }

    [JsonProperty("name")] public string ProjName { get; set; }
}

public class Settings
{
    [JsonProperty("gitlab_url")] public string GitlabUrl { get; set; }
    [JsonProperty("gitlab_access_token")] public string GitlabAccessToken { get; set; }
    [JsonProperty("gitlab_api_version")] public int GitlabApiVersion { get; set; }
    [JsonProperty("with_commit_journey")] public bool WithCommitJourney { get; set; }
    [JsonProperty("with_menu")] public bool WithMenu { get; set; }
    [JsonProperty("debug")] public bool Debug { get; set; }
    
    public static Settings Load()
    {
        string filePath = @"settings.json";
        try
        {
            string jsonContent = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Settings>(jsonContent);
        }
        catch (FileNotFoundException)
        {
            Settings defaultSettings = new Settings
            {
                GitlabUrl = "https://gitlab.com",
                GitlabAccessToken = "your_access_token",
                GitlabApiVersion = 3,
                WithCommitJourney = true,
                WithMenu = true,
                Debug = false
            };
            string jsonContent = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
            File.WriteAllText(filePath, jsonContent);
            Console.WriteLine("You need to setup the settings.json first, you can find it in the folder.");
            Program.Pause();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            // 捕獲其他異常
            Console.WriteLine($"讀取 settings.json 文件時發生錯誤：{ex.Message}");
            Program.Pause();
            Environment.Exit(1);
        }
        return null;
    }
}