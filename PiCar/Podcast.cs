using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Net.Http;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Windows.Storage;
using Windows.Networking.BackgroundTransfer;

namespace PiCar
{
    class Episode
    {
        public string podcastName;
        public string name;
        public string url;
        public string status = null;
        public DateTime pubDate;

        public async Task DownloadAsync()
        {
            Uri source = new Uri(url);
            var localFolder = ApplicationData.Current.LocalFolder;
            
            StorageFile destinationFile = await localFolder.CreateFileAsync(
                name + ".mp3", CreationCollisionOption.GenerateUniqueName);

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(source, destinationFile);
            await download.StartAsync();
            while (download.Progress.Status != BackgroundTransferStatus.Completed )
            {
            }
            var existingFile = await localFolder.TryGetItemAsync("PodcastDB.db");
            var conString = "Data Source=" + existingFile.Path + ";";
            var cmdString = "UPDATE Episodes " +
                "SET Status = 'Downloaded' " +
                "WHERE Title = '" + name + "'";
            SqliteConnection cn = new SqliteConnection(conString);
            SqliteCommand cmd = new SqliteCommand(cmdString, cn);
            cn.Open();
            cmd.ExecuteNonQuery();
            cn.Close();
        }
    }
    class Podcast
    {
        public int key;
        public string url;
        public string name;
        public List<Episode> episodesList;

        public async Task GetEpisodeListAsync()
        {
            episodesList = new List<Episode>();
            var httpClient = new HttpClient();
            var httpResponse = await httpClient.GetAsync(url);
            httpResponse.EnsureSuccessStatusCode();
            var httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
            TextReader reader = new StringReader(httpResponseBody);

            XDocument rssFeed = XDocument.Load(reader);
            foreach (var item in rssFeed.Descendants("item"))
            {
                var curEpisode = new Episode();
                curEpisode.name = item.Element("title").Value;
                if (!DateTime.TryParse(item.Element("pubDate").Value, out curEpisode.pubDate))
                    curEpisode.pubDate = Convert.ToDateTime("1984-01-19");
                curEpisode.url = item.Element("enclosure").Attribute("url").Value;
                curEpisode.podcastName = name;
                episodesList.Add(curEpisode);
            }
            await GetEpisodeHistory();
        }
        private async Task GetEpisodeHistory()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var existingFile = await localFolder.TryGetItemAsync("PodcastDB.db");
            var conString = "Data Source=" + existingFile.Path + ";";
            var cmdString = "SELECT * FROM Episodes";
            SqliteConnection cn = new SqliteConnection(conString);
            SqliteCommand cmd = new SqliteCommand(cmdString, cn);
            var episodeStatus = new List<Episode>();
            cn.Open();
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var curEpisode = new Episode();
                curEpisode.podcastName = name;
                curEpisode.url = reader["URL"].ToString();
                curEpisode.status = reader["Status"].ToString();
                curEpisode.name = reader["Title"].ToString();
                episodeStatus.Add(curEpisode);
            }
            cn.Close();
            foreach (var item in episodesList)
            {
                item.status = episodeStatus.Where(x => x.url == item.url)
                    .Select(x => x.status)
                    .FirstOrDefault();
            }
            var needsUpdateList = episodesList.Where(x => x.status == null).ToList();
            episodesList = episodesList.Where(x => x.status != "Deleted").ToList();
            Task.Run(()=> UpdateDB(needsUpdateList));
        }

        private async void UpdateDB(List<Episode> updatedList)
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var existingFile = await localFolder.TryGetItemAsync("PodcastDB.db");

            var conString = "Data Source=" + existingFile.Path + ";";
            string cmdString = "";
            SqliteConnection cn = new SqliteConnection(conString);
            SqliteCommand cmd = new SqliteCommand(cmdString, cn);
            cn.Open();
            foreach (var item in updatedList)
            {
                cmd.CommandText = String.Format("INSERT INTO Episodes (PodcastKey, Title, URL, Status) " +
                    "SELECT PodcastKey, '{0}', '{1}', 'New' "+
                    "FROM PodcastMaster " +
                    "WHERE PodcastName = '{2}'", item.name.Replace("'",""), item.url, name);
                cmd.ExecuteNonQuery();
            }
            cn.Close();
        }
    }
}
