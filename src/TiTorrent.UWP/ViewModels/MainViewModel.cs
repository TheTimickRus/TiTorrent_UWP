﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TiTorrent.UWP.Helpers;
using TiTorrent.UWP.Helpers.Extensions;
using TiTorrent.UWP.Models;
using TiTorrent.UWP.Services.Dialogs;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace TiTorrent.UWP.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region PublicProps

        public ObservableCollection<ListViewItemModel> TorrentsCollection { get; set; } = new();
        public ListViewItemModel SelectedTorrent { get; set; }

        public PivotModel MainPivotModel { get; set; }

        #endregion

        #region PrivateProps

        private bool _isAlreadyRun;

        private static readonly string TorrentsFolderPath = Path.Combine($"{ApplicationData.Current.LocalFolder.Path}", "State", "Torrents");
        private static readonly string DhtNodesPath = $"{ApplicationData.Current.LocalFolder.Path}\\State\\DhtNodes.dat";
        private static readonly string FastResumePath = $"{ApplicationData.Current.LocalFolder.Path}\\State\\FastResume.dat";

        private ClientEngine _clientEngine = new();

        #endregion

        #region Constructors

        // Инициализация
        public async Task InitializeAsync()
        {
            if (_isAlreadyRun is false)
            {
                // Загрузка сохранений
                await Task.Run(async () =>
                {
                    _clientEngine = await LoadState();

                    //Thread.Sleep(2000);

                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        _clientEngine?.Torrents?
                            .ToList()
                            .ForEach(manager =>
                            {
                                manager.StartAsync();
                                TorrentsCollection.Add(new ListViewItemModel(manager));
                            });
                    });
                });

                // Запускаем таймер обновления
                MainTimer();
            }

            _isAlreadyRun = true;

            // Успех!
            await Task.CompletedTask;
        }

        public MainViewModel()
        {
            // События
            _clientEngine.CriticalException += async (_, args) =>
            {
                await new MessageDialog(args.Exception.Message, "Error!").ShowAsync();
                Log.Instance.Error(args.Exception, "Произошла ошибка в движке!");
            };

            AppDomain.CurrentDomain.UnhandledException += async (_, args) =>
            {
                await new MessageDialog(args.ExceptionObject.ToString(), "Error!").ShowAsync();
                Log.Instance.Error(args.ExceptionObject.ToString(), "Произошла ошибка!");
            };
        }

        #endregion

        #region Commands

        public ICommand BAddCommand => new RelayCommand(async () =>
        {
            // Торрент
            TorrentManager manager = null;

            try
            {
                // Выбираем торрент
                manager = await Singleton<AddDisplayService>.Instance.ShowAsync();

                // Если торрент не выбран - выходим
                if (manager is null)
                    return;

                // Проверяем, на повтор
                if (_clientEngine.Torrents.FirstOrDefault(m => m.Equals(manager)) is not null)
                    throw new Exception("Торрент уже существует!");

                // Добавляем в коллекцию и регистрируем в движке
                TorrentsCollection.Add(new ListViewItemModel(manager));
                await _clientEngine.Register(manager);

                // Начинаем загрузку
                await manager.StartAsync();

                // Отслеживаем смену State
                manager.TorrentStateChanged += ManagerOnTorrentStateChanged;

                // Логгируемся
                Log.Instance.Information($"Торрент {manager} успешно добавлен!");
            }
            catch (Exception ex)
            {
                // Показываем ошибку
                await new MessageDialog(ex.Message, "Error!").ShowAsync();

                // Логгируемся
                Log.Instance.Error(ex, $"Ошибка при добавлении торрента {manager}!");
            }
        });

        public ICommand BDeleteCommand => new RelayCommand(async () =>
        {
            // Торрент
            TorrentManager manager = null;

            try
            {
                // Менеджер
                manager = _clientEngine.Torrents.First(tm => tm.InfoHash.ToHex().Equals(SelectedTorrent.Hash));

                var isFilesDelete = await Singleton<DeleteDisplayService>.Instance.ShowAsync(manager);

                if (isFilesDelete is null)
                {
                    return;
                }

                // Удаляем торрент
                await manager.StopAsync();
                await _clientEngine.Unregister(manager);
                TorrentsCollection.Remove(SelectedTorrent);

                // Удаляем загруженные файлы
                if (isFilesDelete.Value)
                {
                    var path = Path.Combine(manager.SavePath, manager.Torrent.Name);

                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                // Логгируемся
                Log.Instance.Information($"Торрент {manager} был удален!");

            }
            catch (Exception ex)
            {
                // Показываем ошибку
                await new MessageDialog(ex.Message, "Error!").ShowAsync();

                // Логгируемся
                Log.Instance.Error(ex, $"Ошибка при добавлении торрента {manager}!");
            }
        }, () => SelectedTorrent != null);

        public ICommand BStartCommand => new RelayCommand(async () =>
        {
            // Торрент
            TorrentManager manager = null;

            try
            {
                manager = _clientEngine.Torrents.First(tm => tm.InfoHash.ToHex().Equals(SelectedTorrent.Hash));
                await manager.StartAsync();

                // Логгируемся
                Log.Instance.Information($"Торрент {manager} был запущен!");
            }
            catch (Exception ex)
            {
                // Показываем ошибку
                await new MessageDialog(ex.Message, "Error!").ShowAsync();

                // Логгируемся
                Log.Instance.Error(ex, $"Ошибка при запуске торрента {manager}!");
            }
        }, () => SelectedTorrent != null);

        public ICommand BPauseCommand => new RelayCommand(async () =>
        {
            try
            {
                var manager = _clientEngine.Torrents.First(tm => tm.InfoHash.ToHex().Equals(SelectedTorrent.Hash));
                await manager.PauseAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message, "Error!").ShowAsync();
            }
        }, () => SelectedTorrent != null);

        public ICommand BStopCommand => new RelayCommand(async () =>
        {
            try
            {
                var manager = _clientEngine.Torrents.First(tm => tm.InfoHash.ToHex().Equals(SelectedTorrent.Hash));
                await manager.StopAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message, "Error!").ShowAsync();
            }
        }, () => SelectedTorrent != null);

        public ICommand FOpenFolderCommand => new RelayCommand(async () =>
        {
            try
            {
                Process.Start("cmd", "/c ping google.ru");
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message, "Error!").ShowAsync();
            }
        }, () => SelectedTorrent != null);

        #endregion

        #region Methods

        private async void ManagerOnTorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
        {
            if (sender is not TorrentManager manager)
                return;

            switch (e.OldState)
            {
                case TorrentState.Downloading:
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            var listViewItem = TorrentsCollection.FirstOrDefault(model => manager.InfoHash.ToHex().Equals(model.Hash));
                            if (listViewItem is not null)
                                ViewModelLocator.Current.DownloadViewModel.TorrentsCollection.Remove(listViewItem);
                        });
                    break;

                case TorrentState.Seeding:
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            var listViewItem = TorrentsCollection.FirstOrDefault(model => manager.InfoHash.ToHex().Equals(model.Hash));
                            if (listViewItem is not null)
                                ViewModelLocator.Current.UploadViewModel.TorrentsCollection.Remove(listViewItem);
                        });
                    break;
            }

            switch (e.NewState)
            {
                case TorrentState.Downloading:
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            var listViewItem = TorrentsCollection.FirstOrDefault(model => manager.InfoHash.ToHex().Equals(model.Hash));
                            if (listViewItem is not null && manager.Complete is false)
                                ViewModelLocator.Current.DownloadViewModel.TorrentsCollection.Add(listViewItem);
                        });
                    break;

                case TorrentState.Seeding:
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            var listViewItem = TorrentsCollection.FirstOrDefault(model => manager.InfoHash.ToHex().Equals(model.Hash));
                            if (listViewItem is not null && manager.Complete)
                                ViewModelLocator.Current.UploadViewModel.TorrentsCollection.Add(listViewItem);
                        });

                    Log.Instance.Information($"Торрент {manager.Torrent.Name} успешно загружен!");
                    break;


                case TorrentState.Error:
                    Log.Instance.Error(e.TorrentManager.Error.Exception, $"Произошла ошибка в ({e.TorrentManager})!");
                    break;
            }
        }
        private void MainTimer()
        {
            // Создаем Timer
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Start();

            // Событие
            timer.Tick += async (_, _) =>
            {
                await CoreApplication.GetCurrentView().Dispatcher.RunTaskAsync(
                    () =>
                    {
                        // Если нет торрентов - выходим
                        if (_clientEngine == null || _clientEngine.Torrents.Count == 0)
                        {
                            SelectedTorrent = null;
                            MainPivotModel = null;

                            return Task.CompletedTask;
                        }

                        // Обновляем данные в ListView
                        _clientEngine.Torrents.ToList().ForEach(manager => TorrentsCollection.First(model => model.Hash.Equals(manager.InfoHash.ToHex())).UpdateProp(manager));

                        // Обновляем данные в Pivot
                        if (SelectedTorrent != null)
                        {
                            var manager = _clientEngine.Torrents.First(m => m.InfoHash.ToHex().Equals(SelectedTorrent.Hash));

                            if (MainPivotModel != null && MainPivotModel.Hash == SelectedTorrent.Hash)
                                MainPivotModel.UpdateProp(manager);
                            else
                                MainPivotModel = new PivotModel(manager);
                        }
                        else
                        {
                            MainPivotModel = null;
                        }

                        return Task.CompletedTask;
                    });
            };
        }

        private static async Task<ClientEngine> LoadState()
        {
            var engine = new ClientEngine();
            
            if (File.Exists(FastResumePath))
            {
                var fastResume = BEncodedValue.Decode<BEncodedDictionary>(await File.ReadAllBytesAsync(FastResumePath));

                foreach (var file in Directory.GetFiles(TorrentsFolderPath))
                {
                    try
                    {
                        if (file.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                        {
                            var torrent = await Torrent.LoadAsync(file);

                            if (fastResume.ContainsKey(torrent.InfoHash.ToHex()))
                            {
                                var manager = new TorrentManager(torrent, $"{ApplicationData.Current.LocalFolder.Path}\\Downloads");

                                manager.LoadFastResume(new FastResume((BEncodedDictionary)fastResume[torrent.InfoHash.ToHex()]));
                                await engine.Register(manager);
                            }
                            else
                            {
                                File.Delete(file);
                            }
                        }
                        else
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Instance.Error(ex, "Ошибка при инициализации!");
                    }
                }
            }

            return engine;
        }

        public async Task SaveState()
        {
            try
            {
                var fastResume = new BEncodedDictionary();
                var torrents = _clientEngine.Torrents;

                await _clientEngine.StopAllAsync();

                torrents.ToList().ForEach(manager =>
                {
                    if (manager.HashChecked)
                        fastResume.Add(manager.Torrent.InfoHash.ToHex(), manager.SaveFastResume().Encode());
                });

                if (!Directory.Exists(Path.GetDirectoryName(DhtNodesPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DhtNodesPath));
                }

                await File.WriteAllBytesAsync(DhtNodesPath, await _clientEngine.DhtEngine.SaveNodesAsync());
                await File.WriteAllBytesAsync(FastResumePath, fastResume.Encode());
            }
            catch (Exception ex)
            {
                Log.Instance.Error(ex, ex.Message);
            }
            finally
            {
                _clientEngine.Dispose();
            }
        }

        #endregion
    }
}
