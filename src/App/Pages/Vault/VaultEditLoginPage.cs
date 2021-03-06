﻿using System;
using System.Collections.Generic;
using System.Linq;
using Acr.UserDialogs;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Resources;
using Plugin.Connectivity.Abstractions;
using Xamarin.Forms;
using XLabs.Ioc;

namespace Bit.App.Pages
{
    public class VaultEditLoginPage : ExtendedContentPage
    {
        private readonly string _loginId;
        private readonly ILoginService _loginService;
        private readonly IFolderService _folderService;
        private readonly IUserDialogs _userDialogs;
        private readonly IConnectivity _connectivity;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;

        public VaultEditLoginPage(string loginId)
        {
            _loginId = loginId;
            _loginService = Resolver.Resolve<ILoginService>();
            _folderService = Resolver.Resolve<IFolderService>();
            _userDialogs = Resolver.Resolve<IUserDialogs>();
            _connectivity = Resolver.Resolve<IConnectivity>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();

            Init();
        }

        public FormEntryCell PasswordCell { get; private set; }
        public FormEntryCell UsernameCell { get; private set; }
        public FormEntryCell UriCell { get; private set; }
        public FormEntryCell NameCell { get; private set; }
        public FormEditorCell NotesCell { get; private set; }
        public FormPickerCell FolderCell { get; private set; }
        public ExtendedTextCell GenerateCell { get; private set; }
        public ExtendedTextCell DeleteCell { get; private set; }

        private void Init()
        {
            var login = _loginService.GetByIdAsync(_loginId).GetAwaiter().GetResult();
            if(login == null)
            {
                // TODO: handle error. navigate back? should never happen...
                return;
            }

            NotesCell = new FormEditorCell(height: 90);
            NotesCell.Editor.Text = login.Notes?.Decrypt();

            PasswordCell = new FormEntryCell(AppResources.Password, isPassword: true, nextElement: NotesCell.Editor,
                useButton: true);
            PasswordCell.Entry.Text = login.Password?.Decrypt();
            PasswordCell.Button.Image = "eye";
            PasswordCell.Entry.DisableAutocapitalize = true;
            PasswordCell.Entry.Autocorrect = false;
            PasswordCell.Entry.FontFamily = Device.OnPlatform(iOS: "Courier", Android: "monospace", WinPhone: "Courier");

            UsernameCell = new FormEntryCell(AppResources.Username, nextElement: PasswordCell.Entry);
            UsernameCell.Entry.Text = login.Username?.Decrypt();
            UsernameCell.Entry.DisableAutocapitalize = true;
            UsernameCell.Entry.Autocorrect = false;

            UriCell = new FormEntryCell(AppResources.URI, Keyboard.Url, nextElement: UsernameCell.Entry);
            UriCell.Entry.Text = login.Uri?.Decrypt();
            NameCell = new FormEntryCell(AppResources.Name, nextElement: UriCell.Entry);
            NameCell.Entry.Text = login.Name?.Decrypt();

            GenerateCell = new ExtendedTextCell
            {
                Text = AppResources.GeneratePassword,
                ShowDisclousure = true
            };

            var folderOptions = new List<string> { AppResources.FolderNone };
            var folders = _folderService.GetAllAsync().GetAwaiter().GetResult()
                .OrderBy(f => f.Name?.Decrypt()).ToList();
            int selectedIndex = 0;
            int i = 0;
            foreach(var folder in folders)
            {
                i++;
                if(folder.Id == login.FolderId)
                {
                    selectedIndex = i;
                }

                folderOptions.Add(folder.Name.Decrypt());
            }
            FolderCell = new FormPickerCell(AppResources.Folder, folderOptions.ToArray());
            FolderCell.Picker.SelectedIndex = selectedIndex;

            var favoriteCell = new ExtendedSwitchCell
            {
                Text = AppResources.Favorite,
                On = login.Favorite
            };

            DeleteCell = new ExtendedTextCell { Text = AppResources.Delete, TextColor = Color.Red };

            var table = new ExtendedTableView
            {
                Intent = TableIntent.Settings,
                EnableScrolling = true,
                HasUnevenRows = true,
                Root = new TableRoot
                {
                    new TableSection(AppResources.LoginInformation)
                    {
                        NameCell,
                        UriCell,
                        UsernameCell,
                        PasswordCell,
                        GenerateCell
                    },
                    new TableSection
                    {
                        FolderCell,
                        favoriteCell
                    },
                    new TableSection(AppResources.Notes)
                    {
                        NotesCell
                    },
                    new TableSection
                    {
                        DeleteCell
                    }
                }
            };

            if(Device.OS == TargetPlatform.iOS)
            {
                table.RowHeight = -1;
                table.EstimatedRowHeight = 70;
            }
            else if(Device.OS == TargetPlatform.Android)
            {
                PasswordCell.Button.WidthRequest = 40;
            }

            var saveToolBarItem = new ToolbarItem(AppResources.Save, null, async () =>
            {
                if(!_connectivity.IsConnected)
                {
                    AlertNoConnection();
                    return;
                }

                if(string.IsNullOrWhiteSpace(NameCell.Entry.Text))
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired,
                        AppResources.Name), AppResources.Ok);
                    return;
                }

