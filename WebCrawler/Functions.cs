using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using TurnerSoftware.SitemapTools;

public static class Functions
{
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

    public static int[] GetTimingsList(List<string> mergedLinks)
    {
        var timings = new int[mergedLinks.Count];
        for (int i = 0; i < mergedLinks.Count; i++)
            timings[i] = Functions.GetTiming(mergedLinks[i]);
        return timings;
    }

    public static List<string> GetSortedByTimingsLinks(List<string> mergedLinks, int[] timings)
    {
        var sortedLinks = mergedLinks.Select((link, index) => new { Link = link, Index = index })
                .OrderBy(x => timings.ElementAtOrDefault(x.Index))
                .Select(x => x.Link)
                .ToList();
        return sortedLinks;
    }
}