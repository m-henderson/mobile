﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Acr.UserDialogs;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Resources;
using Xamarin.Forms;
using XLabs.Ioc;
using Bit.App.Utilities;
using Plugin.Settings.Abstractions;
using Plugin.Connectivity.Abstractions;
using System.Collections.Generic;
using System.Threading;
using static Bit.App.Models.Page.VaultListPageModel;

namespace Bit.App.Pages
{
    public class VaultListGroupingsPage : ExtendedContentPage
    {
        private readonly IFolderService _folderService;
        private readonly ICollectionService _collectionService;
        private readonly ICipherService _cipherService;
        private readonly IUserDialogs _userDialogs;
        private readonly IConnectivity _connectivity;
        private readonly IDeviceActionService _deviceActionService;
        private readonly ISyncService _syncService;
        private readonly IPushNotificationService _pushNotification;
        private readonly IDeviceInfoService _deviceInfoService;
        private readonly ISettings _settings;
        private readonly IAppSettingsService _appSettingsService;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;
        private CancellationTokenSource _filterResultsCancellationTokenSource;

        public VaultListGroupingsPage()
            : base(true)
        {
            _folderService = Resolver.Resolve<IFolderService>();
            _collectionService = Resolver.Resolve<ICollectionService>();
            _cipherService = Resolver.Resolve<ICipherService>();
            _connectivity = Resolver.Resolve<IConnectivity>();
            _userDialogs = Resolver.Resolve<IUserDialogs>();
            _deviceActionService = Resolver.Resolve<IDeviceActionService>();
            _syncService = Resolver.Resolve<ISyncService>();
            _pushNotification = Resolver.Resolve<IPushNotificationService>();
            _deviceInfoService = Resolver.Resolve<IDeviceInfoService>();
            _settings = Resolver.Resolve<ISettings>();
            _appSettingsService = Resolver.Resolve<IAppSettingsService>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();

            Init();
        }

        public ExtendedObservableCollection<Section<Grouping>> PresentationSections { get; private set; }
            = new ExtendedObservableCollection<Section<Grouping>>();
        public ListView ListView { get; set; }
        public StackLayout NoDataStackLayout { get; set; }
        public ActivityIndicator LoadingIndicator { get; set; }
        private AddCipherToolBarItem AddCipherItem { get; set; }
        private SearchToolBarItem SearchItem { get; set; }

