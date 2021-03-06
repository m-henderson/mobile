﻿using Bit.App.Models.Page;
using FFImageLoading.Forms;
using System;
using Xamarin.Forms;

namespace Bit.App.Controls
{
    public class VaultGroupingViewCell : ExtendedViewCell
    {
        public static readonly BindableProperty GroupingParameterProeprty = BindableProperty.Create(nameof(GroupingParameter),
            typeof(VaultListPageModel.Grouping), typeof(VaultGroupingViewCell), null);

        public VaultGroupingViewCell()
        {
            Icon = new CachedImage
            {
                WidthRequest = 20,
                HeightRequest = 20,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Source = "folder.png",
                Margin = new Thickness(0, 0, 10, 0)
            };

            Label = new Label
            {
                LineBreakMode = LineBreakMode.TailTruncation,
                FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Label)),
                HorizontalOptions = LayoutOptions.StartAndExpand
            };
            Label.SetBinding(Label.TextProperty, nameof(VaultListPageModel.Grouping.Name));

            CountLabel = new Label
            {
                LineBreakMode = LineBreakMode.NoWrap,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                Style = (Style)Application.Current.Resources["text-muted"],
                HorizontalOptions = LayoutOptions.End
            };
            CountLabel.SetBinding(Label.TextProperty, nameof(VaultListPageModel.Grouping.Count));

            var stackLayout = new StackLayout
            {
                Spacing = 0,
                Padding = new Thickness(16, 8),
                Children = { Icon, Label, CountLabel },
                Orientation = StackOrientation.Horizontal
            };

            if(Device.RuntimePlatform == Device.Android)
            {
                Label.TextColor = Color.Black;
            }

            View = stackLayout;
            BackgroundColor = Color.White;
            SetBinding(GroupingParameterProeprty, new Binding("."));
        }

        public VaultListPageModel.Grouping GroupingParameter
        {
            get => GetValue(GroupingParameterProeprty) as VaultListPageModel.Grouping;
            set { SetValue(GroupingParameterProeprty, value); }
        }
        public CachedImage Icon { get; private set; }
        public Label Label { get; private set; }
        public Label CountLabel { get; private set; }

        protected override void OnBindingContextChanged()
        {
            if(BindingContext is VaultListPageModel.Grouping grouping)
            {
                Icon.Source = grouping.Folder ? $"folder{(grouping.Id == null ? "_o" : string.Empty)}.png" : "cube.png";
            }

            base.OnBindingContextChanged();
        }
    }
}
