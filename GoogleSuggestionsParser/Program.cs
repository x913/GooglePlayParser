using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Fclp;
using HtmlAgilityPack;
using System.IO;

namespace GoogleSuggestionsParser
{
    class Program
    {
        static void Main(string[] args)
        {

            var parser = new FluentCommandLineParser<CommandLineArguments>();

            parser.SetupHelp("?", "help")
                .Callback(text => Console.WriteLine(text) );

            parser.Setup(arg => arg.Query)
                .As('q', "query")
                .Required()
                .WithDescription("Query to google, i.e. how to draw");

            parser.Setup(arg => arg.Mode)
                .As('m', "mode")
                .Required()
                .WithDescription("Work mode: f (parse categories - topselling_free) s (parse suggestions) r (get search results by query) d (parse by developer) c (encode result to CSV)");


            var result = parser.Parse(args);
            if (result.HasErrors)
            {
                Console.WriteLine(result.ErrorText);
                Console.WriteLine("--mode for work mode: f (parse categories - topselling_free) s (parse suggestions) r (get search results by query) d (parse by developer) c (encode result to CSV)");
                return;
            }

            var queryFile = $"{parser.Object.Query}.json";
            var csvFile = $"{parser.Object.Query}.csv";

            // parse suggestion
            if (parser.Object.Mode == "s")
            {
                var poolOfSuggestions = new List<string>();
                foreach (var pm in PermuteLetters())
                {
                    var sss = GetSuggestion($"{parser.Object.Query} {pm}");
                    foreach (var s in sss)
                    {
                        if (poolOfSuggestions.Any(x => x == s))
                            continue;
                        poolOfSuggestions.Add(s);
                        Console.WriteLine(s);
                    }
                }

                return;
            }

            // parse all free categories
            if(parser.Object.Mode == "f")
            {
                if (!System.IO.File.Exists("categories.txt"))
                {
                    Console.WriteLine("File with categories list (categories.txt) not found!");
                    return;
                }

                var categoires = parser.Object.Query.Split(' ');
                var allEntries = new List<GooglePlayEntry>();
                foreach(var category in categoires.Select( x => x.ToUpper().Trim()))
                {
                    try
                    {
                        var entries = ParseTopsellingFreeCategory(category);
                        foreach(var entry in entries)
                        {
                            ParseGooglePlayApplicationPage(entry);
                        }
                        allEntries.AddRange(entries);
                        var tmpresult = JsonConvert.SerializeObject(allEntries, Formatting.Indented);
                        File.WriteAllText("topsellfree_categories.json", tmpresult);
                    } catch(Exception ex)
                    {
                        Console.WriteLine($"Error while parsing {category} - {ex.Message}");
                    }
                    ConvertJsonToCsv("topsellfree_categories.json", "topsellfree_categories.csv");
                }

            }

            // parse apps by search query
            if (parser.Object.Mode == "r")
            {

                GooglePlayEntry[] googlePlayEntries = null;
                if (File.Exists(queryFile))
                {
                    try
                    {
                        googlePlayEntries = JsonConvert.DeserializeObject<GooglePlayEntry[]>(System.IO.File.ReadAllText(queryFile));
                    } catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                } 

                if(googlePlayEntries == null)
                    googlePlayEntries = ParseSearchResultsByQuery(parser.Object.Query).ToArray();

                for (var i = 0; i < googlePlayEntries.Length; i++)
                {

                    if (!string.IsNullOrEmpty(googlePlayEntries[i].CurrentVersion))
                    {
                        Console.WriteLine($"{googlePlayEntries[i].AppId} already parsed, skip...");
                        continue;
                    }
                    Console.WriteLine($"{googlePlayEntries[i].AppId} parsing...");
                    googlePlayEntries[i] = ParseGooglePlayApplicationPage(googlePlayEntries[i]);
                    var tmpresult = JsonConvert.SerializeObject(googlePlayEntries);
                    File.WriteAllText(queryFile, tmpresult);
                }
                ConvertJsonToCsv(queryFile, csvFile);
                return;
            }

            // parse apps by developer id
            if(parser.Object.Mode == "d")
            {
                GooglePlayEntry[] googlePlayEntries = null;
                if (File.Exists(queryFile))
                {
                    try
                    {
                        googlePlayEntries = JsonConvert.DeserializeObject<GooglePlayEntry[]>(File.ReadAllText(queryFile));
                    } catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                if (googlePlayEntries == null)
                    googlePlayEntries = ParseSearchResultsByDeveloper(parser.Object.Query).ToArray();

                for (var i = 0; i < googlePlayEntries.Length; i++)
                {

                    if (!string.IsNullOrEmpty(googlePlayEntries[i].CurrentVersion))
                    {
                        Console.WriteLine($"{googlePlayEntries[i].AppId} already parsed, skip...");
                        continue;
                    }
                    Console.WriteLine($"{googlePlayEntries[i].AppId} parsing...");
                    googlePlayEntries[i] = ParseGooglePlayApplicationPage(googlePlayEntries[i]);
                    var tmpresult = JsonConvert.SerializeObject(googlePlayEntries);
                    File.WriteAllText(queryFile, tmpresult);
                }
                ConvertJsonToCsv(queryFile, csvFile);
                return;
            }

            // try to encode query from json to csv
            if(parser.Object.Mode == "c")
            {   
                ConvertJsonToCsv(queryFile, csvFile);
                return;
            }


            //Console.WriteLine("Unknown work mode");
        }

        static void ConvertJsonToCsv(string input, string output)
        {
            if (System.IO.File.Exists(input))
            {
                GooglePlayEntry[] googlePlayEntries = null;
                try
                {
                    googlePlayEntries = JsonConvert.DeserializeObject<GooglePlayEntry[]>(System.IO.File.ReadAllText(input));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }

                using (var writer = new StreamWriter(output))
                {
                    foreach (var entry in googlePlayEntries)
                    {
                        writer.WriteLine(entry);
                    }
                }
                Console.WriteLine($"Done: {input} converted to {output}");
            }
        }

        /// <summary>
        /// Parse application page
        /// </summary>
        /// <param name="entry"></param>
        static GooglePlayEntry ParseGooglePlayApplicationPage(GooglePlayEntry entry)
        {
            using (var wc = new WebClient())
            {
                WebHeaderCollection headers = new WebHeaderCollection
                {
                    [HttpRequestHeader.UserAgent] = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Ubuntu/10.04 Chromium/11.0.696.0 Chrome/11.0.696.0 Safari/534.24"
                };
                wc.Encoding = Encoding.UTF8;
                wc.Headers = headers;
                var response = wc.DownloadString($"https://play.google.com/{entry.AppId}");

                Console.WriteLine($"Parsing - {entry.AppId}");

                var doc = new HtmlDocument();
                doc.LoadHtml(response);
                var descrHtmlNode = doc.DocumentNode.SelectSingleNode(@"//div[@class='show-more-content text-body']");
                if (descrHtmlNode == null)
                    return entry;
                var descriptionFull = descrHtmlNode.InnerText;

                var ratingHtmlNode = doc.DocumentNode.SelectSingleNode(@"//div[@class='rating-box']/div/meta");
                if (ratingHtmlNode == null)
                    return entry;
                var rating = ratingHtmlNode.GetAttributeValue("content", "0.0");

                foreach (var itemprop in new string[] { "datePublished", "numDownloads", "softwareVersion" }) {
                    var node = doc.DocumentNode.SelectSingleNode($"//div[@itemprop='{itemprop}']");
                    if (node == null)
                        continue;
                    switch(itemprop)
                    {
                        case "datePublished":
                            entry.Updated = node.InnerText;
                            break;
                        case "numDownloads":
                            entry.Installations = node.InnerText;
                            break;
                        case "softwareVersion":
                            entry.CurrentVersion = node.InnerText;
                            break;
                    }
                }

               
            }
            return entry;
        }

        /// <summary>
        /// Get content from https://play.google.com/store/apps/category/[CATEGORY]/collection/topselling_free
        /// and parse all applications from it
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        static IEnumerable<GooglePlayEntry> ParseTopsellingFreeCategory(string category)
        {
            using (var wc = new WebClient())
            {
                WebHeaderCollection headers = new WebHeaderCollection
                {
                    [HttpRequestHeader.UserAgent] = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Ubuntu/10.04 Chromium/11.0.696.0 Chrome/11.0.696.0 Safari/534.24"
                };
                wc.Encoding = Encoding.UTF8;
                wc.Headers = headers;
                return ParseGooglePlaySearchResults(wc.DownloadString($"https://play.google.com/store/apps/category/{category}/collection/topselling_free"), category);
            }
        }

        static IEnumerable<GooglePlayEntry> ParseSearchResultsByDeveloper(string developerId)
        {
            using (var wc = new WebClient())
            {
                WebHeaderCollection headers = new WebHeaderCollection
                {
                    [HttpRequestHeader.UserAgent] = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Ubuntu/10.04 Chromium/11.0.696.0 Chrome/11.0.696.0 Safari/534.24"
                };
                wc.Encoding = Encoding.UTF8;
                wc.Headers = headers;
                var aaa = $"https://play.google.com/store/apps/developer?id={developerId}";
                return ParseGooglePlaySearchResults(wc.DownloadString($"https://play.google.com/store/apps/developer?id={developerId}"), developerId);
            }
        }

        /// <summary>
        /// Parse search results by query, i.e. how to draw a ...
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        static IEnumerable<GooglePlayEntry> ParseSearchResultsByQuery(string q)
        {
            using (var wc = new WebClient())
            {
                WebHeaderCollection headers = new WebHeaderCollection
                {
                    [HttpRequestHeader.UserAgent] = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Ubuntu/10.04 Chromium/11.0.696.0 Chrome/11.0.696.0 Safari/534.24"
                };
                wc.Encoding = Encoding.UTF8;
                wc.Headers = headers;
                var query = new NameValueCollection()
                {
                    { "q", q},
                    { "c", "apps"},
                };
                wc.QueryString = query;
                return ParseGooglePlaySearchResults(wc.DownloadString("https://play.google.com/store/search"), q);
            }
        }

        static IEnumerable<GooglePlayEntry> ParseGooglePlaySearchResults(string response, string q)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(response);
            //var nodes = doc.DocumentNode.SelectNodes(@"//div[@class='card no-rationale square-cover apps small']");
            var nodes = doc.DocumentNode.SelectNodes(@"//div[contains(@class, 'card no-rationale square-cover apps')]");
            var entries = new List<GooglePlayEntry>();
            if (nodes != null)
            {
                foreach (HtmlNode node in nodes)
                {
                    try
                    {
                        var entry = new GooglePlayEntry();
                        entry.SearchQuery = q;

                        // get application
                        var detailsNode = node.SelectSingleNode(@"div/div[@class='details']");

                        var hrefNode = detailsNode.SelectSingleNode("a[@class='title']");
                        entry.AppId = hrefNode.GetAttributeValue("href", string.Empty);
                        entry.AppName = hrefNode.InnerText.Trim();

                        Console.WriteLine(entry.AppId);

                        // get description from details
                        var description = detailsNode.SelectSingleNode(@"div[@class='description']");
                        //Console.WriteLine(description.InnerText.Trim());
                        entry.Desc = description.InnerText.Trim();

                        // get developer from description
                        var ahref = detailsNode.SelectSingleNode(@"div[@class='subtitle-container']/a");
                        var href = ahref.GetAttributeValue("href", string.Empty);
                        entry.DevId = href;
                        entries.Add(entry);
                        //Console.WriteLine(href);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ex.Message} while parsing {q}");
                    }
                }
            }
            return entries;
        }