        private void Init()
        {
            SearchItem = new SearchToolBarItem(this);
            AddCipherItem = new AddCipherToolBarItem(this, null);
            ToolbarItems.Add(SearchItem);
            ToolbarItems.Add(AddCipherItem);

            ListView = new ListView(ListViewCachingStrategy.RecycleElement)
            {
                IsGroupingEnabled = true,
                ItemsSource = PresentationSections,
                HasUnevenRows = true,
                GroupHeaderTemplate = new DataTemplate(() => new SectionHeaderViewCell(
                    nameof(Section<Grouping>.Name), nameof(Section<Grouping>.Count), new Thickness(16, 12))),
                ItemTemplate = new DataTemplate(() => new VaultGroupingViewCell())
            };

            if(Device.RuntimePlatform == Device.iOS)
            {
                ListView.RowHeight = -1;
            }

            var noDataLabel = new Label
            {
                Text = AppResources.NoItems,
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                Style = (Style)Application.Current.Resources["text-muted"]
            };

            var addCipherButton = new ExtendedButton
            {
                Text = AppResources.AddAnItem,
                Command = new Command(() => Helpers.AddCipher(this, null)),
                Style = (Style)Application.Current.Resources["btn-primaryAccent"]
            };

            NoDataStackLayout = new StackLayout
            {
                Children = { noDataLabel, addCipherButton },
                VerticalOptions = LayoutOptions.CenterAndExpand,
                Padding = new Thickness(20, 0),
                Spacing = 20
            };

            LoadingIndicator = new ActivityIndicator
            {
                IsRunning = true,
                VerticalOptions = LayoutOptions.CenterAndExpand,
                HorizontalOptions = LayoutOptions.Center
            };

            Content = LoadingIndicator;
            Title = AppResources.MyVault;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            MessagingCenter.Subscribe<Application, bool>(Application.Current, "SyncCompleted", (sender, success) =>
            {
                if(success)
                {
                    _filterResultsCancellationTokenSource = FetchAndLoadVault();
                }
            });

            ListView.ItemSelected += GroupingSelected;
            AddCipherItem?.InitEvents();
            SearchItem?.InitEvents();

            _filterResultsCancellationTokenSource = FetchAndLoadVault();

            if(_connectivity.IsConnected && Device.RuntimePlatform == Device.iOS)
            {
                var pushPromptShow = _settings.GetValueOrDefault(Constants.PushInitialPromptShown, false);
                Action registerAction = () =>
                {
                    var lastPushRegistration =
                        _settings.GetValueOrDefault(Constants.PushLastRegistrationDate, DateTime.MinValue);
                    if(!pushPromptShow || DateTime.UtcNow - lastPushRegistration > TimeSpan.FromDays(1))
                    {
                        _pushNotification.Register();
                    }
                };

                if(!pushPromptShow)
                {
                    _settings.AddOrUpdateValue(Constants.PushInitialPromptShown, true);
                    _userDialogs.Alert(new AlertConfig
                    {
                        Message = AppResources.PushNotificationAlert,
                        Title = AppResources.EnableAutomaticSyncing,
                        OnAction = registerAction,
                        OkText = AppResources.OkGotIt
                    });
                }
                else
                {
                    // Check push registration once per day
                    registerAction();
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<Application, bool>(Application.Current, "SyncCompleted");

            ListView.ItemSelected -= GroupingSelected;
            AddCipherItem?.Dispose();
            SearchItem?.Dispose();
        }

        private CancellationTokenSource FetchAndLoadVault()
        {
            var cts = new CancellationTokenSource();
            _filterResultsCancellationTokenSource?.Cancel();

            Task.Run(async () =>
            {
                var sections = new List<Section<Grouping>>();
                var ciphers = await _cipherService.GetAllAsync();
                var collectionsDict = (await _collectionService.GetAllCipherAssociationsAsync())
                    .GroupBy(c => c.Item2).ToDictionary(g => g.Key, v => v.ToList());

                var folderCounts = new Dictionary<string, int> { ["none"] = 0 };
                foreach(var cipher in ciphers)
                {
                    if(cipher.FolderId != null)
                    {
                        if(!folderCounts.ContainsKey(cipher.FolderId))
                        {
                            folderCounts.Add(cipher.FolderId, 0);
                        }
                        folderCounts[cipher.FolderId]++;
                    }
                    else
                    {
                        folderCounts["none"]++;
                    }
                }

                var folders = await _folderService.GetAllAsync();
                var folderGroupings = folders?
                    .Select(f => new Grouping(f, folderCounts.ContainsKey(f.Id) ? folderCounts[f.Id] : 0))
                    .OrderBy(g => g.Name).ToList();
                folderGroupings.Add(new Grouping(AppResources.FolderNone, folderCounts["none"]));
                sections.Add(new Section<Grouping>(folderGroupings, AppResources.Folders));

                var collections = await _collectionService.GetAllAsync();
                var collectionGroupings = collections?
                    .Select(c => new Grouping(c,
                        collectionsDict.ContainsKey(c.Id) ? collectionsDict[c.Id].Count() : 0))
                   .OrderBy(g => g.Name).ToList();
                if(collectionGroupings?.Any() ?? false)
                {
                    sections.Add(new Section<Grouping>(collectionGroupings, AppResources.Collections));
                }

                Device.BeginInvokeOnMainThread(() =>
                {
                    PresentationSections.ResetWithRange(sections);

                    if(ciphers.Any() || folders.Any())
                    {
                        Content = ListView;
                    }
                    else if(_syncService.SyncInProgress)
                    {
                        Content = LoadingIndicator;
                    }
                    else
                    {
                        Content = NoDataStackLayout;
                    }
                });
            }, cts.Token);

            return cts;
        }

        private async void GroupingSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var grouping = e.SelectedItem as Grouping;
            if(grouping == null)
            {
                return;
            }

            Page page;
            if(grouping.Folder)
            {
                page = new VaultListCiphersPage(folder: true, folderId: grouping.Id, groupingName: grouping.Name);
            }
            else
            {
                page = new VaultListCiphersPage(collectionId: grouping.Id, groupingName: grouping.Name);
            }

            await Navigation.PushAsync(page);
            ((ListView)sender).SelectedItem = null;
        }

        private async void Search()
        {
            var page = new ExtendedNavigationPage(new VaultListCiphersPage());
            await Navigation.PushModalAsync(page);
        }

        private class SearchToolBarItem : ExtendedToolbarItem
        {
            public SearchToolBarItem(VaultListGroupingsPage page)
                : base(() => page.Search())
            {
                Text = AppResources.Search;
                Icon = "search.png";
            }
        }
    }
}
