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
            
            string domain = inputUrl.GetLeftPart(UriPartial.Authority);
            Console.WriteLine($"Domain URL - {domain}");

            Console.WriteLine("\nCrawling web pages...");
            var crawlingLinks = GetCrawlingLinks(domain);
            var mergedLinks = crawlingLinks;
            Console.WriteLine($"Total Urls found by crawling: {crawlingLinks.Count}");

            Console.WriteLine("\nFinding Urls from sitemap.xml...");
            var sitemapLinks = await GetSitemapLinksAsync(inputUrl);
            if (sitemapLinks.Count > 0)
            {
                Console.WriteLine($"Total Urls found from sitemap.xml: {sitemapLinks.Count}");
                mergedLinks = mergedLinks.Union(sitemapLinks).ToList();
                //mergedLinks = mergedLinks.Union(sitemapLinks).Take(100).ToList();
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

        public static List<string> GetCrawlingLinks(string domain)
        {
            var crawlingLinks = new List<string>();
            Chilkat.Spider crawler = new Chilkat.Spider();
            Chilkat.StringArray seedUrls = new Chilkat.StringArray();
            seedUrls.Append(domain);

            crawler.AddAvoidOutboundLinkPattern("*?id=*");
            crawler.AddAvoidOutboundLinkPattern("*?do=*");
            crawler.AddAvoidOutboundLinkPattern("*.mypages.*");
            crawler.AddAvoidOutboundLinkPattern("*.personal.*");
            crawler.AddAvoidOutboundLinkPattern("*.comcast.*");
            crawler.AddAvoidOutboundLinkPattern("*.aol.*");
            crawler.AddAvoidOutboundLinkPattern("*~*");

            while (seedUrls.Count > 0)
            {
                string url = seedUrls.Pop();
                crawler.Initialize(url);

                for (bool success = crawler.CrawlNext(); success; success = crawler.CrawlNext())
                    crawlingLinks.Add(crawler.LastUrl);
            }
            return crawlingLinks.Distinct().ToList();
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
