using System.Collections.Specialized;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using Newtonsoft.Json;

namespace StepScraper;

public class Program
{
    public static void Main(string[] args)
    {
        string pdfFolderPath = args[0];

        List<string> youtubeLinks = DocReader.ReadFolderAndExtractYoutubeLinks(pdfFolderPath);
        List<string> videoIds = youtubeLinks.Select(ExtractVideoIdFromUrl).ToList();
        
        string jsonPath = Path.Combine(pdfFolderPath, "videoTitleAndDescriptions.json");
        string jsonFileText = File.ReadAllText(jsonPath);
        Dictionary<string, Tuple<string, string>> videoTitleAndDescriptions =
            JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(jsonFileText) ?? [];

        ScrapeYoutubeAndWriteJson(videoIds, videoTitleAndDescriptions, args[1], jsonPath);
    }

    static string ExtractVideoIdFromUrl(string url)
    {
        Uri uri = new Uri(url);
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
            YouTubeService youtubeService = new YouTubeService(new BaseClientService.Initializer()
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
}