using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CrawMusicWebSite
{
    public class ImageDownloader
    {
        public void DownloadImagesFromUrl(string url, string folderImagesPath)
        {
            var uri = new Uri(url + "/?per_page=50");
            var pages = new List<HtmlNode> { LoadHtmlDocument(uri) };

            pages.AddRange(LoadOtherPages(pages[0], url));

            pages.SelectMany(p => p.SelectNodes("//a[@class='catalog__displayedItem__columnFotomainLnk']/img"))
                 .Select(node => Tuple.Create(new UriBuilder(uri.Scheme, uri.Host, uri.Port, node.Attributes["src"].Value).Uri, new WebClient()))
                 .AsParallel()
                 .ForAll(t => DownloadImage(folderImagesPath, t.Item1, t.Item2));
        }

        public static string DownloadImageDirect(string folderImagesPath, string url, WebClient webClient)
        {
            var uri = new Uri(url);
            var tuple = Tuple.Create(new UriBuilder(url).Uri, new WebClient());
            return DownloadImage(folderImagesPath, tuple.Item1, tuple.Item2);
        }

        private static string DownloadImage(string folderImagesPath, Uri url, WebClient webClient)
        {
            try
            {
                var filePath = Path.Combine(folderImagesPath, Path.GetFileName(url.ToString()));
                webClient.DownloadFile(url, filePath);
                return Path.Combine(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        private static IEnumerable<HtmlNode> LoadOtherPages(HtmlNode firstPage, string url)
        {
            return Enumerable.Range(1, DiscoverTotalPages(firstPage))
                             .AsParallel()
                             .Select(i => LoadHtmlDocument(new Uri(url + "/?per_page=50&page=" + i)));
        }

        private static int DiscoverTotalPages(HtmlNode documentNode)
        {
            var totalItemsDescription = documentNode.SelectNodes("//div[@class='catalogItemList__numsInWiev']").First().InnerText.Trim();
            var totalItems = int.Parse(Regex.Match(totalItemsDescription, @"\d+$").ToString());
            var totalPages = (int)Math.Ceiling(totalItems / 50d);
            return totalPages;
        }

        private static HtmlNode LoadHtmlDocument(Uri uri)
        {
            var doc = new HtmlDocument();
            var wc = new WebClient();
            doc.LoadHtml(wc.DownloadString(uri));

            var documentNode = doc.DocumentNode;
            return documentNode;
        }
    }
}
