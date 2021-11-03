using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebCrawler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Enter URL:");
            Uri inputUrl = null;
            while (!Uri.TryCreate(Console.ReadLine(), UriKind.Absolute, out inputUrl))
                Console.WriteLine("Enter valid URL:");

            Console.WriteLine("\nCrawling web pages...");
            var crawlingLinks = Functions.GetCrawlingLinks(inputUrl);
            var mergedLinks = crawlingLinks;
            Console.WriteLine($"Total Urls found by crawling: {crawlingLinks.Count}");

            Console.WriteLine("\nFinding Urls from sitemap.xml...");
            var sitemapLinks = await Functions.GetSitemapLinksAsync(inputUrl);
            if (sitemapLinks.Count > 0)
            {
                Console.WriteLine($"Total Urls found from sitemap.xml: {sitemapLinks.Count}");
                mergedLinks = mergedLinks.Union(sitemapLinks).ToList();
            }
            else
                Console.WriteLine("Sitemap.xml is not found.");

            var firstResult = mergedLinks.Except(crawlingLinks).ToList();
            Console.WriteLine($"\nUrls found in sitemap.xml but not found after crawling a web site ({firstResult.Count}):");
            foreach (var url in firstResult)
                Console.WriteLine(url);

            var secondResult = mergedLinks.Except(sitemapLinks).ToList();
            Console.WriteLine($"\nUrls found by crawling the web site but not found in sitemap.xml ({secondResult.Count}):");
            foreach (var url in secondResult)
                Console.WriteLine(url);

            Console.WriteLine("\nQuerying the Urls and calculating response times...");
            var timings = Functions.GetTimingsList(mergedLinks);
            var sortedLinks = Functions.GetSortedByTimingsLinks(mergedLinks, timings);
            var sortedTimings = timings.OrderBy(x => x).ToArray();
            for (int i = 0; i < sortedLinks.Count; i++)
                Console.WriteLine($"{i + 1}) {sortedLinks[i]}\t{sortedTimings[i]}");
        }
    }
}