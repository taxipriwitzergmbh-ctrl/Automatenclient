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

namespace TaMi_Kassenclient
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
        private static string mAppConfigFilename = "";

        public static string tamiServerHost = "localhost";
        public static int    tamiServerPort = 63500;
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

                //AppPath (Pfad der .exe) und AppDataPath (C:\ProgramData\SuE-Software\SuE-TaMi Client SQL)
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

        ExitApp:

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
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SuE-Software", "TaMi-Kassenclient", "logs");
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

    }
}
