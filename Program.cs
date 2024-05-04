using System.Collections.Specialized;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace StepScraper;

public class Program
{
    public static async Task Main(string[] args)
    {
        string pdfFolderPath = args[0];

        List<string> youtubeLinks = DocReader.ReadFolderAndExtractYoutubeLinks(pdfFolderPath);
        List<string> videoIds = youtubeLinks.Select(ExtractVideoIdFromUrl).ToList();

        string jsonPath = Path.Combine(pdfFolderPath, "videoTitleAndDescriptions.json");

        Dictionary<string, Tuple<string, string>> videoTitleAndDescriptions = new();
        if (!File.Exists(jsonPath))
        {
            string json = JsonConvert.SerializeObject(videoTitleAndDescriptions, Formatting.Indented);
            await File.WriteAllTextAsync(jsonPath, json);
        }
        
        string jsonFileText = await File.ReadAllTextAsync(jsonPath);
        videoTitleAndDescriptions =
            JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(jsonFileText) ?? [];

        ScrapeYoutubeAndWriteJson(videoIds, videoTitleAndDescriptions, args[1], jsonPath);

        List<string> stepZipUrls = [];
        foreach ((string? title, string? description) in videoTitleAndDescriptions.Values)
        {
            string[] lines = description.Split('\n');
            foreach (string line in lines)
            {
                if (line.EndsWith("STEP.zip"))
                {
                    stepZipUrls.Add(line);
                }
            }
        }

        await DownloadStepFiles(stepZipUrls, pdfFolderPath);
    }

    static string ExtractVideoIdFromUrl(string url)
    {
        Uri uri = new(url);
        NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query["v"] ?? url[(url.LastIndexOf('/') + 1)..];
    }

    static void ScrapeYoutubeAndWriteJson(
        List<string> videoIds,
        IDictionary<string, Tuple<string, string>> videoTitleAndDescriptions,
        string apiKey,
        string jsonPath)
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
                    new Tuple<string, string>(video.Snippet.Title, video.Snippet.Description));
            }

            count++;
        }

        string json = JsonConvert.SerializeObject(videoTitleAndDescriptions, Formatting.Indented);

        File.WriteAllText(jsonPath, json);
        Console.WriteLine("json written to " + jsonPath);
    }

    static async Task DownloadStepFiles(List<string> stepZipUrls, string pdfFolderPath)
    {
        foreach (string stepZipUrl in stepZipUrls)
        {
            string downloadUrl = await FetchDownloadLink(stepZipUrl);
            if (string.IsNullOrEmpty(downloadUrl)) continue;

            // download zip file
            string zipPath = Path.Combine(pdfFolderPath, stepZipUrl[(stepZipUrl.LastIndexOf('/') + 1)..]);
            Console.WriteLine($"Downloading {stepZipUrl} to {zipPath}");
            using HttpClient client = new();

            try
            {
                byte[] bytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(zipPath, bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
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