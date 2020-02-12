﻿using System.Text;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

using SakuraLauncher.Data;
using SakuraLauncher.Helper;

namespace SakuraLauncher
{
    /// <summary>
    /// CreateTunnelWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTunnelWindow : Window
    {
        public Prop<string> ProxyName { get; set; } = new Prop<string>("");
        public Prop<bool> Creating { get; set; } = new Prop<bool>();
        public Prop<int> RemotePort { get; set; } = new Prop<int>();

        public Prop<bool> Loading { get; set; } = new Prop<bool>();

        public ObservableCollection<ServerData> Servers => MainWindow.Instance.Servers;
        public ObservableCollection<ListeningData> Listening { get; set; } = new ObservableCollection<ListeningData>();

        public CreateTunnelWindow()
        {
            InitializeComponent();

            DataContext = this;

            LoadListeningList();
        }

        public void LoadListeningList()
        {
            Loading.Value = true;
            Listening.Clear();
            var process = Process.Start(new ProcessStartInfo("netstat.exe", "-ano")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            process.OutputDataReceived += (s, e) =>
            {
                if(e.Data != null)
                {
                    var tokens = Regex.Split(e.Data.Trim(), "\\s+");
                    if(tokens.Length > 4 && tokens[3] == "LISTENING" && (tokens[0] == "TCP" || tokens[0] == "UDP"))
                    {
                        var pname = "[拒绝访问]";
                        try
                        {
                            pname = Process.GetProcessById(int.Parse(tokens[4])).ProcessName;
                        }
                        catch { }
                        var spliter = tokens[1].LastIndexOf(':');
                        Dispatcher.Invoke(() => Listening.Add(new ListeningData()
                        {
                            Protocol = tokens[0],
                            Address = tokens[1].Substring(0, spliter),
                            Port = tokens[1].Substring(spliter + 1),
                            PID = tokens[4],
                            ProcessName = pname
                        }));
                    }
                }
            };
            process.BeginOutputReadLine();
            ThreadPool.QueueUserWorkItem(s =>
            {
                try
                {
                    process.WaitForExit(3000);
                    process.Kill();
                }
                catch { }
                Loading.Value = false;
            });
        }

        private void ButtonCreate_Click(object sender, RoutedEventArgs e)
        {
            if(Creating.Value)
            {
                return;
            }
            if(!(listening.SelectedItem is ListeningData l))
            {
                MessageBox.Show("请先选择一个监听项目再创建隧道", "Oops", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if(ProxyName.Value.Length < 3)
            {
                MessageBox.Show("隧道名称太短", "Oops", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if(!(server.SelectedItem is ServerData s))
            {
                MessageBox.Show("请选择穿透服务器", "Oops", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Creating.Value = true;
            App.ApiRequest("adduserproxy", new StringBuilder("proxytype=").Append(l.Protocol)
                .Append("&localaddr=").Append(l.Address == "0.0.0.0" ? "127.0.0.1" : l.Address)
                .Append("&localport=").Append(l.Port)
                .Append("&proxyname=").Append(ProxyName.Value)
                .Append("&nodeid=").Append(s.ID)
                .Append("&remoteport=").Append(RemotePort.Value).ToString()).ContinueWith(t =>
            {
                Creating.Value = false;
                var json = t.Result;
                if(json == null)
                {
                    return;
                }
                if(MessageBox.Show(json["msg"] + "\n\n是否继续创建?", "创建成功", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProxyName.Value = "";
                        server.SelectedItem = null;
                        listening.SelectedItem = null;
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        Close();
                    });
                }
            });
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            if(!Loading.Value)
            {
                LoadListeningList();
            }
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            MainWindow.Instance.LoggedIn.Value = false;
            MainWindow.Instance.TryLogin();
        }
    }
}
