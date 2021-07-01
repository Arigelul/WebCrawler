using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using TurnerSoftware.SitemapTools;

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
            var crawlingLinks = GetCrawlingLinks(inputUrl);
            var mergedLinks = crawlingLinks;
            Console.WriteLine($"Total Urls found by crawling: {crawlingLinks.Count}");

            Console.WriteLine("\nFinding Urls from sitemap.xml...");
            var sitemapLinks = await GetSitemapLinksAsync(inputUrl);
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
            var timings = new int[mergedLinks.Count];
            for (int i = 0; i < mergedLinks.Count; i++)
                timings[i] = GetTiming(mergedLinks[i]);

            var sortedLinks = mergedLinks.Select((link, index) => new { Link = link, Index = index })
                .OrderBy(x => timings.ElementAtOrDefault(x.Index))
                .Select(x => x.Link)
                .ToList();
            timings = timings.OrderBy(x => x).ToArray();
            for (int i = 0; i < sortedLinks.Count; i++)
                Console.WriteLine($"{i + 1}) {sortedLinks[i]}\t{timings[i]}");
        }

        public static List<string> GetCrawlingLinks(Uri inputUrl)
        {
            var foundLinks = GetLinksFromHtml(inputUrl);
            var crawledPages = new List<string>() { inputUrl.ToString() };
            string domain = inputUrl.GetLeftPart(UriPartial.Authority);
            for (int i = 0; i < foundLinks.Count; i++)
            {
                if (!crawledPages.Contains(foundLinks[i]))
                {
                    crawledPages.Add(foundLinks[i]);
                    foundLinks = foundLinks.Union(GetLinksFromHtml(new Uri(foundLinks[i])).Where(s => s.StartsWith(domain))).ToList();
                    //if (foundLinks.Count > 1000) return foundLinks;
                }
            }
            return foundLinks;
        }

        public static List<string> GetLinksFromHtml(Uri inputUrl)
        {
            var regex = new Regex("^http(s)?://" + inputUrl.Host, RegexOptions.IgnoreCase);
            var doc = new HtmlWeb().Load(inputUrl);

            return doc.DocumentNode
                .Descendants("a")
                .Select(a =>
                {
                    var val = a.GetAttributeValue("href", string.Empty);
                    return val.StartsWith("/") ? inputUrl.GetLeftPart(UriPartial.Authority) + val : val;
                })
                .Distinct()
                .Where(u => !string.IsNullOrEmpty(u) && regex.IsMatch(u))
                .ToList();
        }

        public static async Task<List<string>> GetSitemapLinksAsync(Uri inputUrl)
        {
            var sitemapLinks = new List<string>();
            var sitemapQuery = new SitemapQuery();
            var sitemapEntries = await sitemapQuery.GetAllSitemapsForDomainAsync(inputUrl.Host);

            foreach (var sitemap in sitemapEntries)
            {
                if (sitemap.Location.ToString().EndsWith(".xml"))
                {
                    XmlDocument docXML = new XmlDocument();
                    docXML.Load(sitemap.Location.ToString());

                    int count = docXML.GetElementsByTagName("loc").Count;
                    for (int i = 0; i < count; i++)
                        sitemapLinks.Add(docXML.GetElementsByTagName("loc")[i].InnerText);
                }
            }
            return sitemapLinks.Distinct().ToList();
        }

        public static int GetTiming(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = new HttpWebResponse();
            Stopwatch timer = new Stopwatch();
            timer.Start();
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                return 999;
            }
            timer.Stop();
            return timer.Elapsed.Milliseconds;
        }
    }
}