                login.Uri = UriCell.Entry.Text?.Encrypt();
                login.Name = NameCell.Entry.Text?.Encrypt();
                login.Username = UsernameCell.Entry.Text?.Encrypt();
                login.Password = PasswordCell.Entry.Text?.Encrypt();
                login.Notes = NotesCell.Editor.Text?.Encrypt();
                login.Favorite = favoriteCell.On;

                if(FolderCell.Picker.SelectedIndex > 0)
                {
                    login.FolderId = folders.ElementAt(FolderCell.Picker.SelectedIndex - 1).Id;
                }
                else
                {
                    login.FolderId = null;
                }

                _userDialogs.ShowLoading(AppResources.Saving, MaskType.Black);
                var saveTask = await _loginService.SaveAsync(login);

                _userDialogs.HideLoading();

                if(saveTask.Succeeded)
                {
                    await Navigation.PopForDeviceAsync();
                    _userDialogs.Toast(AppResources.LoginUpdated);
                    _googleAnalyticsService.TrackAppEvent("EditeLogin");
                }
                else if(saveTask.Errors.Count() > 0)
                {
                    await _userDialogs.AlertAsync(saveTask.Errors.First().Message, AppResources.AnErrorHasOccurred);
                }
                else
                {
                    await _userDialogs.AlertAsync(AppResources.AnErrorHasOccurred);
                }
            }, ToolbarItemOrder.Default, 0);

            Title = AppResources.EditLogin;
            Content = table;
            ToolbarItems.Add(saveToolBarItem);
            if(Device.OS == TargetPlatform.iOS)
            {
                ToolbarItems.Add(new DismissModalToolBarItem(this, AppResources.Cancel));
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if(!_connectivity.IsConnected)
            {
                AlertNoConnection();
            }

            PasswordCell.InitEvents();
            UsernameCell.InitEvents();
            UriCell.InitEvents();
            NameCell.InitEvents();
            NotesCell.InitEvents();
            FolderCell.InitEvents();
            PasswordCell.Button.Clicked += PasswordButton_Clicked;
            GenerateCell.Tapped += GenerateCell_Tapped;
            DeleteCell.Tapped += DeleteCell_Tapped;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            PasswordCell.Dispose();
            UsernameCell.Dispose();
            UriCell.Dispose();
            NameCell.Dispose();
            NotesCell.Dispose();
            FolderCell.Dispose();
            PasswordCell.Button.Clicked -= PasswordButton_Clicked;
            GenerateCell.Tapped -= GenerateCell_Tapped;
            DeleteCell.Tapped -= DeleteCell_Tapped;
        }

        private void PasswordButton_Clicked(object sender, EventArgs e)
        {
            PasswordCell.Entry.InvokeToggleIsPassword();
            PasswordCell.Button.Image = "eye" + (!PasswordCell.Entry.IsPasswordFromToggled ? "_slash" : string.Empty);
        }

        private async void GenerateCell_Tapped(object sender, EventArgs e)
        {
            if(!string.IsNullOrWhiteSpace(PasswordCell.Entry.Text)
                && !await _userDialogs.ConfirmAsync(AppResources.PasswordOverrideAlert, null, AppResources.Yes, AppResources.No))
            {
                return;
            }

            var page = new ToolsPasswordGeneratorPage((password) =>
            {
                PasswordCell.Entry.Text = password;
                _userDialogs.Toast(AppResources.PasswordGenerated);
            });
            await Navigation.PushForDeviceAsync(page);
        }

        private async void DeleteCell_Tapped(object sender, EventArgs e)
        {
            if(!_connectivity.IsConnected)
            {
                AlertNoConnection();
                return;
            }

            if(!await _userDialogs.ConfirmAsync(AppResources.DoYouReallyWantToDelete, null, AppResources.Yes, AppResources.No))
            {
                return;
            }

            _userDialogs.ShowLoading(AppResources.Deleting, MaskType.Black);
            var deleteTask = await _loginService.DeleteAsync(_loginId);
            _userDialogs.HideLoading();

            if(deleteTask.Succeeded)
            {
                await Navigation.PopForDeviceAsync();
                _userDialogs.Toast(AppResources.LoginDeleted);
                _googleAnalyticsService.TrackAppEvent("DeletedLogin");
            }
            else if(deleteTask.Errors.Count() > 0)
            {
                await _userDialogs.AlertAsync(deleteTask.Errors.First().Message, AppResources.AnErrorHasOccurred);
            }
            else
            {
                await _userDialogs.AlertAsync(AppResources.AnErrorHasOccurred);
            }
        }

        private void AlertNoConnection()
        {
            DisplayAlert(AppResources.InternetConnectionRequiredTitle, AppResources.InternetConnectionRequiredMessage,
                AppResources.Ok);
        }
    }
}
