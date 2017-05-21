using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace PiCar
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeDB();

            this.InitializeComponent();
            
            var test = Task.Run(() => GetSubscribedListAsync()).Result;
            var listToDispaly = new List<Episode>();
            foreach (var item in test)
            {
                var tempList = Task.Run(() => ListEpisodesAsync(item.name, item.url, item.key)).Result;
                listToDispaly.AddRange(tempList);
            }

            lv_Episodes.ItemsSource = listToDispaly;
        }
        
        private async Task<List<Podcast>> GetSubscribedListAsync()
        {
            var subList = new List<Podcast>();
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var existingFile = await localFolder.TryGetItemAsync("PodcastDB.db");
            var conString = "Data Source=" + existingFile.Path + ";";
            var cmdString = "SELECT * FROM PodcastMaster";
            SqliteConnection cn = new SqliteConnection(conString);
            SqliteCommand cmd = new SqliteCommand(cmdString, cn);
            cn.Open();
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var curPodcast = new Podcast();
                curPodcast.key = Convert.ToInt32(reader["PodcastKey"]);
                curPodcast.name = reader["PodcastName"].ToString();
                curPodcast.url = reader["PodcastURL"].ToString();
                subList.Add(curPodcast);
            }
            cn.Close();
            return subList;
        }
        private async Task<List<Episode>> ListEpisodesAsync(string name, string url, int key)
        {
            Podcast pc = new Podcast();
            pc.key = key;
            pc.name = name;
            pc.url = url;
            await pc.GetEpisodeListAsync();
            return pc.episodesList;
        }

        private async void InitializeDB()
        {
            //var dbFile = ApplicationData.Current.LocalFolder.TryGetItemAsync("\\Assets\\PodcastDB.db") as StorageFile;

            ////await ApplicationData.Current.LocalFolder.TryGetItemAsync("PodcastDB.db") as StorageFile;

            //if (null == dbFile)
            //{
            //    var localFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            //    string dbFileName = @"Assets\PodcastDB.db";
            //    var originalDbFile = await localFolder.GetFileAsync(dbFileName);
            //    if (null != originalDbFile)
            //        dbFile = await originalDbFile.CopyAsync(localFolder, "PodcastDB.db", NameCollisionOption.ReplaceExisting);
            //}
            // Check if the file is already present in the Local folder
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var existingFile = await localFolder.TryGetItemAsync("PodcastDB.db");

            if (existingFile == null)
            {
                // Copy the file from the install folder to the local folder
                var folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
                var file = await folder.GetFileAsync("PodcastDB.db");
                if (file != null)
                {
                    await file.CopyAsync(localFolder, "PodcastDB.db", Windows.Storage.NameCollisionOption.FailIfExists);
                }
            }
        }

        private void _btn_Select_Click(object sender, RoutedEventArgs e)
        {
            var selectedButton = e.OriginalSource as Button;
            selectedButton.Content = "Downloading";
            var selectedEpisode = selectedButton.DataContext as Episode;
            Task.Run(()=> selectedEpisode.DownloadAsync()).Wait();
            selectedButton.Content = "Play";
        }

        private void _btn_Select_Loaded(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var btn = sender as Button;
            if (btn.DataContext == null)
                return;

            var episode = btn.DataContext as Episode;
            
            switch (episode.status)
            {
                case "New":
                    btn.Content = "Download";
                    break;
                case "Downloaded":
                    btn.Content = "Play";
                    break;
                case "Played":
                    btn.Content = "Delete";
                    break;
                default:
                    break;
            }
        }
    }
}
