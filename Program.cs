using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace BoxesPyDesktop
{
    static class Program
    {
        private static Process pythonProcess;
        private static string appDir;
        private static int port;
        private static NotifyIcon trayIcon;
        private static ContextMenuStrip trayMenu;
        private static string appUrl;
        private static Mutex singleInstanceMutex;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            appDir = AppDomain.CurrentDomain.BaseDirectory;

            bool createdNew;
            singleInstanceMutex = new Mutex(true, "BoxesPy_Desktop_Studio_SingleInstance_Mutex", out createdNew);

            port = FindFreePort(18950);
            appUrl = "http://127.0.0.1:" + port + "/Menu";

            if (!StartPythonBackend())
            {
                return;
            }

            // Sunucunun ayağa kalkmasını bekle (en fazla 4 saniye)
            WaitForServerReady("http://127.0.0.1:" + port + "/Menu", 4000);

            InitializeTrayIcon();
            LaunchAppWindow(appUrl);

            Application.ApplicationExit += (s, e) => StopPythonBackend();
            Application.Run();

            StopPythonBackend();
        }

        private static bool StartPythonBackend()
        {
            string pyExe = Path.Combine(appDir, @"python\python.exe");
            string serverScript = Path.Combine(appDir, @"boxes\scripts\boxesserver.py");
            string staticDir = Path.Combine(appDir, "static");

            if (!File.Exists(pyExe) || !File.Exists(serverScript))
            {
                MessageBox.Show(
                    "Boxes.py motor dosyaları bulunamadı!\nAranan yol: " + pyExe,
                    "Boxes.py Studio Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = pyExe,
                    Arguments = string.Format("\"{0}\" --host 127.0.0.1 --port {1} --static_path \"{2}\" --no_reload", serverScript, port, staticDir),
                    WorkingDirectory = appDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                psi.EnvironmentVariables["PYTHONPATH"] = appDir;
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";

                pythonProcess = Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Boxes.py yerel sunucusu başlatılamadı:\n" + ex.Message,
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return false;
            }
        }

        private static void WaitForServerReady(string url, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = 400;
                    using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                    {
                        if (res.StatusCode == HttpStatusCode.OK)
                            return;
                    }
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
        }

        private static void StopPythonBackend()
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }
            }
            catch { }

            try
            {
                if (pythonProcess != null && !pythonProcess.HasExited)
                {
                    pythonProcess.Kill();
                    pythonProcess.Dispose();
                    pythonProcess = null;
                }
            }
            catch { }
        }

        private static void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();

            ToolStripMenuItem itemOpenMenu = new ToolStripMenuItem("Boxes.py Stüdyo Penceresini Aç (Menü)", null, (s, e) => LaunchAppWindow("http://127.0.0.1:" + port + "/Menu"));
            itemOpenMenu.Font = new Font(itemOpenMenu.Font, FontStyle.Bold);
            trayMenu.Items.Add(itemOpenMenu);

            ToolStripMenuItem itemOpenGallery = new ToolStripMenuItem("Görsel Kutu Galerisini Aç (Gallery)", null, (s, e) => LaunchAppWindow("http://127.0.0.1:" + port + "/Gallery"));
            trayMenu.Items.Add(itemOpenGallery);

            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemBrowser = new ToolStripMenuItem("Varsayılan Tarayıcıda Aç", null, (s, e) => {
                try { Process.Start(new ProcessStartInfo(appUrl) { UseShellExecute = true }); } catch { }
            });
            trayMenu.Items.Add(itemBrowser);

            ToolStripMenuItem itemFolder = new ToolStripMenuItem("Uygulama Klasörünü Aç", null, (s, e) => {
                try { Process.Start(new ProcessStartInfo("explorer.exe", appDir)); } catch { }
            });
            trayMenu.Items.Add(itemFolder);

            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemExit = new ToolStripMenuItem("Çıkış (Boxes.py Kapat)", null, (s, e) => {
                StopPythonBackend();
                Application.Exit();
            });
            trayMenu.Items.Add(itemExit);

            trayIcon = new NotifyIcon
            {
                Text = "Boxes.py Lazer Kesim & Kutu Stüdyosu (Çalışıyor)",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            try
            {
                string iconPath = Path.Combine(appDir, "app.ico");
                if (File.Exists(iconPath))
                {
                    trayIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch
            {
                trayIcon.Icon = SystemIcons.Application;
            }

            trayIcon.DoubleClick += (s, e) => LaunchAppWindow(appUrl);
        }

        private static int FindFreePort(int preferredPort)
        {
            try
            {
                TcpListener test = new TcpListener(IPAddress.Loopback, preferredPort);
                test.Start();
                test.Stop();
                return preferredPort;
            }
            catch
            {
                TcpListener l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                int p = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return p;
            }
        }

        private static void LaunchAppWindow(string url)
        {
            string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            if (!File.Exists(edgePath))
            {
                edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
            }

            string chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe");
            if (!File.Exists(chromePath))
            {
                chromePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe");
            }

            string targetBrowser = null;
            if (File.Exists(edgePath)) targetBrowser = edgePath;
            else if (File.Exists(chromePath)) targetBrowser = chromePath;

            string userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BoxesPy_Studio_Data");

            ProcessStartInfo psi = new ProcessStartInfo();
            if (targetBrowser != null)
            {
                psi.FileName = targetBrowser;
                psi.Arguments = string.Format("--app=\"{0}\" --window-size=1440,920 --user-data-dir=\"{1}\"", url, userDataDir);
                psi.UseShellExecute = false;
            }
            else
            {
                psi.FileName = url;
                psi.UseShellExecute = true;
            }

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uygulama penceresi açılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
