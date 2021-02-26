﻿using System;
using GalaSoft.MvvmLight;
using MonoTorrent;
using System.Collections.ObjectModel;
using System.Linq;
using ByteSizeLib;
using MonoTorrent.Client;

namespace TiTorrent.Core.Models
{
    public class PivotModel : ObservableObject
    {
        #region PublicProp

        // Информация
        // => Торрент
        public string ActiveTime { get; set; }
        public ByteSize DownloadSize { get; set; }
        public ByteSize DownloadSpeed { get; set; }

        public string LeftTime { get; set; }
        public ByteSize UploadSize { get; set; }
        public ByteSize UploadSpeed { get; set; }

        public int ConnectionCount { get; set; }
        public int SeedsCount { get; set; }
        public int PeersCount { get; set; }

        // => Дополнительно
        public ByteSize TotalSize { get; set; }
        public string DateAdded { get; set; }
        public string Hash { get; set; }
        public string SavePath { get; set; }
        public string Comment { get; set; }

        public int PieceCount { get; set; }
        public string DateCompletion { get; set; }

        public string CreateIn { get; set; }
        public string DateCreate { get; set; }


        // Файлы
        public ObservableCollection<ITorrentFileInfo> TorrentFiles { get; set; } = new();

        #endregion

        public PivotModel(TorrentManager manager)
        {
            // Информация
            // => Торрент
            ActiveTime = (DateTime.Now - manager.StartTime).ToString(@"hh\:mm\:ss");
            DownloadSize = ByteSize.FromBytes(manager.Monitor.DataBytesDownloaded);
            DownloadSpeed = ByteSize.FromBytes(manager.Monitor.DownloadSpeed);

            LeftTime = ActiveTime;
            UploadSize = ByteSize.FromBytes(manager.Monitor.DataBytesUploaded);
            UploadSpeed = ByteSize.FromBytes(manager.Monitor.UploadSpeed);

            ConnectionCount = manager.OpenConnections;
            SeedsCount = manager.Peers.Seeds;
            PeersCount = manager.Peers.Available;

            // => Дополнительно
            TotalSize = ByteSize.FromBytes(manager.Torrent.Size);
            DateAdded = manager.Torrent.CreationDate.ToString("g");
            Hash = manager.InfoHash.ToHex();
            SavePath = manager.SavePath;
            Comment = manager.Torrent.Comment;

            PieceCount = manager.PieceManager.CurrentRequestCountAsync().Result;
            DateCompletion = DateTime.Today.ToString("g");

            CreateIn = manager.Torrent.CreatedBy;
            DateCreate = manager.Torrent.CreationDate.ToString("g");

            // Файлы
            TorrentFiles.Clear();
            manager.Files.ToList().ForEach(info => TorrentFiles.Add(info));
        }

        public void UpdateProp(TorrentManager manager)
        {
            // Информация
            // => Торрент
            ActiveTime = (DateTime.Now - manager.StartTime).ToString(@"hh\:mm\:ss");
            DownloadSize = ByteSize.FromBytes(manager.Monitor.DataBytesDownloaded);
            DownloadSpeed = ByteSize.FromBytes(manager.Monitor.DownloadSpeed);

            LeftTime = ActiveTime;
            UploadSize = ByteSize.FromBytes(manager.Monitor.DataBytesUploaded);
            UploadSpeed = ByteSize.FromBytes(manager.Monitor.UploadSpeed);

            ConnectionCount = manager.OpenConnections;
            SeedsCount = manager.Peers.Seeds;
            PeersCount = manager.Peers.Available;

            // => Дополнительно
            TotalSize = ByteSize.FromBytes(manager.Torrent.Size);
            DateAdded = manager.Torrent.CreationDate.ToString("g");
            Hash = manager.InfoHash.ToHex();
            SavePath = manager.SavePath;
            Comment = manager.Torrent.Comment;

            PieceCount = manager.PieceManager.CurrentRequestCountAsync().Result;
            DateCompletion = DateTime.Today.ToString("g");

            CreateIn = manager.Torrent.CreatedBy;
            DateCreate = manager.Torrent.CreationDate.ToString("g");

            // Файлы
            TorrentFiles.Clear();
            manager.Files.ToList().ForEach(info => TorrentFiles.Add(info));
        }
    }
}
