using System.Collections.Specialized;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace StepScraper;

public class Program
{
    public enum FileData
    {
        DEAD = 0,
        DESCRIPTION = 1,
        STEP = 2,
        INV = 3,
        STEP_INV = 4
    }

    public class VideoAndLinks
    {
        public string Title;
        public string Description;
        public string StepLink;
        public string InvLink;
        public FileData FileData;
    }

    public static async Task Main(string[] args)
    {
        string pdfFolderPath = args[0];

        List<string> youtubeLinks = DocReader.ReadFolderAndExtractYoutubeLinks(pdfFolderPath);
        List<string> videoIds = youtubeLinks.Select(ExtractVideoIdFromUrl).ToList();

        string jsonPath = Path.Combine(pdfFolderPath, "videoTitleAndDescriptions.json");

        Dictionary<string, VideoAndLinks> videoTitleAndDescriptions = new();
        if (!File.Exists(jsonPath))
        {
            string json = JsonConvert.SerializeObject(videoTitleAndDescriptions, Formatting.Indented);
            await File.WriteAllTextAsync(jsonPath, json);
        }

        string jsonFileText = await File.ReadAllTextAsync(jsonPath);
        videoTitleAndDescriptions =
            JsonConvert.DeserializeObject<Dictionary<string, VideoAndLinks>>(jsonFileText) ?? [];

        ScrapeYoutube(videoIds, videoTitleAndDescriptions, args[1]);

        foreach (VideoAndLinks videoAndLinks in videoTitleAndDescriptions.Values)
        {
            string[] lines = videoAndLinks.Description.Split('\n');
            foreach (string line in lines)
            {
                if (line.EndsWith("STEP.zip"))
                {
                    videoAndLinks.StepLink = line;
                }
                else if (line.EndsWith("Inv.zip"))
                {
                    videoAndLinks.InvLink = line;
                }
            }
        }

        await DownloadStepFiles(videoTitleAndDescriptions.Values.ToList(), pdfFolderPath);
        
        string jsonFinal = JsonConvert.SerializeObject(videoTitleAndDescriptions, Formatting.Indented);

        await File.WriteAllTextAsync(jsonPath, jsonFinal);
        Console.WriteLine("json written to " + jsonPath);
    }

    static string ExtractVideoIdFromUrl(string url)
    {
        Uri uri = new(url);
        NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query["v"] ?? url[(url.LastIndexOf('/') + 1)..];
    }

    static void ScrapeYoutube(
        List<string> videoIds,
        Dictionary<string, VideoAndLinks> videoTitleAndDescriptions,
        string apiKey)
    {
        const int max = 10;
        int count = 0;

        foreach (string videoId in videoIds)
        {
            if (videoTitleAndDescriptions.ContainsKey(videoId)) continue;

            if (count >= max)
            {
                break;
            }

            // open the youtube link and scrape the video description
            YouTubeService youtubeService = new(new BaseClientService.Initializer()
            {
                ApiKey = apiKey
            });

            Console.WriteLine($"Scrapping video title and description for id {videoId}...");
            VideosResource.ListRequest? request = youtubeService.Videos.List("snippet");
            request.Id = videoId;

            VideoListResponse? response = request.Execute();
            if (response.Items.Count == 0)
            {
                Console.WriteLine("Video not found!");
            }
            else
            {
                Video? video = response.Items[0];
                videoTitleAndDescriptions.Add(
                    videoId,
                    new VideoAndLinks
                    {
                        Title = video.Snippet.Title,
                        Description = video.Snippet.Description,
                        FileData = FileData.DESCRIPTION
                    });
            }

            count++;
        }

    }

    static async Task DownloadStepFiles(List<VideoAndLinks> videoAndLinksList, string pdfFolderPath)
    {
        foreach (VideoAndLinks videoAndLinks in videoAndLinksList)
        {
            bool stepSuccess = false;
            bool invSuccess = false;
            if (!string.IsNullOrEmpty(videoAndLinks.StepLink))
            {
                stepSuccess  = await X(videoAndLinks.StepLink, pdfFolderPath);
            }

            if (!string.IsNullOrEmpty(videoAndLinks.InvLink))
            {
                invSuccess = await X(videoAndLinks.InvLink, pdfFolderPath);
            }

            if (stepSuccess && invSuccess)
            {
                videoAndLinks.FileData = FileData.STEP_INV;
            }
            else if (stepSuccess)
            {
                videoAndLinks.FileData = FileData.STEP;
            }
            else if (invSuccess)
            {
                videoAndLinks.FileData = FileData.INV;
            }
            else
            {
                videoAndLinks.FileData = FileData.DEAD;
            }
        }
    }

    static async Task<bool> X(string assetUrl, string pdfFolderPath)
    {
        string downloadUrl = await FetchDownloadLink(assetUrl);
        if (string.IsNullOrEmpty(downloadUrl)) return false;

        // download zip file
        string zipPath = Path.Combine(pdfFolderPath, assetUrl[(assetUrl.LastIndexOf('/') + 1)..]);
        Console.WriteLine($"Downloading {assetUrl} to {zipPath}");
        using HttpClient client = new();

        try
        {
            byte[] bytes = await client.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return false;
        }

        return true;
    }

    static async Task<string> FetchDownloadLink(string url)
    {
        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync(url);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return "";
        }

        string htmlContent = await response.Content.ReadAsStringAsync();

        HtmlDocument htmlDoc = new();
        htmlDoc.LoadHtml(htmlContent);

        HtmlNode? downloadButton = htmlDoc.DocumentNode.SelectSingleNode("//*[@id='downloadButton']");
        if (downloadButton == null)
        {
            throw new InvalidOperationException("Download button not found.");
        }

        string? downloadLink = downloadButton.GetAttributeValue("href", string.Empty);
        if (string.IsNullOrEmpty(downloadLink))
        {
            throw new InvalidOperationException("No download link found in the download button.");
        }

        return downloadLink;
    }
}