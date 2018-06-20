using Jurassic.Library;
using NAudio.Wave;
using Newtonsoft.Json;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public static string folderSongPath = @"C:\Music Store Web\PublishOutput\Data\Album_Songs";
        //public static string folderSongPath = @"E:\CrawlData\Album_Songs";
        public static string[] AlbumCategory = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "0" };

        static void Main(string[] args)
        {
            //GetListAlbumData();
            //crawlAlbums();
            //CrawlGenres();
            //MigrateData();
            //CrawlMusicFile();
            GetDurationOfMusicFile();
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


        public static List<string> GetCollectionPageLinks()
        {
            List<string> links = new List<string>();
            var listCollectionLinksAfterCharacters = new List<string>();
            string collectionLinkTemplate = "http://www.cristiana.fm/last-playlist/{0}/";
            for (int i = 0; i < AlbumCategory.Length; i++)
            {
                listCollectionLinksAfterCharacters.Add(string.Format(collectionLinkTemplate, AlbumCategory[i]));
            }

            return listCollectionLinksAfterCharacters;
        }

        public static void GetCollectionDetailPage()
        {
            var collectionPages = GetCollectionPageLinks();
            var collectionDetailLinks = new List<string>();
            ScrapingBrowser Browser = new ScrapingBrowser();
            Browser.AllowAutoRedirect = true; // Browser has settings you can access in setup
            Browser.AllowMetaRedirect = true;
            Browser.Encoding = Encoding.UTF8;
            List<Collection_Data> collections = new List<Collection_Data>();

            for (int i = 0; i < collectionPages.Count; i++)
            {
                WebPage PageResult = Browser.NavigateToPage(new Uri(collectionPages[i]));
                var listCollectionElements = PageResult.Html.CssSelect("#t-playlist article").ToList();
                if (listCollectionElements != null && listCollectionElements.Count > 0)
                {
                    foreach (var item in listCollectionElements)
                    {
                        var collection = new Collection_Data();
                        collection.Title = item.InnerText.Trim();
                        collection.Url = DOMAIN_WITHOUT_SLASH + item.CssSelect("a").First().GetAttributeValue("href");
                        collections.Add(collection);
                    }
                }
            }
            using (CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities())
            {
                foreach (var item in collections)
                {
                    var newCollection = CrawlCollectionDetails(Browser, item.Url);
                }
            }
        }

        public static Collection CrawlCollectionDetails(ScrapingBrowser Browser, string collectionLink)
        {

            WebPage PageResult = Browser.NavigateToPage(new Uri(collectionLink));
            Collection result = new Collection();
            result.Title = PageResult.Html.CssSelect("#artist-info>article>header>h1:first").First().InnerText;
            result.Thumbnail = PageResult.Html.CssSelect("#MainContent_MainImage").First().GetAttributeValue("src");
            result.Url = collectionLink;
            result.Description = String.Empty;
            result.map_collection_song = new List<map_collection_song>();
            return result;
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

            //Console.WriteLine("Migrate Artist");
            //List<Artist> cr_Artists = crawlDataContext.Artists.ToList();
            //foreach (var item in cr_Artists)
            //{
            //    ms_Artist artist = new ms_Artist()
            //    {
            //        Name = item.Name,
            //        Status = 1,
            //        Url = item.Slug,
            //        Thumbnail = Path.GetFileName(item.Thumbnail),
            //    };

            //    musicStoreContext.ms_Artist.Add(artist);
            //}
            //musicStoreContext.SaveChanges();

            //Console.WriteLine("Migrate Genre");
            //List<Genre> cr_Genres = crawlDataContext.Genres.Include("Artists").ToList();
            //foreach (var item in cr_Genres)
            //{
            //    ms_Genre genre = new ms_Genre()
            //    {
            //        Name = item.Name,
            //        Status = 1,
            //        Url = item.Slug,
            //        Description = item.Description,
            //    };

            //    if(item.Artists != null && item.Artists.Count > 0)
            //    {
            //        foreach (var cr_artist in item.Artists)
            //        {
            //            var db_artist = musicStoreContext.ms_Artist.Where(a => a.Name == cr_artist.Name).FirstOrDefault();
            //            if(db_artist != null)
            //            {
            //                genre.ms_Artist.Add(db_artist);
            //            }
            //        }
            //    }

            //    musicStoreContext.ms_Genre.Add(genre);
            //}
            //musicStoreContext.SaveChanges();

            //Console.WriteLine("Migrate Album");
            //List<Album> cr_Albums = crawlDataContext.Albums.Include("Artist").Include("Songs").ToList();

            //foreach (var item in cr_Albums)
            //{
            //    ms_Album album = new ms_Album()
            //    {
            //        Title = item.Title,
            //        Description = item.Description,
            //        ReleaseDate = new DateTime(int.Parse(item.ReleaseDate), 1, 1),
            //        Thumbnail = Path.GetFileName(item.Thumbnail),
            //        Url = item.Slug,
            //        Status = 1
            //    };

            //    var db_Artist = musicStoreContext.ms_Artist.Where(a => a.Name == item.ArtistName).FirstOrDefault();
            //    if (db_Artist != null)
            //    {
            //        album.ms_Artist.Add(db_Artist);
            //    }

            //    musicStoreContext.ms_Album.Add(album);
            //}
            //musicStoreContext.SaveChanges();
            List<Album> cr_Albums = crawlDataContext.Albums.Include("Artist").Include("Songs").ToList();
            Console.WriteLine("Migrate Song of albums");
            foreach (var cr_album in cr_Albums)
            {
                Console.WriteLine("Begin migrate song of album " + cr_album.Title);
                var db_album = musicStoreContext.ms_Album.Include("ms_Artist").Where(a => a.Title == cr_album.Title  && a.ms_Artist.FirstOrDefault().Name == cr_album.ArtistName).FirstOrDefault();
                if (db_album != null)
                {
                    if (cr_album.Songs != null && cr_album.Songs.Count > 0)
                    {
                        foreach (var cr_song in cr_album.Songs)
                        {
                            var songMediaUrl = Path.GetFileName(cr_song.MediaUrl);
                            var foundDbSong = musicStoreContext.ms_Song.Where(s => s.MediaUrl == songMediaUrl).FirstOrDefault();
                            if (foundDbSong != null)
                            {
                                db_album.ms_Song.Add(foundDbSong);
                            }
                            else
                            {
                                ms_Song newSong = new ms_Song()
                                {
                                    Description = cr_song.Description,
                                    Lyrics = cr_song.Lyrics,
                                    MediaUrl = Path.GetFileName(cr_song.MediaUrl),
                                    Status = 1,
                                    Thumbnail = "",
                                    Title = cr_song.Title,
                                    Url = cr_song.Url,
                                    Duration = cr_song.Duration
                                };

                                var db_artist = db_album.ms_Artist.FirstOrDefault();
                                if (db_artist != null)
                                {
                                    newSong.ms_Artist.Add(db_artist);
                                }

                                db_album.ms_Song.Add(newSong);
                            }
                        }
                    }
                }

                musicStoreContext.SaveChanges();
                Console.WriteLine("End migrate song of album " + cr_album.Title);
            }
        }

        #endregion

        #region Crawl Music File
        private static void CrawlMusicFile()
        {
            int from = 0;
            
            bool result = false;
            while (!result)
            {
                Console.Clear();
                Console.WriteLine("Input the beginning song index");
                result = int.TryParse(Console.ReadLine(), out from);
            }

            //result = false;
            //while (!result)
            //{
            //    Console.WriteLine("Input the end song index");
            //    Console.Clear();
            //    result = int.TryParse(Console.ReadLine(), out to);
            //}

            CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities();
            List<Song> songs = crawlDataContext.Songs.ToList();
            for (int i = from; i < songs.Count; i++)
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
            MusicStoreEntities musicStoreContext = new MusicStoreEntities();
            List<ms_Song> songs = musicStoreContext.ms_Song.ToList();
            for (int i = 0; i < songs.Count; i++)
            {
                var mp3FullPath = Path.Combine(folderSongPath, Path.GetFileName(songs[i].MediaUrl));
                Console.WriteLine(mp3FullPath);
                int songId = songs[i].Id;
                var songModel = musicStoreContext.ms_Song.Where(s => s.Id == songId).FirstOrDefault();
                if (File.Exists(mp3FullPath))
                {
                    try
                    {
                        Mp3FileReader reader = new Mp3FileReader(mp3FullPath);
                        TimeSpan duration = reader.TotalTime;
                        float ratebit = reader.Id3v1Tag != null ? reader.Id3v1Tag.Length : 128;

                        Console.WriteLine(duration.TotalSeconds);
                        songModel.Duration = duration.TotalSeconds;
                        songModel.Quality = ratebit;
                        musicStoreContext.SaveChanges();
                    }
                    catch(Exception ex)
                    {

                    }
                }
            }
        }
        #endregion

        #region Crawl genres
        public static void CrawlGenres()
        {
            ScrapingBrowser Browser = new ScrapingBrowser();
            Browser.AllowAutoRedirect = true; // Browser has settings you can access in setup
            Browser.AllowMetaRedirect = true;
            Browser.Encoding = Encoding.UTF8;
            WebPage PageResult = Browser.NavigateToPage(new Uri("http://www.cristiana.fm/"));
            var listGenreElements = PageResult.Html.CssSelect("#main .wdoc.center nav>#nav>#mnav>ul>li").ToList();
            List<GenreData> genres = new List<GenreData>();
            if (listGenreElements!=null && listGenreElements.Count > 0)
            {

                foreach (var item in listGenreElements)
                {
                    var newGenre = new GenreData();
                    newGenre.Name = item.InnerText.Trim();
                    newGenre.Url = item.CssSelect("a").First().GetAttributeValue("href");
                    newGenre.Slug = newGenre.Url.Replace("/g/", "").TrimEnd('/');
                    genres.Add(newGenre);
                }
            }

            string linkGenreTemplate = "http://www.cristiana.fm/ajax/artist?t=1&siteId=2da0afc6-f506-4964-985b-36261ab4fdd0&genreSlug={0}&top=1000&page=1";
            Console.WriteLine("Total genre " + genres.Count);
            using (CrawlDatabaseEntities crawlDataContext = new CrawlDatabaseEntities())
            {
                foreach (var item in genres)
                {
                    Console.WriteLine("Begin genre: " + item.Name);
                    string link = string.Format(linkGenreTemplate, item.Slug);
                    var artists = GetArtistOfGenre(Browser, link);
                    var listArtistEntity = new List<Artist>();
                    if (artists != null)
                    {
                        foreach (var artist in artists)
                        {
                            var foundArtist = crawlDataContext.Artists.Where(a => a.Name == artist.artist).FirstOrDefault();
                            if (foundArtist != null)
                            {
                                listArtistEntity.Add(foundArtist);
                            }
                            else
                            {
                                Artist newArtist = new Artist()
                                {
                                    Name = artist.artist,
                                    Slug = artist.slug,
                                    Thumbnail = artist.image
                                };
                                listArtistEntity.Add(newArtist);
                            }
                        }
                    }

                    Genre newGenre = new Genre()
                    {
                        Name = item.Name,
                        Description = string.Empty,
                        Status = 1,
                        Url = item.Url,
                        Slug = item.Slug,
                        Artists = listArtistEntity
                    };
                    var foundGenre = crawlDataContext.Genres.Where(g => g.Name == newGenre.Name).FirstOrDefault();
                    if (foundGenre == null)
                    {
                        crawlDataContext.Genres.Add(newGenre);
                        crawlDataContext.SaveChanges();

                        //for (int i = 0; i < listArtistEntity.Count; i++)
                        //{
                        //    var artistName = listArtistEntity[i].Name;
                        //    var artistEntity = crawlDataContext.Artists.Where(a => a.Name == artistName).FirstOrDefault();
                        //    if (artistEntity != null)
                        //    {
                        //        newGenre.Artists.Add(artistEntity);
                        //    }
                        //}

                        //crawlDataContext.SaveChanges();
                    }
                    Console.WriteLine("End genre " + item.Name);
                }

            }
        }

        public static List<genre_artist> GetArtistOfGenre(ScrapingBrowser Browser, string link)
        {
            WebPage PageResult = Browser.NavigateToPage(new Uri(link));
            var result = new List<genre_artist>();
            GetArtistsOfGenreResponse response = JsonConvert.DeserializeObject<GetArtistsOfGenreResponse>(PageResult.ToString());
            try
            {
                if (response.code == 0 && response.data != null && response.data.Count > 0)
                {
                    return response.data;
                }
                else
                {
                    return Enumerable.Empty<genre_artist>().ToList<genre_artist>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on Get Artist Of Genre: {0}", ex.Message);
                return Enumerable.Empty<genre_artist>().ToList<genre_artist>();
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

    public class GenreData
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Slug { get; set; }
    }

    public class GetArtistsOfGenreResponse
    {
        public int code { get; set; }
        public string message { get; set; }
        public List<genre_artist> data { get; set; }
    }

    public class genre_artist
    {
        public string artist { get; set; }
        public string slug { get; set; }
        public string image { get; set; }
    }

    public class Collection_Data
    {
        public string Title { get; set; }
        public string Url { get; set; }
    }
}

#endregion

