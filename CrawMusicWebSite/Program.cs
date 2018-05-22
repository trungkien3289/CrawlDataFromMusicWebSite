using Jurassic.Library;
using NAudio.Wave;
using Newtonsoft.Json;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CrawMusicWebSite
{
    class Program
    {
        public static string ALBUM_URL_BASE = "album/";
        public static string ARTIST_URL_BASE = "artist/";
        public static string SONG_URL_BASE = "song/";
        public static string TOPDOMAIN = "cristiana.fm";
        public static string CDN_DOMAIN = "http://cdn.cristiana.fm/";
        public static string DOMAIN = "http://cristiana.fm/";
        public static string DOMAIN_WITHOUT_SLASH = "http://cristiana.fm";
        public static string folderAlbumImagePath = @"E:\CrawlData\Album_Images";
        public static string folderArtistImagePath = @"E:\CrawlData\Artist_Images";
        public static string folderSongPath = @"E:\CrawlData\Album_Songs";
        public static string[] AlbumCategory = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "0" };

        static void Main(string[] args)
        {
            //GetListAlbumData();
            crawlAlbums();
            //MigrateData();
            //CrawlMusicFile();
            //GetDurationOfMusicFile();
        }

        #region Crawl data

        public static void GetListAlbumData()
        {
            ScrapingBrowser Browser = new ScrapingBrowser();
            Browser.AllowAutoRedirect = true; // Browser has settings you can access in setup
            Browser.AllowMetaRedirect = true;
            Browser.Encoding = Encoding.UTF8;
            var albumCategoryLinks = getAlbumCategoryLinks();
            if (albumCategoryLinks != null && albumCategoryLinks.Count > 0)
            {
                for (int i = 0; i < albumCategoryLinks.Count; i++)
                {
                    GetAlbumDataOfCategory(Browser, albumCategoryLinks[i]);
                }
            }
        }

        public static void GetAlbumDataOfCategory(ScrapingBrowser Browser, string albumCategoryLink)
        {
            WebPage PageResult = Browser.NavigateToPage(new Uri(albumCategoryLink));
            GetAlbumsByPrefixResponse response = JsonConvert.DeserializeObject<GetAlbumsByPrefixResponse>(PageResult.ToString());
            try
            {
                if (response.code == 0 && response.data != null && response.data.Count > 0)
                {
                    using (CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities())
                    {
                        crawlDataContext.Album_Data.AddRange(response.data);
                        crawlDataContext.SaveChanges();
                    }
                }
            }catch(Exception ex)
            {
                Console.WriteLine("Error on GetAlbumDataOfCategory: {0}", ex.Message);
            }
        }

        public static void crawlAlbums()
        {
            ScrapingBrowser Browser = new ScrapingBrowser();
            Browser.AllowAutoRedirect = true; // Browser has settings you can access in setup
            Browser.AllowMetaRedirect = true;
            Browser.Encoding = Encoding.UTF8;

            var albumUrls = new List<string>();
            //var albumCategoryLinks = getAlbumCategoryLinks();
            //if (albumCategoryLinks != null && albumCategoryLinks.Count > 0)
            //{
            //    for (int i = 0; i < albumCategoryLinks.Count; i++)
            //    {
            //        albumUrls.AddRange(getAlbumLinks(Browser, albumCategoryLinks[i]));
            //    }
            //}

            using (CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities())
            {
                albumUrls = crawlDataContext.Album_Data.ToList().Select(a => DOMAIN_WITHOUT_SLASH + a.urlalbum).ToList();
            }


                Console.WriteLine("Total album " + albumUrls.Count);
            for (int i = 0; i < albumUrls.Count; i++)
            {
                Console.WriteLine("Begin album " + i);
                Console.WriteLine("Album link: " + albumUrls[i]);
                ScrapingAlbum(Browser, albumUrls[i]);
                Console.WriteLine("End album " + i);
            }
        }

        public static List<string> getAlbumCategoryLinks()
        {
            List<string> links = new List<string>();
            var listAlbumLinksAfterCharacters = new List<string>();
            string albumLinkTemplate = "http://www.cristiana.fm/ajax/album?t=1&siteId=2da0afc6-f506-4964-985b-36261ab4fdd0&l={0}&top=1000&page=1";
            for (int i = 0; i < AlbumCategory.Length; i++)
            {
                listAlbumLinksAfterCharacters.Add(string.Format(albumLinkTemplate, AlbumCategory[i]));
            }

            return listAlbumLinksAfterCharacters;
        }

        public static List<string> getAlbumLinks(ScrapingBrowser Browser, string albumCategoryLink)
        {
            List<string> albumUrls = new List<string>();
            WebPage PageResult = Browser.NavigateToPage(new Uri(albumCategoryLink));
            GetAlbumsByPrefixResponse response = JsonConvert.DeserializeObject<GetAlbumsByPrefixResponse>(PageResult.ToString());
            if (response.code == 0 && response.data != null && response.data.Count > 0)
            {
                albumUrls = response.data.Select(a => DOMAIN_WITHOUT_SLASH + a.urlalbum).ToList();
            }

            return albumUrls;
        }

        public static Album ScrapingAlbum(ScrapingBrowser Browser, string link)
        {
            CrawlDatabaseEntities context = new CrawlDatabaseEntities();
            WebPage PageResult = Browser.NavigateToPage(new Uri(link));
            var albumScript = PageResult.Html.CssSelect("head>script");
            string scripts = "";
            foreach (var item in albumScript)
            {
                if(!String.IsNullOrEmpty(item.GetAttributeValue("src")))
                {
                    scripts += item.InnerHtml;
                }
            }
            var scriptString = PageResult.Html.CssSelect("#music>script").FirstOrDefault();

            var engine = new Jurassic.ScriptEngine();
            var result = engine.Evaluate("(function() { var MN = {};MN.m_page= {};MN.m_page.songlist = {};MN.m_page.songlist.artists = {};MN.m_page.songlist.songs = {};MN.m_page.songlist.sid = {};" + scriptString.InnerHtml + " return MN.m_page.songlist; })()");
            var json = JSONObject.Stringify(engine, result);
            songlist data = JsonConvert.DeserializeObject<songlist>(json);

            // Get list song data of album
            List<Guid>  listSongGuid = new List<Guid>(data.songs.Keys);
            List<Song> listSongs = new List<Song>();
            for (int i = 0; i < listSongGuid.Count; i++)
            {
                Console.WriteLine("-------------  Begin song " + i);
                Console.WriteLine("-------------  Song link " + listSongGuid[i]);
                listSongs.Add(FetchSong(Browser, data, listSongGuid[i], context));
                Console.WriteLine("-------------  End song " + i);
            }

            // Crawl album
            Album album = new Album();
            album.Title = PageResult.Html.CssSelect("#artist-info>article>header>h1").FirstOrDefault().InnerText.Trim();
            album.ReleaseDate = PageResult.Html.CssSelect("#artist-info>article>header>h1>time").FirstOrDefault().InnerText.TrimStart('-').Trim();
            album.ArtistName = PageResult.Html.CssSelect("#artist-info>article>header>h2>a").FirstOrDefault().InnerText.Trim();
            album.Thumbnail = PageResult.Html.CssSelect("#artist-info>article>figure>img").FirstOrDefault().GetAttributeValue("src");
            album.Slug = listSongs.Count > 0 ? listSongs[0].AlbumSlug: "";
            album.Songs = listSongs;
            SaveImage(folderAlbumImagePath, album.Thumbnail);

            // Crawl artist
            List<Artist> artist = new List<Artist>();
            List<Guid> listArtistIds = new List<Guid>(data.artists.Keys);
            album.ArtistGuid = listArtistIds.FirstOrDefault().ToString();
            for (int i = 0; i < listArtistIds.Count; i++)
            {
                var artistImageUrl = GetArtistImage(data.artists[listArtistIds[i]].slug, 2);

                var newArtist = new Artist()
                {
                    Guid = listArtistIds[i].ToString(),
                    Name = data.artists[listArtistIds[i]].artist,
                    Slug = data.artists[listArtistIds[i]].slug,
                    Thumbnail = artistImageUrl
                };

                artist.Add(newArtist);

                bool isArtistExisted = context.Artists.Where(a => a.Guid == newArtist.Guid).Any();
                if (!isArtistExisted)
                {
                    context.Artists.Add(newArtist);
                    SaveImage(folderArtistImagePath, artistImageUrl);
                }
            }

            bool isAlbumExisted = context.Albums.Where(a => a.Title == album.Title && a.ArtistName == album.ArtistName).Any();
            if (!isAlbumExisted)
            {
                context.Albums.Add(album);
            }

            context.SaveChanges();

            // Save mp3 files

            //for (int i = 0; i < listSongs.Count; i++)
            //{
            //    var mp3FullPath = Path.Combine(folderSongPath, Path.GetFileName(listSongs[i].MediaUrl));
            //    var success = FileDownloader.DownloadFile(listSongs[i].MediaUrl, mp3FullPath, 120000);
            //    Console.WriteLine("Done  - success: " + success);
            //}

            return null;
        }

        public static Song FetchSong(ScrapingBrowser Browser, songlist albumData,Guid songGuid, CrawlDatabaseEntities context)
        {
            Song song = new Song();
            var title = albumData.songs[songGuid].song;
            var slug = albumData.songs[songGuid].slug;
            var artistGuid = albumData.songs[songGuid].artistId;
            var endPathSongUrl = albumData.songs[songGuid].url;
            var songSId = albumData.sid[songGuid];
            var subDomain = (Convert.ToInt32(songSId, 16) - 100) / 7;
            var songUrl = "http://mus" + subDomain + "." + TOPDOMAIN + endPathSongUrl;
            var artistRouteName = albumData.artists[artistGuid].slug;
            var albumRouteName = albumData.songs[songGuid].albumSlug;
            var displaySongImage = "";
            if (albumData.songs[songGuid].haveAlbumImage == "True")
            {
                displaySongImage = GetAlbumImage(artistRouteName, albumRouteName, 2);
            }
            else
            {
                displaySongImage = GetArtistImage(artistRouteName, 2);
            }

            // Get lyric
            var lyricUrl = DOMAIN + String.Format("ajax/song?t=1&songId={0}", songGuid);
            WebPage PageResult = Browser.NavigateToPage(new Uri(lyricUrl));
            string lyrics = "";
            ajax_response response = JsonConvert.DeserializeObject<ajax_response>(PageResult.ToString());
            if(response.code == 0 && response.data != null && response.data.Count>0)
            {
                lyrics = response.data[0].lyrics;
            }

            song.Guid = songGuid.ToString();
            song.Title = title;
            song.ArtistGuid = artistGuid.ToString();
            song.MediaUrl = songUrl;
            song.Thumbnail = displaySongImage;
            song.Url = slug;
            song.Lyrics = lyrics;
            song.AlbumSlug = albumRouteName;
            song.ArtistSlug = artistRouteName;


            //bool hasSong = context.Songs.Where(s => s.Guid == song.Guid).Any();
            //if (!hasSong)
            //{
                context.Songs.Add(song);
            //}

            // Save resources
            //SaveImage(folderImagePath, songUrl);
            //SaveImage(folderSongPath, songUrl);

            return song;
        }

        private static string GetAlbumImage(string artistRouteName, string albumRouteName, int key)
        {
            return CDN_DOMAIN + "artists/" + artistRouteName[0] + "/" + artistRouteName + "/albums/" + albumRouteName + "-" + key + ".jpg";
        }

        private static string GetArtistImage(string artistRouteName,int key)
        {
            return CDN_DOMAIN + "artists/" + artistRouteName[0] + "/" + artistRouteName + "/" + artistRouteName + "-" + key + ".jpg";
        }

        private static void SaveImage(string folderPath, string imageUrl)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var uri = new Uri(imageUrl);
            var filePath = Path.Combine(folderPath, Path.GetFileName(uri.ToString()));
            if (!File.Exists(filePath))
            {
                string coverImageLocalPath = ImageDownloader.DownloadImageDirect(folderPath, imageUrl, new WebClient());
            }
           
        }

        #endregion

        #region migrate data
        private static void MigrateData()
        {
            MusicStoreEntities musicStoreContext = new MusicStoreEntities();
            CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities();

            List<Album> cr_Albums = crawlDataContext.Albums.ToList();

            foreach (var item in cr_Albums)
            {
                ms_Album album = new ms_Album()
                {
                    Title = item.Title,
                    Description = item.Description,
                    ReleaseDate = new DateTime(int.Parse(item.ReleaseDate), 1, 1),
                    Thumbnail = Path.GetFileName(item.Thumbnail),
                    Url = item.Slug,
                    Status = 1
                };

                var cr_Artist = crawlDataContext.Artists.Where(a => a.Guid == item.ArtistGuid).FirstOrDefault();
                ms_Artist artist = null;
                if (cr_Artist != null)
                {
                    artist = new ms_Artist()
                    {
                        Name = cr_Artist.Name,
                        Url = cr_Artist.Slug,
                        Thumbnail = Path.GetFileName(cr_Artist.Thumbnail),
                        Status = 1
                    };
                    album.ms_Artist.Add(artist);
                }
                
                album.ms_Song = new List<ms_Song>();
                foreach (var song in item.Songs)
                {
                    var newSong = new ms_Song()
                    {
                        Description = song.Description,
                        Lyrics = song.Lyrics,
                        MediaUrl = Path.GetFileName(song.MediaUrl),
                        Status = 1,
                        Thumbnail = "",
                        Title = song.Title,
                        Url = song.Url,
                        Duration = song.Duration
                    };
                    if (artist != null)
                    {
                        newSong.ms_Artist.Add(artist);
                    }
                    album.ms_Song.Add(newSong);
                }

                musicStoreContext.ms_Album.Add(album);

            }

            musicStoreContext.SaveChanges();
        }

        #endregion

        #region Crawl Music File
        private static void CrawlMusicFile()
        {
            CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities();
            List<Song> songs = crawlDataContext.Songs.ToList();
            for (int i = 0; i < songs.Count; i++)
            {
                var mp3FullPath = Path.Combine(folderSongPath, Path.GetFileName(songs[i].MediaUrl));
                if (!File.Exists(mp3FullPath))
                {
                    Console.WriteLine((i + 1) + ". Start download file " + mp3FullPath);
                    try
                    {
                        var success = FileDownloader.DownloadFile(songs[i].MediaUrl, mp3FullPath, 1200000);
                        Console.WriteLine("Done  - success: " + success);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Fail: " + (i + 1) + ": " + mp3FullPath);
                    }
                }
            }
        }
        #endregion

        #region add duration for mp3 files
        public static void GetDurationOfMusicFile()
        {
            CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities();
            List<Song> songs = crawlDataContext.Songs.ToList();
            for (int i = 0; i < songs.Count; i++)
            {
                var mp3FullPath = Path.Combine(folderSongPath, Path.GetFileName(songs[i].MediaUrl));
                int songId = songs[i].Id;
                var songModel = crawlDataContext.Songs.Where(s => s.Id == songId).FirstOrDefault();
                if (File.Exists(mp3FullPath))
                {
                    try
                    {
                        Mp3FileReader reader = new Mp3FileReader(mp3FullPath);
                        TimeSpan duration = reader.TotalTime;
                        Console.WriteLine(mp3FullPath);
                        Console.WriteLine(duration.TotalMilliseconds);
                        songModel.Duration = duration.TotalMilliseconds;
                        crawlDataContext.SaveChanges();
                    }
                    catch(Exception ex)
                    {

                    }
                    
                    //Console.WriteLine((i + 1) + ". Start download file " + mp3FullPath);
                    //try
                    //{
                    //    var success = FileDownloader.DownloadFile(songs[i].MediaUrl, mp3FullPath, 1200000);
                    //    Console.WriteLine("Done  - success: " + success);
                    //}
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine("Fail: " + (i + 1) + ": " + mp3FullPath);
                    //}
                }
            }
        }
        #endregion
    }


