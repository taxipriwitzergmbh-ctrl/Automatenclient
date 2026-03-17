using SuE.TaMi;
using SuE.Tools;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using System.Net; // added for AutoUpdater
using System.Reflection; // added for AutoUpdater
using System.Text; // added for AutoUpdater
using System.Threading.Tasks; // NEW for release notes

namespace TaMi_Automatenclient
{
    internal static class Program
    {
        //WinAPI Importe
        [DllImport("kernel32.dll", EntryPoint = "ProcessIdToSessionId", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        public static extern bool ProcessIdToSessionId([In] int pid, [Out] out int tSSession);

#region "TaMi Client Variablen"

        private static Mutex mTaMiClientMutex;
        private static Mutex mAppMutex;

        public static string mSessionId;

        private static string mAppPath;
        private static string mAppDataPath = "";

        public static string tamiServerHost ;
        public static int tamiServerPort ;
        public static string localTaMi_Database_Server = "\\SuE";
        public static string localTaMi_Database_Name = "SuE-TaMi";
        public static string localTaMi_Database_User = "TaMiCli";
        public static string localTaMi_Database_Pass = "tami";

        private static TaMiClient mMainTaMiClient;
        public static TaMiClient MainTaMiClient
        {
            get { return mMainTaMiClient; }
        }

        // Reconnect-Timer: ruft alle 5 Sekunden Connect auf, falls nicht verbunden
        private static System.Threading.Timer mReconnectTimer;
        private const int ReconnectIntervalMs = 5000;
        // Schutz, damit keine überlappenden Connect-Aufrufe stattfinden
        private static int _reconnectRunning = 0;

        #endregion

        private static Icon mAppIcon;
        public static Icon AppIcon
        {
            get { return mAppIcon; }
        }


        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                //Unhandled Exception Handler
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => HandleException(e.Exception, "ThreadException");
                AppDomain.CurrentDomain.UnhandledException += (s, e) => HandleException(e.ExceptionObject as Exception, "UnhandledException");

                //LoadAutomatenNamenFromIni
                if (AppSettings.AutomatenNamen.Count == 0)
                    AppSettings.LoadAutomatenNamenFromIni();

                //App Icon laden
                try
                {
                    mAppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch 
                {
                    mAppIcon = null;
                }

                // Prüfe Update vom Webspace und ggf. neu starten
                try { AutoUpdater.CheckAndPromptAtStartup(); } catch { }

                #region "TaMi Client Initialisierung"

                //SessionId (Username + SessionId)
                ProcessIdToSessionId(Process.GetCurrentProcess().Id, out int sessionId);
                mSessionId = WindowsIdentity.GetCurrent().Name + ":" + sessionId;

                // mAppDataPath über den Parameter /appdatapath= übernehmen, sofern angegeben
                for (int i = 0; i < args.Length; i++)
                {
                    //Debug.WriteLine("Arg[{0}] = [{1}]", i, args[i]);
                    if (args[i].StartsWith("/appdatapath="))
                    {
                        mAppDataPath = args[i].Substring(13);
                    }
                }

                if (mAppDataPath.Length > 0)
                {
                    if (System.IO.Directory.Exists(mAppDataPath) == false)
                    {
                        MessageBox.Show(null, "Der Pfad '" + mAppDataPath + "' existiert nicht oder es kann nicht darauf zugegriffen werden.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 2;
                    }

                    if (mAppDataPath.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()) == false)
                        mAppDataPath += System.IO.Path.DirectorySeparatorChar;
                }



                //Mutex App
                mAppMutex = new Mutex(false, "SuE-TaMi Map");

                //Mutex "Global\\Mutex:SuE-TaMiClient.SQL"
                MutexSecurity mutexSecurity = new MutexSecurity();
                mutexSecurity.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                                                                MutexRights.Delete | MutexRights.Synchronize | MutexRights.Modify, AccessControlType.Allow));

                mTaMiClientMutex = new Mutex(false, "Global\\Mutex:SuE-TaMiClient.SQL", out bool createdNew, mutexSecurity);

                //AppPath (Pfad der .exe) und AppDataPath (C:\\ProgramData\\SuE-Software\\SuE-TaMi Client SQL)
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

                mAppPath = System.IO.Path.GetDirectoryName(assembly.Location) + System.IO.Path.DirectorySeparatorChar;

                if (mAppDataPath.Length == 0)
                {
                    mAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\SuE-Software\\SuE-TaMi Client SQL\\";

                    if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(mAppDataPath)) == false)
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(mAppDataPath));
                    }
                }


                //Lade TaMiClient.ini
                LoadSettingsFromTaMiClientINI(mAppDataPath + "TaMi Client.ini");

                //TaMi Client initialisieren
                mMainTaMiClient = new TaMiClient("Lokal", tamiServerHost, tamiServerPort, mSessionId, localTaMi_Database_Server, localTaMi_Database_Name, localTaMi_Database_User, localTaMi_Database_Pass);
                mMainTaMiClient.Enabled = true; // Muss true sein, damit Messages gesendet werden
                mMainTaMiClient.Connect(-1, string.Empty);

                // Reconnect-Timer initialisieren und starten
                mReconnectTimer = new System.Threading.Timer(ReconnectTimer_Callback, null, ReconnectIntervalMs, ReconnectIntervalMs);

                #endregion


                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // Login erzwingen: Nur bei erfolgreichem Login Programm starten
                /*
                var user = LoginForm.ShowLogin();
                if (user == null)
                {
                    // Abgebrochen oder fehlgeschlagen -> Anwendung beenden
                    goto ExitApp;
                }
                */

                Application.Run(new MenueForm());
            }
            catch (Exception ex)
            {
                HandleException(ex, "MainCatch");
                return ex.HResult;
            }

    

            //ReconnectTimer stoppen und freigeben
            try { mReconnectTimer?.Dispose(); mReconnectTimer = null; } catch { }

            //TaMi Client Verbindungen trennen
            if (MainTaMiClient.State != ConnectionState.DISCONNECTED)
                MainTaMiClient.Disconnect();

            //AppMutex und TaMiClientMutex freigeben
            if (mAppMutex != null)
            {
                mAppMutex.Close();
                mAppMutex = null;
            }

            //TaMiClientMutex freigeben
            if (mTaMiClientMutex != null)
            {
                mTaMiClientMutex.Close();
                mTaMiClientMutex = null;
            }

            return 0;
        }

        //ReconnectTimer Callback
        private static void ReconnectTimer_Callback(object state)
        {
            // Schutz gegen überlappende Aufrufe
            if (Interlocked.Exchange(ref _reconnectRunning, 1) == 1)
                return;

            try
            {
                if (mMainTaMiClient != null && mMainTaMiClient.Enabled)
                {
                    if (mMainTaMiClient.State == ConnectionState.DISCONNECTED)
                    {
                        mMainTaMiClient.Connect(-1, string.Empty);
                    }
                }
            }
            catch
            {
                // Ignorieren
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectRunning, 0);
            }
        }


        private static void HandleException(Exception ex, string source)
        {
            try
            {
                if (ex == null) return;
                var time = DateTime.Now;
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SuE-Software", "TaMi-Automatenclient", "logs");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, $"error_{time:yyyyMMdd_HHmmss}_{source}.log");
                File.WriteAllText(logFile, ex.ToString());
                MessageBox.Show($"Fehler beim Start ({source}):\n\n{ex.Message}\n\nDetails: {logFile}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Fallback, wenn Logging selbst scheitert
                MessageBox.Show($"Fehler: {ex}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //AppPath
        public static string AppPath
        {
            get { return Program.mAppPath; }
        }

        //AppDataPath
        public static string AppDataPath
        {
            get { return Program.mAppDataPath; }
        }

        //LoadSettingsFromTaMiClientINI
        private static void LoadSettingsFromTaMiClientINI(string path)
        {
            const string SERVER = "server";
            const string DATABASE = "database";

            string[] readText = System.IO.File.ReadAllLines(path);

            string section = "";
            string szLine;

            foreach (string line in readText)
            {
                szLine = line.Trim();
                //Console.WriteLine(s);

                //Prüfe Sektion "[NAME]"

                if (szLine.StartsWith("[") && szLine.EndsWith("]")) { section = szLine.Substring(1, szLine.Length - 2).ToLower(); }

                //[Server]
                if (section.Equals(SERVER))
                {
                    if (section.Equals(SERVER) && szLine.StartsWith("Host=")) { tamiServerHost = szLine.Substring(5); }
                    else if (section.Equals(SERVER) && szLine.StartsWith("Port=")) { tamiServerPort = Convert.ToInt32(szLine.Substring(5)); }
                }

                //[Database]
                else if (section.Equals(DATABASE))
                {
                    if (szLine.StartsWith("Server=")) { localTaMi_Database_Server = szLine.Substring(7); }
                    if (szLine.StartsWith("Database=")) { localTaMi_Database_Name = szLine.Substring(9); }
                    if (szLine.StartsWith("User=")) { localTaMi_Database_User = szLine.Substring(5); }
                    if (szLine.StartsWith("Pass=")) { localTaMi_Database_Pass = szLine.Substring(5); }
                }
            }
        }

        public static void ShowUpdateHints(Form owner)
        {
            try { Task.Run(() => ReleaseNotes.ShowReleaseNotesAsync(owner, forceShow: true)); } catch { }
        }

        public static void CheckForUpdateNow(Form owner)
        {
            try { Task.Run(() => AutoUpdater.CheckAndPromptAtStartup()); } catch { }
        }

        public static Task<bool?> CheckForUpdateAvailableAsync()
        {
            try { return AutoUpdater.CheckForUpdateAvailableAsync(); }
            catch { return Task.FromResult<bool?>(null); }
        }

    }

    internal static class ReleaseNotes
    {
        private const string ReleaseNotesUrl = "http://kassenautomat.priwitzer-dienstleistungsgmbh.de/Update_Automatenclient/releasenotes.txt";

        public static void CheckAndShowAtStartup(Form owner)
        {
            try { Task.Run(() => ShowReleaseNotesAsync(owner, forceShow: false)); } catch { }
        }

        public static async Task ShowReleaseNotesAsync(Form owner, bool forceShow)
        {
            try
            {
                var current = GetCurrentAppVersion();
                var last = ReadLastSeenVersion();

                bool shouldShow = forceShow;
                if (!shouldShow)
                {
                    if (last == null) shouldShow = true;
                    else if (current > last) shouldShow = true;
                }

                if (!shouldShow) return;

                var notes = await TryDownloadReleaseNotesAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(notes))
                    notes = "Keine Updatehinweise gefunden (releasenotes.txt).";

                ShowReleaseNotesOnUi(owner, notes);

                if (!forceShow)
                    WriteLastSeenVersion(current);
            }
            catch { }
        }

        private static Version GetCurrentAppVersion()
        {
            try { return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0); }
            catch { return new Version(0, 0, 0, 0); }
        }

        private static Version ReadLastSeenVersion()
        {
            try
            {
                var s = (AppSettings.ReadIniValue("APP", "LastSeenVersion", "") ?? string.Empty).Trim();
                Version v; return Version.TryParse(s, out v) ? v : null;
            }
            catch { return null; }
        }

        private static void WriteLastSeenVersion(Version v)
        {
            try
            {
                if (v == null) return;
                AppSettings.WriteIniValue("APP", "LastSeenVersion", v.ToString());
            }
            catch { }
        }

        private static async Task<string> TryDownloadReleaseNotesAsync()
        {
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Proxy = WebRequest.DefaultWebProxy;
                    wc.Encoding = Encoding.UTF8;
                    return await wc.DownloadStringTaskAsync(ReleaseNotesUrl).ConfigureAwait(false);
                }
            }
            catch { return null; }
        }

        private static void ShowReleaseNotesOnUi(Form owner, string notes)
        {
            try
            {
                Action show = () =>
                {
                    try
                    {
                        using (var dlg = new Form())
                        {
                            dlg.Text = "Update";
                            dlg.StartPosition = owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
                            dlg.Size = new Size(920, 680);
                            dlg.MinimizeBox = false;
                            dlg.MaximizeBox = false;
                            dlg.BackColor = Color.White;
                            try { dlg.Font = new Font("Segoe UI Variable", 10f); } catch { dlg.Font = new Font("Segoe UI", 10f); }

                            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(245, 248, 255) };
                            var lblTitle = new Label
                            {
                                AutoSize = false,
                                Dock = DockStyle.Fill,
                                TextAlign = ContentAlignment.MiddleLeft,
                                Text = "Update – Hinweise",
                                Padding = new Padding(20, 0, 20, 0),
                                Font = new Font(dlg.Font.FontFamily, 16f, FontStyle.Bold),
                                ForeColor = Color.FromArgb(33, 150, 243)
                            };
                            header.Controls.Add(lblTitle);

                            var lblStatus = new Label
                            {
                                AutoSize = false,
                                Dock = DockStyle.Right,
                                Width = 320,
                                TextAlign = ContentAlignment.MiddleRight,
                                Padding = new Padding(10, 0, 20, 0),
                                Font = new Font(dlg.Font.FontFamily, 10.5f, FontStyle.Bold),
                                ForeColor = Color.FromArgb(90, 90, 90),
                                Text = "Prüfe…"
                            };
                            header.Controls.Add(lblStatus);

                            var contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14), BackColor = Color.White };

                            var tb = new TextBox
                            {
                                Multiline = true,
                                ReadOnly = true,
                                ScrollBars = ScrollBars.Vertical,
                                Dock = DockStyle.Fill,
                                BorderStyle = BorderStyle.FixedSingle,
                                BackColor = Color.White,
                                ForeColor = Color.FromArgb(30, 30, 30),
                                Font = new Font("Consolas", 10f),
                                Text = notes ?? string.Empty
                            };
                            contentHost.Controls.Add(tb);

                            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White };

                            var btnUpdate = new Button
                            {
                                Text = "Update verfügbar",
                                Visible = false,
                                Width = 170,
                                Height = 40,
                                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                                FlatStyle = FlatStyle.Flat,
                                BackColor = Color.FromArgb(46, 125, 50),
                                ForeColor = Color.White,
                                Left = dlg.ClientSize.Width - 160 - 170 - 12,
                                Top = 12
                            };
                            btnUpdate.FlatAppearance.BorderSize = 0;
                            btnUpdate.Click += (s3, e3) =>
                            {
                                try { Program.CheckForUpdateNow(dlg); } catch { }
                            };
                            footer.Controls.Add(btnUpdate);

                            var btn = new Button
                            {
                                Text = "OK",
                                DialogResult = DialogResult.OK,
                                Width = 140,
                                Height = 40,
                                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                                FlatStyle = FlatStyle.Flat,
                                BackColor = Color.FromArgb(33, 150, 243),
                                ForeColor = Color.White,
                                Left = dlg.ClientSize.Width - 160,
                                Top = 12
                            };
                            btn.FlatAppearance.BorderSize = 0;
                            footer.Controls.Add(btn);

                            dlg.Controls.Add(contentHost);
                            dlg.Controls.Add(footer);
                            dlg.Controls.Add(header);
                            dlg.AcceptButton = btn;

                            dlg.Resize += (s2, e2) =>
                            {
                                try { btn.Left = dlg.ClientSize.Width - btn.Width - 20; } catch { }
                                try { btnUpdate.Left = btn.Left - btnUpdate.Width - 12; } catch { }
                            };
                            try { btn.Left = dlg.ClientSize.Width - btn.Width - 20; } catch { }
                            try { btnUpdate.Left = btn.Left - btnUpdate.Width - 12; } catch { }

                            try
                            {
                                Task.Run(async () =>
                                {
                                    var hasUpdate = await Program.CheckForUpdateAvailableAsync().ConfigureAwait(false);
                                    try
                                    {
                                        dlg.BeginInvoke((Action)(() =>
                                        {
                                            if (hasUpdate == true)
                                            {
                                                lblStatus.Text = "Update verfügbar";
                                                lblStatus.ForeColor = Color.FromArgb(46, 125, 50);
                                                btnUpdate.Visible = true;
                                            }
                                            else if (hasUpdate == false)
                                            {
                                                lblStatus.Text = "Sie sind aktuell – besser wird es heute nicht mehr.";
                                                lblStatus.ForeColor = Color.FromArgb(90, 90, 90);
                                                btnUpdate.Visible = false;
                                            }
                                            else
                                            {
                                                lblStatus.Text = "Prüfung nicht möglich";
                                                lblStatus.ForeColor = Color.FromArgb(229, 57, 53);
                                                btnUpdate.Visible = false;
                                            }
                                        }));
                                    }
                                    catch { }
                                });
                            }
                            catch { }

                            if (owner != null) dlg.ShowDialog(owner); else dlg.ShowDialog();
                        }
                    }
                    catch { }
                };

                if (owner != null && owner.InvokeRequired) owner.BeginInvoke(show);
                else show();
            }
            catch { }
        }
    }

    // Embedded AutoUpdater for TaMi Automatenclient (MSI/EXE) with UAC elevation
    internal static class AutoUpdater
    {
        // Setze diese URL auf die konkrete Datei (MSI oder Setup.exe) auf deinem Webspace,
        // z. B. "https://server/pfad/Setup1.msi" oder "https://server/pfad/setup.exe".
        private const string UpdateUrl = "http://kassenautomat.priwitzer-dienstleistungsgmbh.de/Update_Automatenclient/Automatenclient_Setup.msi";

        public static async Task<bool?> CheckForUpdateAvailableAsync()
        {
            try
            {
                var exeName = Path.GetFileName(Application.ExecutablePath) ?? string.Empty;
                if (string.IsNullOrEmpty(exeName)) return null;

                var currentVersion = GetCurrentVersion();
                var remoteUrl = BuildRemoteUrl(UpdateUrl, exeName);

                string tmpPath = null;
                try
                {
                    var ext = GuessExtensionFromUrl(remoteUrl);
                    tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ext);
                    using (var wc = new WebClient())
                    {
                        await wc.DownloadFileTaskAsync(remoteUrl, tmpPath).ConfigureAwait(false);
                    }
                }
                catch
                {
                    SafeDelete(tmpPath);
                    return null;
                }

                bool isMsi = IsMsiFile(tmpPath);
                Version remoteVersion = isMsi ? GetMsiProductVersion(tmpPath) : GetFileVersion(tmpPath);
                SafeDelete(tmpPath);
                if (remoteVersion == null) return null;
                return remoteVersion > currentVersion;
            }
            catch
            {
                return null;
            }
        }

        public static void CheckAndPromptAtStartup()
        {
            try
            {
                var exeName = Path.GetFileName(Application.ExecutablePath) ?? string.Empty;
                if (string.IsNullOrEmpty(exeName)) return;

                var currentVersion = GetCurrentVersion();
                var remoteUrl = BuildRemoteUrl(UpdateUrl, exeName);

                string tmpPath = null;
                try
                {
                    var ext = GuessExtensionFromUrl(remoteUrl);
                    tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ext);
                    using (var wc = new WebClient())
                    {
                        wc.DownloadFile(remoteUrl, tmpPath);
                    }
                }
                catch
                {
                    SafeDelete(tmpPath);
                    return;
                }

                bool isMsi = IsMsiFile(tmpPath);
                Version remoteVersion = isMsi ? GetMsiProductVersion(tmpPath) : GetFileVersion(tmpPath);
                if (remoteVersion == null || remoteVersion <= currentVersion)
                {
                    SafeDelete(tmpPath);
                    return;
                }

                var result = MessageBox.Show(
                    $"Es ist eine neue Version verfügbar (aktuell: {currentVersion}, neu: {remoteVersion}).\r\nJetzt aktualisieren?",
                    "Update verfügbar",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    SafeDelete(tmpPath);
                    return;
                }

                try
                {
                    if (isMsi)
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "msiexec.exe",
                            Arguments = "/i \"" + tmpPath + "\" /passive /norestart",
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                    }
                    else
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = tmpPath,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                    }
                }
                catch
                {
                    SafeDelete(tmpPath);
                    return;
                }

                try { Environment.Exit(0); } catch { Application.Exit(); }
            }
            catch
            {
            }
        }

        private static bool RequiresElevation(string appDir)
        {
            try
            {
                var t = Path.Combine(appDir, ".__updtest_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(t, "x");
                File.Delete(t);
                return false;
            }
            catch (UnauthorizedAccessException) { return true; }
            catch (System.Security.SecurityException) { return true; }
            catch { return false; }
        }

        private static Version GetCurrentVersion()
        {
            try { return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0); }
            catch { return new Version(0, 0, 0, 0); }
        }

        private static string BuildRemoteUrl(string baseOrFileUrl, string exeName)
        {
            if (string.IsNullOrWhiteSpace(baseOrFileUrl)) return string.Empty;
            var u = baseOrFileUrl.Trim();
            int lastSlash = u.LastIndexOf('/');
            int lastDot = u.LastIndexOf('.');
            bool looksLikeFile = (lastDot > lastSlash);
            if (looksLikeFile) return u;
            if (!u.EndsWith("/")) u += "/";
            return u + exeName;
        }

        private static Version GetFileVersion(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(info.FileVersion))
                {
                    Version v; if (Version.TryParse(info.FileVersion, out v)) return v;
                }
            }
            catch { }
            return null;
        }

        private static void SafeDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

        private static bool IsMsiFile(string path)
        {
            try { return string.Equals(Path.GetExtension(path), ".msi", StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }

        private static string GuessExtensionFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var ext = Path.GetExtension(uri.LocalPath);
                if (!string.IsNullOrEmpty(ext)) return ext;
            }
            catch { }
            return ".exe"; // Default
        }

        // Liest ProductVersion aus einer MSI-Datei über Windows Installer COM per Reflection
        private static Version GetMsiProductVersion(string msiPath)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WindowsInstaller.Installer");
                if (t == null) return null;
                var installer = Activator.CreateInstance(t);
                var db = t.InvokeMember("OpenDatabase", System.Reflection.BindingFlags.InvokeMethod, null, installer, new object[] { msiPath, 0 });
                var dbType = db.GetType();
                var view = dbType.InvokeMember("OpenView", System.Reflection.BindingFlags.InvokeMethod, null, db, new object[] { "SELECT `Value` FROM `Property` WHERE `Property`='ProductVersion'" });
                var viewType = view.GetType();
                viewType.InvokeMember("Execute", System.Reflection.BindingFlags.InvokeMethod, null, view, new object[] { null });
                var record = viewType.InvokeMember("Fetch", System.Reflection.BindingFlags.InvokeMethod, null, view, null);
                if (record == null) return null;
                var recType = record.GetType();
                var verStr = recType.InvokeMember("StringData", System.Reflection.BindingFlags.GetProperty, null, record, new object[] { 1 }) as string;
                Version v; if (Version.TryParse(verStr, out v)) return v;
            }
            catch { }
            return null;
        }
    }
}