        static IEnumerable<string> GetSuggestion(string q)
        {
            using (var wc = new WebClient())
            {
                WebHeaderCollection headers = new WebHeaderCollection
                {
                    [HttpRequestHeader.UserAgent] = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Ubuntu/10.04 Chromium/11.0.696.0 Chrome/11.0.696.0 Safari/534.24"
                };
                wc.Encoding = Encoding.UTF8;
                wc.Headers = headers;
                var query = new NameValueCollection()
                {
                    { "json", "1"},
                    { "c", "3"},
                    { "query", q},
                    { "hl", "ru"},
                    { "gl", "RU"},
                };
                wc.QueryString = query;
                var response = wc.DownloadString("https://market.android.com/suggest/SuggRequest");
                var result = JsonConvert.DeserializeObject<IEnumerable<Suggestion>>(response);
                return result
                    .Select(x => x.s)
                    .ToList();
            }
        }

        static IEnumerable<string> PermuteLetters()
        {
            var chars = new char[] { 'a', 'b', 'c', 'd', 'e', 'g', 'h', 'i', 'j', 'k', 'm', 'n', 'o', 'p', 'q', 's', 't', 'u', 'v', 'w', 'y', 'z' };
            var iteration = 0;
            var startWith = 0;


            foreach(var c in chars)
            {
                yield return c.ToString();
            }

            while (true)
            {
                for (var i = startWith; i < chars.Length; i++)
                {
                    yield return $"{chars[i]}{chars[iteration]}";
                }
                iteration++;
                if (iteration >= chars.Length)
                {
                    startWith++;
                    if (startWith >= chars.Length)
                        break;
                    iteration = 0;
                }
            }
        }
    }

 
}
