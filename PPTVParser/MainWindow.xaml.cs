﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace PPTVParser
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public static bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            X509Chain verify = new X509Chain();
            verify.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            verify.ChainPolicy.RevocationMode = X509RevocationMode.Online; //revocation checking  
            verify.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            if (verify.Build(new X509Certificate2(certificate)))
            {
                return true;
            }
            return false;
        }

        //获取网页源码  
        private static string GetWebSource(String url, string headers = "", int TimeOut = 60000)
        {
            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
            string htmlCode = string.Empty;
            try
            {
                HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                webRequest.Method = "GET";
                webRequest.UserAgent = "Mozilla/4.0";
                webRequest.Headers.Add("Accept-Encoding", "gzip, deflate");
                webRequest.Timeout = TimeOut;  //设置超时  
                webRequest.KeepAlive = false;
                //添加headers  
                if (headers != "")
                {
                    foreach (string att in headers.Split('|'))
                    {
                        try
                        {
                            if (att.Split(':')[0].ToLower() == "referer")
                                webRequest.Referer = att.Substring(att.IndexOf(":") + 1);
                            else if (att.Split(':')[0].ToLower() == "user-agent")
                                webRequest.UserAgent = att.Substring(att.IndexOf(":") + 1);
                            else if (att.Split(':')[0].ToLower() == "range")
                                webRequest.AddRange(Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[0], Convert.ToInt32(att.Substring(att.IndexOf(":") + 1).Split('-')[1])));
                            else if (att.Split(':')[0].ToLower() == "accept")
                                webRequest.Accept = att.Substring(att.IndexOf(":") + 1);
                            else
                                webRequest.Headers.Add(att);
                        }
                        catch (Exception e)
                        {

                        }
                    }
                }
                HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
                if (webResponse.ContentEncoding != null
                    && webResponse.ContentEncoding.ToLower() == "gzip") //如果使用了GZip则先解压  
                {
                    using (Stream streamReceive = webResponse.GetResponseStream())
                    {
                        using (var zipStream =
                            new System.IO.Compression.GZipStream(streamReceive, System.IO.Compression.CompressionMode.Decompress))
                        {
                            using (StreamReader sr = new StreamReader(zipStream, Encoding.UTF8))
                            {
                                htmlCode = sr.ReadToEnd();
                            }
                        }
                    }
                }
                else
                {
                    using (Stream streamReceive = webResponse.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(streamReceive, Encoding.UTF8))
                        {
                            htmlCode = sr.ReadToEnd();
                        }
                    }
                }

                if (webResponse != null)
                {
                    webResponse.Close();
                }
                if (webRequest != null)
                {
                    webRequest.Abort();
                }
            }
            catch (Exception e)  //捕获所有异常  
            {

            }

            return htmlCode;
        }
        
        //获取有效文件名
        public static string GetValidFileName(string input, string re = ".")
        {
            string title = input;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(invalidChar.ToString(), re);
            }
            return title;
        }

        private static string GetVidFromUrl(string input)
        {
            string webSource = GetWebSource(input);
            Regex vidRex = new Regex("id\":(.*),\"id_encode", RegexOptions.Compiled);
            return vidRex.Match(webSource).Groups[1].Value;
        }

        Dictionary<string, string> results = new Dictionary<string, string>();
        private void Button_Go_Click(object sender, RoutedEventArgs e)
        {
            TextBox_Result.Text = "";
            string input = TextBox_Input.Text.Trim();
            results.Clear();
            int dfn = 4;
            if (Radio_0.IsChecked == true) dfn = 0;
            if (Radio_1.IsChecked == true) dfn = 1;
            if (Radio_2.IsChecked == true) dfn = 2;
            if (Radio_3.IsChecked == true) dfn = 3;
            if (Radio_4.IsChecked == true) dfn = 4;
            ThreadPool.QueueUserWorkItem((object state) =>
            {
                try
                {
                    if (input.Contains("/page/")) //批量
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            string pid = GetVidFromUrl(input.Split('\n')[0]);
                            string webSource = GetWebSource(
                                $"https://apis.web.pptv.com/show/videoList?from=web&version=1.0.0&format=jsonp&pid={pid}&cat_id=2&vt=22&tdsourcetag=s_pctim_aiomsg",
                                "Cookie:PUID=bf2a0e4f2cf24517c294-a68a65ed8c50;ppi=302c393939;"
                                ).Replace("{", "\r\n{");
                            //MessageBox.Show(webSource);
                            Regex regex = new Regex("\"url\":\"(.*)\",\"capture.*isTrailer\":(.*),\"olt\"");
                            MatchCollection matches = regex.Matches(webSource);
                            if (matches.Count == 0)
                                return;
                            Button_Go.IsEnabled = false;
                            TextBox_Input.Text = "";
                            StringBuilder sb = new StringBuilder();
                            foreach (Match match in matches)
                            {
                                if (match.Groups[2].Value == "false")
                                    sb.Append(match.Groups[1].Value.Replace("\\/", "/") + "\r\n");
                            }
                            TextBox_Input.Text = sb.ToString().Trim();
                            Button_Go.IsEnabled = true;
                        }));
                        return;
                    }

                    foreach (var item in input.Split('\n'))
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Button_Go.IsEnabled = false;
                            Button_Go.Content = "解析中";
                        }));
                        string vid = GetVidFromUrl(item);
                        if (vid == "")
                            continue;
                        string api = $"https://play.api.pptv.com/boxplay.api?platform=launcher&type=tv.android&id={vid}";
                        string webSource = new WebClient() { Encoding = Encoding.UTF8 }.DownloadString(api);
                        string fname = "";
                        string rid = "";
                        string sh = "";
                        string key = "";
                        Regex fnameRex = new Regex("nm=\"(.*)\".vip", RegexOptions.Compiled);
                        fname = fnameRex.Match(webSource).Groups[1].Value;
                        Regex fileRex = new Regex("<dt ft=\"" + dfn + "\"[\\s\\S]*<\\/dt>", RegexOptions.Compiled);
                        webSource = fileRex.Match(webSource).Value;
                        fileRex = new Regex("rid=\"(.*mp4)", RegexOptions.Compiled);
                        rid = fileRex.Match(webSource).Groups[1].Value;
                        fileRex = new Regex("sh>(.*)<", RegexOptions.Compiled);
                        sh = fileRex.Match(webSource).Groups[1].Value;
                        fileRex = new Regex("<key.*>(.*)</key>");
                        key = fileRex.Match(webSource).Groups[1].Value;
                        if (sh == "" || rid == "" || key == "")
                            continue;
                        string videourl = $"https://{sh}/w/{rid}?platform=launcher&type=tv.android&k={key}";
                        results.Add(fname, videourl);
                        this.Dispatcher.BeginInvoke(new Action(() => TextBox_Result.AppendText(videourl + "\r\n")));
                    }
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Button_Go.IsEnabled = true;
                        Button_Go.Content = "开始解析";
                        TextBox_Result.Text = TextBox_Result.Text.Trim();
                    }));
                }
                catch (Exception)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Button_Go.IsEnabled = true;
                        Button_Go.Content = "开始解析";
                        TextBox_Result.Text = TextBox_Result.Text.Trim();
                        MessageBox.Show("遇到了错误");
                    }));
                }
            }, null);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (results.Count == 0)
                return;
            var saveFileDialog1 = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "批处理文件|*.bat",
                Title = "保存解析结果",
                ValidateNames = true,
                AddExtension = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
            };

            if (saveFileDialog1.ShowDialog() == true) 
            {
                string headers = "User-Agent:Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("::Created by PPTVParser");
                sb.AppendLine("::https://github.com/nilaoda/PPTVParser/releases\r\n");
                int i = 0;
                foreach (var item in results)
                {
                    sb.AppendLine($"TITLE [{++i}/{results.Count}] - {item.Key}");
                    sb.AppendLine($"aria2c --max-connection-per-server=8 --file-allocation=none --header=\"{headers}\" -o \"{GetValidFileName(item.Key)}.mp4\" \"{item.Value}\"");
                }
                File.WriteAllText(saveFileDialog1.FileName, sb.ToString().Replace("%", "%%"), Encoding.Default);
                MessageBox.Show("已导出");
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            if (results.Count == 0)
                return;
            string dir = "";
            string headers = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog();  //选择文件夹
            openFileDialog.Description = "选择一个目录，视频将会下载到此处";
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dir = openFileDialog.SelectedPath;
            }

            if (dir == "")
                return;

            foreach (var item in results)
            {
                new IDManLib.CIDMLinkTransmitterClass().SendLinkToIDM2(
                    item.Value, //URL
                    "", //Referer
                    "", //Cookies
                    "", //Data
                    "", //Username
                    "", //Userpassword
                    dir, //LocalPath
                    GetValidFileName(item.Key)+".mp4",  //LocalFileName
                    2, //Flag
                    headers,
                    null
                    );
            }

            try
            {
                Process.Start(@"C:\Program Files (x86)\Internet Download Manager\IDMan.exe");
            }
            catch (Exception){; }
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            if (results.Count == 0)
                return;
            var saveFileDialog1 = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件|*.txt",
                Title = "保存解析结果",
                ValidateNames = true,
                AddExtension = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
            };

            if (saveFileDialog1.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in results)
                    sb.AppendLine(item.Value);
                File.WriteAllText(saveFileDialog1.FileName, sb.ToString().Trim());
                MessageBox.Show("已导出");
            }
        }

        private void TextBox_Input_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TextBox_Input == null || Button_Go == null)
                return;
            if (TextBox_Input.Text.Split('\n')[0].Contains("/page/")) //批量
            {
                Button_Go.Content = "获取全集地址";
            }
            else
            {
                Button_Go.Content = "开始解析";
            }
        }
    }
}