#region model class
    public class songlist
    {
        public Dictionary<Guid, cr_artists> artists { get; set; }
        public Dictionary<Guid, string> sid { get; set; }
        public Dictionary<Guid, cr_song> songs { get; set; }

    }

    public class cr_artists
    {
        public string artist { get; set; }
        public int counter { get; set; }
        public string slug { get; set; }
    }

    public class cr_song
    {
        public string albumSlug { get; set; }
        public Guid artistId { get; set; }
        public string haveAlbumImage { get; set; }
        public Guid id { get; set; }
        public string slug { get; set; }
        public string song { get; set; }
        public string url { get; set; }
    }

    public class ajax_response
    {
        public int code { get; set; }
        public string message { get; set; }
        public List<cr_song_detail> data { get; set; }
    }

    public class cr_song_detail
    {
        public Guid id { get; set; }
        public string song { get; set; }
        public string artist { get; set; }
        public string lyrics { get; set; }
        public string slug { get; set; }
        public string artistSlug { get; set; }
    }

    public class GetAlbumsByPrefixResponse
    {
        public int code { get; set; }
        public string message { get; set; }
        public List<Album_Data> data { get; set; }
    }

    public class AlbumInforResponse
    {
        public string album { get; set; }
        public string artist { get; set; }
        public string slug { get; set; }
        public string urlalbum { get; set; }
        public string image { get; set; }
        public string year { get; set; }
        public int countSongs { get; set; }
    }
}

#endregion

