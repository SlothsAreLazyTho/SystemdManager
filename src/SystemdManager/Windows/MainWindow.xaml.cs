﻿using AdonisUI.Controls;

using Renci.SshNet;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using SystemdManager.Objects;
using SystemdManager.Services;
using SystemdManager.ViewModels;

namespace SystemdManager.Windows;

public partial class MainWindow : AdonisWindow
{

    private readonly ServerViewModel _serverView;

    public MainWindow()
    {
        var applicationView = ApplicationService.ApplicationView;
        _serverView = applicationView.ServerView;
        DataContext = _serverView;

        InitializeComponent();
    }

    private async void OpenConnection()
    {
        try
        {
            OpenConnectionButton.IsEnabled = false;
            var selected = _serverView.SelectedServer;
            await Task.Factory.StartNew(async () =>
            {
                if (!ValidateServerInformation(selected))
                {
                    return;
                }

                var connectedServer = new ConnectedServer(selected);
                try
                {
                    connectedServer.ConnectAsync();
                }
                catch (Exception e)
                {
                    ShowConnectionFailedDialog(e);
                    return;
                }

                await OpenServerWindowAsync(connectedServer);
            });
        }
        catch(Exception e)
        {
            ShowConnectionFailedDialog(e);
        }
        finally
        {
            OpenConnectionButton.IsEnabled = true;
        }
    }

    private async Task OpenServerWindowAsync(ConnectedServer server)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            _serverView.ConnectedServer = server;

            var serverWindow = new ServerWindow();
            await serverWindow.LoadServices();

            serverWindow.Show();
            Close();
        });
    }

    private void ServerListItem_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenConnection();
    }

    private async void TestConnectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        var server = _serverView.SelectedServer;
        if (!ValidateServerInformation(server))
        {
            return;
        }

        TestConnectionButton.IsEnabled = false;
        try
        {
            await Task.Factory.StartNew(() =>
            {
                try
                {
                    var sshClient = new SshClient(server.Host, 22, server.User, server.Password);
                    sshClient.Connect();
                    if (sshClient.IsConnected)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show( "Successfully connected to the host.", "Connected", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });
                    }
                }
                catch (Exception ex)
                {
                    ShowConnectionFailedDialog(ex);
                }
            });
        }
        catch
        {
            // ignored
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private async void CreateButton_OnClick(object sender, RoutedEventArgs e)
    {
        var server = Server.CreateDefault();
        _serverView.Servers.Add(server);
        _serverView.SelectedServer = server;

        SelectedServerNameTextBox.Focus();
        await Task.Factory.StartNew(() => _serverView.SaveServersAsync());
    }

    private async void DeleteServerButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedServer = _serverView.SelectedServer;
        var result = MessageBox.Show(
            $"Are you sure you want to delete this server?\n\nName: {selectedServer.Name}\nHost: {selectedServer.Host}",
            "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _serverView.Servers.Remove(selectedServer);
        _serverView.SelectedServer = _serverView.Servers.FirstOrDefault();
        await Task.Factory.StartNew(() => _serverView.SaveServersAsync());
    }

    private void OpenButton_OnClick(object sender, RoutedEventArgs e) => OpenConnection();

    private void DataGrid_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        UpdatePasswordBox();
    }

    private void SelectedServerPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _serverView.SelectedServer.Password = SelectedServerPasswordBox.Password;
    }

    private void ServerList_OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdatePasswordBox();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        await Task.Factory.StartNew(() => _serverView.SaveServersAsync());
    }

    private void UpdatePasswordBox()
    {
        if (_serverView.SelectedServer == null)
        {
            return;
        }

        SelectedServerPasswordBox.Password = _serverView.SelectedServer.Password;
    }

    // TODO: Check if exception is a connection exception, if not handle it in an other method
    private void ShowConnectionFailedDialog(Exception exception = null)
    {
        Dispatcher.Invoke(() =>
        {
            var message = exception?.Message ?? "Unknown";
            MessageBox.Show($"Reason: {message}", "Failed to connect",
                MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private static bool ValidateServerInformation(Server server, bool showMessageBox = true)
    {
        if (server.Host == null ||
            server.Password == null ||
            server.User == null)
        {
            if (showMessageBox)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Please make sure you entered the host, username and password.", "Invalid input",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            return false;
        }

        return true;
    }

}
