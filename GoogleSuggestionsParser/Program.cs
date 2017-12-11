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
                .WithDescription("Work mode: s (parse suggestions) r (get search results by query) d (parse by developer) c (encode result to CSV)");


            var result = parser.Parse(args);
            if (result.HasErrors)
            {
                Console.WriteLine(result.ErrorText);
                Console.WriteLine("--mode for work mode: s (parse suggestions) r (get search results by query) d (parse by developer) c (encode result to CSV)");
                return;
            }

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

            var queryFile = $"{parser.Object.Query}.json";
            var csvFile = $"{parser.Object.Query}.csv";

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

                var sectionMetaHtmlNodes = doc.DocumentNode.SelectNodes(@"//div[@class='details-section-contents']/div[@class='meta-info']");
                if (sectionMetaHtmlNodes == null)
                    return entry;
                var currentpos = 0;
                foreach(var node in sectionMetaHtmlNodes)
                {
                    var titleNode = node.SelectSingleNode(@"./div[@class='title']");
                    if (titleNode == null)
                        return entry;

                    var title = titleNode.InnerText;

                    var contentNode = titleNode.SelectSingleNode("following-sibling::div[1]");
                    if (contentNode == null)
                        return entry;

                    var content = contentNode.InnerText;

                    switch (currentpos)
                    {
                        // Обновлено
                        case 0:
                            entry.Updated = content.Trim();
                            break;
                        // Количество установок
                        case 1:
                            entry.Installations = content.Trim();
                            break;
                        // Текущая версия
                        case 2:
                            entry.CurrentVersion = content.Trim();
                            if(entry.CurrentVersion.StartsWith("Version"))
                            {
                                entry.CurrentVersion = "0";
                            }
                            break;
                    }

                    currentpos++;
                }
            }
            return entry;
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

    class CommandLineArguments
    {
        public string Query { get; set; }
        public string Mode { get; set; }
    }

    class GooglePlayEntry
    {
        public string SearchQuery { get; set; }
        public string AppId { get; set; }
        public string DevId { get; set; }
        public string Desc { get; set; }
        public string Updated { get; set; }
        public string Installations { get; set; }
        public string CurrentVersion { get; set; }


        public string InstallationsEx
        {
            get
            {
                if (string.IsNullOrEmpty(Installations))
                    return string.Empty;

                var tmp = Installations
                    .Replace(",", string.Empty)
                    .Replace(".", string.Empty)
                    .Replace(" ", string.Empty)
                    .Split('–');

                if (tmp.Length != 2)
                    return $"\t";
                return $"{tmp[0].Trim()}\t{tmp[1].Trim()}";

            }
        }

        public string UpdatexEx
        {
            get
            {
                if (string.IsNullOrEmpty(Updated))
                    return string.Empty;

                var tmp = Updated.Split(' ');
                if (tmp.Length < 3)
                    return $"\t\t";
                //return $"{tmp[0]}\t{MonthNumberByName(tmp[1])}\t{tmp[2]}";
                int day = 0;
                int month = MonthNumberByName(tmp[1]);
                int year = 0;

                if (!int.TryParse(tmp[0], out day) || !int.TryParse(tmp[2], out year))
                    return string.Empty;

                return string.Format("{0:D2}.{1:D2}.{2}", day, month, year);
            }
        }

        public string AppIdUrl
        {
            get
            {
                return $"https://play.google.com/{AppId}";
            }
        }

        public string DevIdUrl
        {
            get
            {
                return $"https://play.google.com/{DevId}";
            }
        }

        public string AppName { get; set; }

        public override string ToString()
        {
            return $"{AppName}\t{Desc}\t{UpdatexEx}\t{InstallationsEx}\t{CurrentVersion}\t{AppIdUrl}\t{DevIdUrl}\t{SearchQuery}";
            //return $"{SearchQuery}\t{AppId}\t{DevId}\t{Desc}\t{UpdatexEx}\t{Installations}\t{CurrentVersion}\t{AppIdUrl}\t{DevIdUrl}";
        }

        public static int MonthNumberByName(string month)
        {
            var months = new string[] { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря", };

            for (var i = 0; i < months.Length; i++)
                if (months[i] == month.ToLower())
                    return i + 1;

            return 0;
        }

    }

    /// <summary>
    /// [{"s":"how to draw batman","t":"q"},{"s":"how to draw barbie","t":"q"}]
    /// </summary>
    class Suggestion
    {
        public string s { get; set; }
        public string t { get; set; }
    }

 
}
