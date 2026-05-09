using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UndertaleSaveStudioPro
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class UpdateConfig
    {
        public bool Enabled = true;
        public bool AutoInstall = true;
        public string Owner = "";
        public string Repo = "";
        public string AssetName = "UndertaleSaveStudioPro.exe";
        public string ManifestUrl = "";
        public string DownloadUrl = "";
        public string FallbackUrl = "";
    }

    internal sealed class RemoteUpdate
    {
        public long Build;
        public string DownloadUrl = "";
        public string FallbackUrl = "";
        public string Notes = "";
    }

    internal static class SelfUpdater
    {
        private const string ConfigFileName = "update.ini";
        private const long CurrentBuild = 202605091700L;

        public static void CheckForUpdates(Form owner, Action<string> report, bool userRequested)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string downloaded = null;
                try
                {
                    UpdateConfig config = LoadOrCreateConfig();
                    if (!config.Enabled)
                    {
                        if (userRequested)
                        {
                            Report(owner, report, "GitHub auto-update is disabled in update.ini.");
                        }
                        return;
                    }

                    RemoteUpdate remote = LoadRemoteManifest(config, owner, report);
                    if (remote == null)
                    {
                        string msg = "GitHub updater is ready, but update-manifest.ini is not available yet.";
                        Report(owner, report, msg);
                        if (userRequested)
                        {
                            ShowInfo(owner, "UPDATE MANIFEST NEEDED", msg + "\r\n\r\nUpload update-manifest.ini to the GitHub repo or set ManifestUrl in update.ini.", Color.FromArgb(255, 195, 70));
                        }
                        return;
                    }

                    if (remote.Build <= CurrentBuild)
                    {
                        Report(owner, report, "GitHub update check complete. This EXE is already the newest build.");
                        return;
                    }

                    string[] urls = ResolveDownloadUrls(config, remote);
                    if (urls.Length == 0)
                    {
                        string msg = "GitHub updater is ready. Add your GitHub repo or release download URL to update.ini.";
                        Report(owner, report, msg);
                        if (userRequested)
                        {
                            ShowInfo(owner, "UPDATE SETTINGS NEEDED", msg + "\r\n\r\nFile:\r\n" + ConfigPath(), Color.FromArgb(255, 195, 70));
                        }
                        return;
                    }

                    Report(owner, report, "New GitHub build " + remote.Build.ToString(CultureInfo.InvariantCulture) + " found. Downloading EXE...");
                    downloaded = Path.Combine(Path.GetTempPath(), "UTSS_update_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".exe");
                    string usedUrl = DownloadFirstWorking(urls, downloaded, owner, report);

                    if (!LooksLikeWindowsExe(downloaded))
                    {
                        throw new InvalidOperationException("The GitHub download did not look like a Windows EXE. Check update.ini, the release asset, or the raw fallback URL.");
                    }

                    string currentExe = Application.ExecutablePath;
                    string currentHash = Sha256(currentExe);
                    string newHash = Sha256(downloaded);
                    if (string.Equals(currentHash, newHash, StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteQuiet(downloaded);
                        Report(owner, report, "GitHub manifest is newer, but the downloaded EXE hash matches this build.");
                        return;
                    }

                    string tempExe = downloaded;
                    downloaded = null;
                    RunOnUi(owner, delegate
                    {
                        bool install = config.AutoInstall;
                        if (!install || userRequested)
                        {
                            install = ProDialog.ShowConfirm(owner, "GITHUB UPDATE FOUND", "A newer EXE was found on GitHub.\r\n\r\nSource:\r\n" + usedUrl + "\r\n\r\nInstall it now? The app will close, replace itself, and restart.", Color.FromArgb(85, 220, 155));
                        }
                        else
                        {
                            ProDialog.ShowInfo(owner, "GITHUB UPDATE FOUND", "A newer EXE was found on GitHub.\r\n\r\nClick OK and the app will update itself, close, and restart.", Color.FromArgb(85, 220, 155));
                        }

                        if (install)
                        {
                            Report(owner, report, "Installing GitHub update and restarting...");
                            LaunchReplacement(owner, currentExe, tempExe);
                        }
                        else
                        {
                            DeleteQuiet(tempExe);
                            Report(owner, report, "GitHub update found but not installed.");
                        }
                    });
                }
                catch (Exception ex)
                {
                    DeleteQuiet(downloaded);
                    string msg = "GitHub update check failed: " + ex.Message;
                    Report(owner, report, msg);
                    if (userRequested)
                    {
                        ShowInfo(owner, "UPDATE FAILED", msg, Color.FromArgb(255, 195, 70));
                    }
                }
            });
        }

        public static void OpenConfig()
        {
            LoadOrCreateConfig();
            try
            {
                Process.Start(ConfigPath());
            }
            catch
            {
                Process.Start("notepad.exe", QuoteArg(ConfigPath()));
            }
        }

        private static UpdateConfig LoadOrCreateConfig()
        {
            string path = ConfigPath();
            if (!File.Exists(path))
            {
                try
                {
                    File.WriteAllText(path,
                        "[Update]\r\n" +
                        "Enabled=true\r\n" +
                        "AutoInstall=true\r\n" +
                        "Owner=jerryopgenorth253-crypto\r\n" +
                        "Repo=undertale-mod-menu\r\n" +
                        "AssetName=UndertaleSaveStudioPro.exe\r\n" +
                        "ManifestUrl=https://raw.githubusercontent.com/jerryopgenorth253-crypto/undertale-mod-menu/main/update-manifest.ini\r\n" +
                        "DownloadUrl=https://github.com/jerryopgenorth253-crypto/undertale-mod-menu/releases/latest/download/UndertaleSaveStudioPro.exe\r\n" +
                        "FallbackUrl=https://raw.githubusercontent.com/jerryopgenorth253-crypto/undertale-mod-menu/main/UndertaleSaveStudioPro.exe\r\n" +
                        "\r\n" +
                        "# DownloadUrl is tried first. FallbackUrl lets the updater work before a formal GitHub Release exists.\r\n");
                }
                catch
                {
                }
            }

            UpdateConfig config = new UpdateConfig();
            if (!File.Exists(path))
            {
                return config;
            }

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("["))
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase)) config.Enabled = ParseBool(value, true);
                else if (key.Equals("AutoInstall", StringComparison.OrdinalIgnoreCase)) config.AutoInstall = ParseBool(value, true);
                else if (key.Equals("Owner", StringComparison.OrdinalIgnoreCase)) config.Owner = value;
                else if (key.Equals("Repo", StringComparison.OrdinalIgnoreCase)) config.Repo = value;
                else if (key.Equals("AssetName", StringComparison.OrdinalIgnoreCase)) config.AssetName = value;
                else if (key.Equals("ManifestUrl", StringComparison.OrdinalIgnoreCase)) config.ManifestUrl = value;
                else if (key.Equals("DownloadUrl", StringComparison.OrdinalIgnoreCase)) config.DownloadUrl = value;
                else if (key.Equals("FallbackUrl", StringComparison.OrdinalIgnoreCase)) config.FallbackUrl = value;
            }
            return config;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }
            string v = value.Trim().ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "on") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off") return false;
            return fallback;
        }

        private static RemoteUpdate LoadRemoteManifest(UpdateConfig config, Control owner, Action<string> report)
        {
            string[] urls = ResolveManifestUrls(config);
            Exception last = null;
            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    Report(owner, report, "Checking update manifest " + (i + 1).ToString() + "/" + urls.Length.ToString() + "...");
                    string text = DownloadString(urls[i]);
                    RemoteUpdate remote = ParseRemoteManifest(text);
                    if (remote != null && remote.Build > 0)
                    {
                        return remote;
                    }
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
            return null;
        }

        private static string[] ResolveManifestUrls(UpdateConfig config)
        {
            List<string> urls = new List<string>();
            if (!string.IsNullOrWhiteSpace(config.ManifestUrl))
            {
                AddUnique(urls, config.ManifestUrl.Trim());
            }

            bool hasRepo = !string.IsNullOrWhiteSpace(config.Owner) &&
                !string.IsNullOrWhiteSpace(config.Repo) &&
                config.Owner.IndexOf("YOUR_", StringComparison.OrdinalIgnoreCase) < 0;

            if (hasRepo)
            {
                AddUnique(urls, "https://raw.githubusercontent.com/" + Uri.EscapeDataString(config.Owner.Trim()) + "/" + Uri.EscapeDataString(config.Repo.Trim()) + "/main/update-manifest.ini");
            }
            return urls.ToArray();
        }

        private static RemoteUpdate ParseRemoteManifest(string text)
        {
            RemoteUpdate remote = new RemoteUpdate();
            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("["))
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                long build;
                if (key.Equals("Build", StringComparison.OrdinalIgnoreCase) && long.TryParse(value, out build)) remote.Build = build;
                else if (key.Equals("DownloadUrl", StringComparison.OrdinalIgnoreCase)) remote.DownloadUrl = value;
                else if (key.Equals("FallbackUrl", StringComparison.OrdinalIgnoreCase)) remote.FallbackUrl = value;
                else if (key.Equals("Notes", StringComparison.OrdinalIgnoreCase)) remote.Notes = value;
            }
            return remote.Build > 0 ? remote : null;
        }

        private static string[] ResolveDownloadUrls(UpdateConfig config, RemoteUpdate remote)
        {
            List<string> urls = new List<string>();
            if (remote != null && !string.IsNullOrWhiteSpace(remote.DownloadUrl))
            {
                AddUnique(urls, remote.DownloadUrl.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(config.DownloadUrl))
            {
                AddUnique(urls, config.DownloadUrl.Trim());
            }

            bool hasRepo = !string.IsNullOrWhiteSpace(config.Owner) &&
                !string.IsNullOrWhiteSpace(config.Repo) &&
                !string.IsNullOrWhiteSpace(config.AssetName) &&
                config.Owner.IndexOf("YOUR_", StringComparison.OrdinalIgnoreCase) < 0;

            if (hasRepo)
            {
                string releaseUrl = "https://github.com/" + Uri.EscapeDataString(config.Owner.Trim()) + "/" + Uri.EscapeDataString(config.Repo.Trim()) + "/releases/latest/download/" + Uri.EscapeDataString(config.AssetName.Trim());
                AddUnique(urls, releaseUrl);
            }

            if (remote != null && !string.IsNullOrWhiteSpace(remote.FallbackUrl))
            {
                AddUnique(urls, remote.FallbackUrl.Trim());
            }

            if (!string.IsNullOrWhiteSpace(config.FallbackUrl))
            {
                AddUnique(urls, config.FallbackUrl.Trim());
            }

            if (hasRepo)
            {
                string rawUrl = "https://raw.githubusercontent.com/" + Uri.EscapeDataString(config.Owner.Trim()) + "/" + Uri.EscapeDataString(config.Repo.Trim()) + "/main/" + Uri.EscapeDataString(config.AssetName.Trim());
                AddUnique(urls, rawUrl);
            }

            return urls.ToArray();
        }

        private static void AddUnique(List<string> urls, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            for (int i = 0; i < urls.Count; i++)
            {
                if (string.Equals(urls[i], url, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            urls.Add(url);
        }

        private static string DownloadFirstWorking(string[] urls, string target, Control owner, Action<string> report)
        {
            Exception last = null;
            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    DeleteQuiet(target);
                    Report(owner, report, "Checking GitHub source " + (i + 1).ToString() + "/" + urls.Length.ToString() + "...");
                    DownloadFile(urls[i], target);
                    return urls[i];
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            throw new InvalidOperationException("Could not download the update EXE from GitHub. Last error: " + (last == null ? "unknown" : last.Message));
        }

        private static void DownloadFile(string url, string target)
        {
            SetTls();
            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "UndertaleSaveStudioPro-Updater/1.0";
                client.DownloadFile(url, target);
            }
        }

        private static string DownloadString(string url)
        {
            SetTls();
            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "UndertaleSaveStudioPro-Updater/1.0";
                return client.DownloadString(url);
            }
        }

        private static void SetTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch
            {
            }
        }

        private static bool LooksLikeWindowsExe(string path)
        {
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    if (fs.Length < 4096)
                    {
                        return false;
                    }
                    return fs.ReadByte() == 0x4D && fs.ReadByte() == 0x5A;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private static void LaunchReplacement(Form owner, string currentExe, string newExe)
        {
            string script = Path.Combine(Path.GetTempPath(), "UTSS_apply_update_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".ps1");
            string backup = currentExe + ".previous";
            string scriptText =
                "param([int]$ProcessId,[string]$Target,[string]$NewFile,[string]$Backup)\r\n" +
                "$ErrorActionPreference='Stop'\r\n" +
                "try { Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue } catch {}\r\n" +
                "Start-Sleep -Milliseconds 500\r\n" +
                "try { if (Test-Path -LiteralPath $Backup) { Remove-Item -LiteralPath $Backup -Force } } catch {}\r\n" +
                "try { if (Test-Path -LiteralPath $Target) { Copy-Item -LiteralPath $Target -Destination $Backup -Force } } catch {}\r\n" +
                "Copy-Item -LiteralPath $NewFile -Destination $Target -Force\r\n" +
                "Remove-Item -LiteralPath $NewFile -Force\r\n" +
                "Start-Process -FilePath $Target\r\n" +
                "Start-Sleep -Seconds 2\r\n" +
                "try { Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force } catch {}\r\n";
            File.WriteAllText(script, scriptText);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " + QuoteArg(script) +
                " -ProcessId " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) +
                " -Target " + QuoteArg(currentExe) +
                " -NewFile " + QuoteArg(newExe) +
                " -Backup " + QuoteArg(backup);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            Process.Start(psi);
            owner.Close();
            Application.Exit();
        }

        private static string ConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        }

        private static void Report(Control owner, Action<string> report, string text)
        {
            if (owner == null || owner.IsDisposed || report == null)
            {
                return;
            }
            RunOnUi(owner, delegate { report(text); });
        }

        private static void ShowInfo(Control owner, string title, string message, Color accent)
        {
            RunOnUi(owner, delegate { ProDialog.ShowInfo(owner, title, message, accent); });
        }

        private static void RunOnUi(Control owner, Action action)
        {
            if (owner == null || owner.IsDisposed || action == null)
            {
                return;
            }
            try
            {
                if (owner.InvokeRequired)
                {
                    owner.BeginInvoke(new MethodInvoker(delegate { if (!owner.IsDisposed) action(); }));
                }
                else
                {
                    action();
                }
            }
            catch
            {
            }
        }

        private static void DeleteQuiet(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static string QuoteArg(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class SaveModel
    {
        public const int Name = 0;
        public const int Lv = 1;
        public const int MaxHp = 2;
        public const int MaxEn = 3;
        public const int At = 4;
        public const int WeaponStrength = 5;
        public const int Defense = 6;
        public const int ArmorDefense = 7;
        public const int Speed = 8;
        public const int Xp = 9;
        public const int Gold = 10;
        public const int Kills = 11;
        public const int Items = 12;
        public const int Weapon = 28;
        public const int Armor = 29;
        public const int Flags = 30;
        public const int Plot = 542;
        public const int Song = 546;
        public const int Room = 547;
        public const int Time = 548;

        public string SaveDir;
        public string IniText;
        public List<string> Lines;
        public string NewLine;
        public string LoadedSource;

        public SaveModel()
        {
            SaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UNDERTALE");
            IniText = "[General]\r\nfun=\"1.000000\"";
            Lines = null;
            NewLine = "\r\n";
            LoadedSource = "";
        }

        public bool HasFile0
        {
            get { return Lines != null; }
        }

        public void LoadLive()
        {
            string ini = Path.Combine(SaveDir, "undertale.ini");
            if (File.Exists(ini))
            {
                IniText = File.ReadAllText(ini);
            }
            else
            {
                IniText = "[General]\r\nfun=\"1.000000\"";
            }

            string file0 = Path.Combine(SaveDir, "file0");
            string file9 = Path.Combine(SaveDir, "file9");
            if (File.Exists(file0))
            {
                LoadFile0Text(File.ReadAllText(file0), "file0");
            }
            else if (File.Exists(file9))
            {
                LoadFile0Text(File.ReadAllText(file9), "file9");
            }
            else
            {
                Lines = null;
                LoadedSource = "";
            }
        }

        public void LoadFile0Text(string text, string source)
        {
            NewLine = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            Lines = new List<string>(normalized.Split('\n'));
            if (Lines.Count > 1 && Lines[Lines.Count - 1] == "")
            {
                Lines.RemoveAt(Lines.Count - 1);
            }
            EnsureLength();
            LoadedSource = source;
        }

        public void CreateShell()
        {
            Lines = new List<string>();
            for (int i = 0; i <= Time; i++)
            {
                Lines.Add("0");
            }
            Set(Name, "CHARA");
            SetNumber(Lv, 1);
            SetNumber(MaxHp, 20);
            SetNumber(MaxEn, 20);
            SetNumber(At, 10);
            SetNumber(WeaponStrength, 0);
            SetNumber(Defense, 10);
            SetNumber(ArmorDefense, 0);
            SetNumber(Speed, 4);
            SetNumber(Xp, 0);
            SetNumber(Gold, 0);
            SetNumber(Kills, 0);
            for (int i = 0; i < 8; i++)
            {
                SetNumber(Items + (i * 2), 0);
                SetNumber(Items + (i * 2) + 1, 0);
            }
            SetNumber(Weapon, 3);
            SetNumber(Armor, 4);
            SetNumber(Flags + 300, 14);
            SetNumber(Plot, 0);
            SetNumber(543, 1);
            SetNumber(544, 1);
            SetNumber(545, 0);
            SetNumber(Song, -1);
            SetNumber(Room, 4);
            SetNumber(Time, 0);
            LoadedSource = "new shell";
        }

        public int Fun()
        {
            string[] lines = IniText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("fun=", StringComparison.OrdinalIgnoreCase))
                {
                    string raw = t.Substring(4).Trim().Trim('"');
                    decimal d;
                    if (decimal.TryParse(raw, out d))
                    {
                        return Clamp((int)Math.Round(d), 1, 100);
                    }
                }
            }
            return 1;
        }

        public void SetFun(int value)
        {
            value = Clamp(value, 1, 100);
            string replacement = "fun=\"" + value.ToString() + ".000000\"";
            string[] lines = IniText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            bool replaced = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("fun=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = replacement;
                    replaced = true;
                }
            }
            if (!replaced)
            {
                List<string> next = new List<string>(lines);
                int general = next.FindIndex(delegate(string s) { return s.Trim().Equals("[General]", StringComparison.OrdinalIgnoreCase); });
                if (general >= 0)
                {
                    next.Insert(general + 1, replacement);
                }
                else
                {
                    next.Insert(0, replacement);
                    next.Insert(0, "[General]");
                }
                lines = next.ToArray();
            }
            IniText = string.Join("\r\n", lines);
        }

        public string Get(int index)
        {
            if (Lines == null || index < 0 || index >= Lines.Count)
            {
                return "";
            }
            return Lines[index];
        }

        public int GetNumber(int index, int fallback)
        {
            int n;
            if (int.TryParse(Get(index), out n))
            {
                return n;
            }
            decimal d;
            if (decimal.TryParse(Get(index), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ||
                decimal.TryParse(Get(index), NumberStyles.Float, CultureInfo.CurrentCulture, out d))
            {
                return (int)Math.Round(d);
            }
            return fallback;
        }

        public void Set(int index, string value)
        {
            if (Lines == null)
            {
                return;
            }
            EnsureLength();
            while (Lines.Count <= index)
            {
                Lines.Add("0");
            }
            Lines[index] = value == null ? "" : value;
        }

        public void SetNumber(int index, int value)
        {
            Set(index, value.ToString());
        }

        public int DamagePower()
        {
            long power = (long)GetNumber(At, 10) + (long)GetNumber(WeaponStrength, 0);
            if (power < 0) return 0;
            if (power > 999999999L) return 999999999;
            return (int)power;
        }

        public void SetDamagePower(int value)
        {
            value = Clamp(value, 0, 999999999);
            SetNumber(At, value);
            SetNumber(WeaponStrength, 0);
        }

        public void SetCoreStats(int lv, int hp, int damage, int xp, int gold, int kills)
        {
            SetNumber(Lv, Clamp(lv, 1, 999999999));
            SetNumber(MaxHp, Clamp(hp, 1, 999999999));
            SetNumber(MaxEn, Clamp(hp, 1, 999999999));
            SetDamagePower(damage);
            SetNumber(Xp, Clamp(xp, 0, 999999999));
            SetNumber(Gold, Clamp(gold, 0, 999999999));
            SetNumber(Kills, Clamp(kills, 0, 999999));
        }

        public int GetFlag(int flag, int fallback)
        {
            return GetNumber(Flags + flag, fallback);
        }

        public void SetFlag(int flag, int value)
        {
            SetNumber(Flags + flag, value);
        }

        public void SetFlags(int[] flags, int value)
        {
            if (flags == null)
            {
                return;
            }
            for (int i = 0; i < flags.Length; i++)
            {
                SetFlag(flags[i], value);
            }
        }

        public void SetPhoneSlots(int value)
        {
            for (int i = 0; i < 8; i++)
            {
                SetNumber(Items + (i * 2) + 1, value);
            }
        }

        public void Equip(int weapon, int armor)
        {
            SetNumber(Weapon, Clamp(weapon, 0, 999999));
            SetNumber(Armor, Clamp(armor, 0, 999999));
        }

        public int[] Inventory()
        {
            int[] ids = new int[8];
            for (int i = 0; i < 8; i++)
            {
                ids[i] = GetNumber(Items + (i * 2), 0);
            }
            return ids;
        }

        public void SetInventory(int[] ids)
        {
            if (Lines == null)
            {
                return;
            }
            for (int i = 0; i < 8; i++)
            {
                SetNumber(Items + (i * 2), i < ids.Length ? ids[i] : 0);
            }
        }

        public string File0Text()
        {
            if (Lines == null)
            {
                return "";
            }
            EnsureLength();
            return string.Join(NewLine, Lines.ToArray());
        }

        public string WriteLive(bool mirrorFile9)
        {
            return WriteLive(mirrorFile9, true);
        }

        public string WriteLive(bool mirrorFile9, bool makeBackup)
        {
            Directory.CreateDirectory(SaveDir);
            string backup = makeBackup ? BackupExisting(mirrorFile9) : "";
            File.WriteAllText(Path.Combine(SaveDir, "undertale.ini"), IniText);
            if (Lines != null)
            {
                File.WriteAllText(Path.Combine(SaveDir, "file0"), File0Text());
                if (mirrorFile9)
                {
                    File.WriteAllText(Path.Combine(SaveDir, "file9"), File0Text());
                }
            }
            WriteLiveConfig(true);
            return backup;
        }

        public void WriteLiveConfig(bool enabled)
        {
            Directory.CreateDirectory(SaveDir);
            List<string> lines = new List<string>();
            lines.Add("[Live]");
            lines.Add("enabled=" + (enabled ? "1" : "0"));
            lines.Add("[Player]");
            lines.Add("lv=" + (HasFile0 ? GetNumber(Lv, 1) : 1).ToString());
            lines.Add("hp=" + (HasFile0 ? GetNumber(MaxHp, 20) : 20).ToString());
            lines.Add("xp=" + (HasFile0 ? GetNumber(Xp, 0) : 0).ToString());
            lines.Add("gold=" + (HasFile0 ? GetNumber(Gold, 0) : 0).ToString());
            lines.Add("damage=" + (HasFile0 ? DamagePower() : 10).ToString());
            lines.Add("room=" + (HasFile0 ? GetNumber(Room, -1) : -1).ToString());
            lines.Add("murder=" + (HasFile0 ? GetNumber(Flags + 26, -1) : -1).ToString());
            lines.Add("[Items]");
            int[] items = HasFile0 ? Inventory() : new int[8];
            for (int i = 0; i < 8; i++)
            {
                lines.Add("slot" + i.ToString() + "=" + items[i].ToString());
            }
            File.WriteAllLines(Path.Combine(SaveDir, "codex_live.ini"), lines.ToArray());
        }

        private string BackupExisting(bool mirrorFile9)
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            string[] names = mirrorFile9 ? new string[] { "undertale.ini", "file0", "file9" } : new string[] { "undertale.ini", "file0" };
            bool any = false;
            for (int i = 0; i < names.Length; i++)
            {
                string source = Path.Combine(SaveDir, names[i]);
                if (File.Exists(source))
                {
                    Directory.CreateDirectory(root);
                    File.Copy(source, Path.Combine(root, names[i]), true);
                    any = true;
                }
            }
            return any ? root : "";
        }

        private void EnsureLength()
        {
            if (Lines == null)
            {
                return;
            }
            while (Lines.Count <= Time)
            {
                Lines.Add("0");
            }
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    internal sealed class ItemDef
    {
        public int Id;
        public string Name;
        public bool Experimental;

        public ItemDef(int id, string name, bool experimental)
        {
            Id = id;
            Name = name;
            Experimental = experimental;
        }

        public override string ToString()
        {
            return Id.ToString().PadLeft(4, '0') + "  " + Name + (Experimental ? "  [RAW]" : "");
        }
    }

    internal static class ItemCatalog
    {
        public static readonly ItemDef[] Items = new ItemDef[]
        {
            new ItemDef(0, "Empty", false),
            new ItemDef(1, "Monster Candy", false),
            new ItemDef(2, "Croquet Roll", false),
            new ItemDef(3, "Stick", false),
            new ItemDef(4, "Bandage", false),
            new ItemDef(5, "Rock Candy", false),
            new ItemDef(6, "Pumpkin Rings", false),
            new ItemDef(7, "Spider Donut", false),
            new ItemDef(8, "Stoic Onion", false),
            new ItemDef(9, "Ghost Fruit", false),
            new ItemDef(10, "Spider Cider", false),
            new ItemDef(11, "Butterscotch Pie", false),
            new ItemDef(12, "Faded Ribbon", false),
            new ItemDef(13, "Toy Knife", false),
            new ItemDef(14, "Tough Glove", false),
            new ItemDef(15, "Manly Bandanna", false),
            new ItemDef(16, "Snowman Piece", false),
            new ItemDef(17, "Nice Cream", false),
            new ItemDef(18, "Puppydough Icecream", false),
            new ItemDef(19, "Bisicle", false),
            new ItemDef(20, "Unisicle", false),
            new ItemDef(21, "Cinnamon Bun", false),
            new ItemDef(22, "Temmie Flakes", false),
            new ItemDef(23, "Abandoned Quiche", false),
            new ItemDef(24, "Old Tutu", false),
            new ItemDef(25, "Ballet Shoes", false),
            new ItemDef(26, "Punch Card", false),
            new ItemDef(27, "Annoying Dog", false),
            new ItemDef(28, "Dog Salad", false),
            new ItemDef(29, "Dog Residue", false),
            new ItemDef(30, "Dog Residue", false),
            new ItemDef(31, "Dog Residue", false),
            new ItemDef(32, "Dog Residue", false),
            new ItemDef(33, "Dog Residue", false),
            new ItemDef(34, "Dog Residue", false),
            new ItemDef(35, "Astronaut Food", false),
            new ItemDef(36, "Instant Noodles", false),
            new ItemDef(37, "Crab Apple", false),
            new ItemDef(38, "Hot Dog...?", false),
            new ItemDef(39, "Hot Cat", false),
            new ItemDef(40, "Glamburger", false),
            new ItemDef(41, "Sea Tea", false),
            new ItemDef(42, "Starfait", false),
            new ItemDef(43, "Legendary Hero", false),
            new ItemDef(44, "Cloudy Glasses", false),
            new ItemDef(45, "Torn Notebook", false),
            new ItemDef(46, "Stained Apron", false),
            new ItemDef(47, "Burnt Pan", false),
            new ItemDef(48, "Cowboy Hat", false),
            new ItemDef(49, "Empty Gun", false),
            new ItemDef(50, "Heart Locket", false),
            new ItemDef(51, "Worn Dagger", false),
            new ItemDef(52, "Real Knife", false),
            new ItemDef(53, "The Locket", false),
            new ItemDef(54, "Bad Memory", false),
            new ItemDef(55, "Dream", false),
            new ItemDef(56, "Undyne's Letter", false),
            new ItemDef(57, "Undyne Letter EX", false),
            new ItemDef(58, "Popato Chisps", false),
            new ItemDef(59, "Junk Food", false),
            new ItemDef(60, "Mystery Key", false),
            new ItemDef(61, "Face Steak", false),
            new ItemDef(62, "Hush Puppy", false),
            new ItemDef(63, "Snail Pie", false),
            new ItemDef(64, "Temy Armor", false),
            new ItemDef(9001, "Gaster Blaster token", true),
            new ItemDef(9002, "Bone Barrage token", true),
            new ItemDef(9003, "Blue Bone token", true),
            new ItemDef(9004, "Undyne Spear token", true),
            new ItemDef(9005, "Blue Spear token", true),
            new ItemDef(9006, "Asgore Spear Swipe token", true),
            new ItemDef(9007, "Flowey Pellet token", true),
            new ItemDef(9008, "Asriel Star token", true)
        };

        public static int[] NormalIds()
        {
            return Items.Where(delegate(ItemDef item) { return !item.Experimental && item.Id > 0; }).Select(delegate(ItemDef item) { return item.Id; }).ToArray();
        }

        public static int[] BattleTokenIds()
        {
            return Items.Where(delegate(ItemDef item) { return item.Experimental; }).Select(delegate(ItemDef item) { return item.Id; }).ToArray();
        }

        public static ItemDef Find(int id)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].Id == id) return Items[i];
            }
            return null;
        }
    }

    internal sealed class RoomWarp
    {
        public int Id;
        public string Name;

        public RoomWarp(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return Id.ToString().PadLeft(3, '0') + "  " + Name;
        }
    }

    internal static class RoomCatalog
    {
        public static RoomWarp[] Load()
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rooms.txt");
            if (File.Exists(file))
            {
                List<RoomWarp> loaded = new List<RoomWarp>();
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] parts = line.Split(new char[] { '|' }, 2);
                    int id;
                    if (parts.Length == 2 && int.TryParse(parts[0], out id))
                    {
                        loaded.Add(new RoomWarp(id, parts[1]));
                    }
                }
                if (loaded.Count > 0)
                {
                    return loaded.ToArray();
                }
            }

            List<RoomWarp> fallback = new List<RoomWarp>();
            for (int i = 4; i <= 336; i++)
            {
                fallback.Add(new RoomWarp(i, "room_" + i.ToString()));
            }
            return fallback.ToArray();
        }

        public static RoomWarp[] PlayerRooms()
        {
            return Load().Where(delegate(RoomWarp room)
            {
                string n = room.Name.ToLowerInvariant();
                if (room.Id < 4) return false;
                if (n.Contains("battle")) return false;
                if (n.Contains("test")) return false;
                if (n.Contains("gameover")) return false;
                if (n.Contains("intro")) return false;
                return true;
            }).ToArray();
        }
    }

    internal sealed class GradientHeader : Panel
    {
        public GradientHeader()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(4, 8, 18), Color.FromArgb(80, 9, 32), 9f))
            {
                e.Graphics.FillRectangle(b, ClientRectangle);
            }
            using (LinearGradientBrush sweep = new LinearGradientBrush(ClientRectangle, Color.FromArgb(0, 54, 151, 255), Color.FromArgb(88, 54, 151, 255), 0f))
            {
                e.Graphics.FillRectangle(sweep, Width - 360, 0, 360, Height);
            }
            using (Pen beam = new Pen(Color.FromArgb(125, 54, 151, 255), 2f))
            {
                e.Graphics.DrawLine(beam, 120, Height - 12, Width - 70, 14);
            }
            using (Pen beam = new Pen(Color.FromArgb(105, 255, 63, 92), 2f))
            {
                e.Graphics.DrawLine(beam, 0, 16, Width / 2, Height - 8);
            }
            using (Pen grid = new Pen(Color.FromArgb(20, 255, 255, 255), 1f))
            {
                for (int x = 0; x < Width; x += 34)
                {
                    e.Graphics.DrawLine(grid, x, 0, x - 42, Height);
                }
            }
            using (LinearGradientBrush shine = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(1, Height / 2)), Color.FromArgb(38, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
            {
                e.Graphics.FillRectangle(shine, 0, 0, Width, Math.Max(1, Height / 2));
            }
            using (Pen p = new Pen(Color.FromArgb(255, 63, 92), 2f))
            {
                e.Graphics.DrawLine(p, 0, Height - 2, Width, Height - 2);
            }
            using (Pen p = new Pen(Color.FromArgb(54, 151, 255), 2f))
            {
                e.Graphics.DrawLine(p, Width / 2, Height - 2, Width, Height - 2);
            }
            using (Pen p = new Pen(Color.FromArgb(40, 255, 195, 70), 1f))
            {
                e.Graphics.DrawLine(p, 0, Height - 5, Width, Height - 5);
            }
            base.OnPaint(e);
        }
    }

    internal sealed class NeonBodyPanel : Panel
    {
        public NeonBodyPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = ClientRectangle;
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(3, 6, 14), Color.FromArgb(11, 16, 30), 22f))
            {
                e.Graphics.FillRectangle(b, r);
            }
            using (Pen grid = new Pen(Color.FromArgb(18, 60, 150, 255), 1f))
            {
                for (int x = -Height; x < Width + Height; x += 64)
                {
                    e.Graphics.DrawLine(grid, x, Height, x + Height, 0);
                }
            }
            using (Pen red = new Pen(Color.FromArgb(170, 255, 18, 70), 2f))
            using (Pen blue = new Pen(Color.FromArgb(160, 0, 205, 255), 2f))
            using (Pen green = new Pen(Color.FromArgb(125, 60, 255, 120), 2f))
            {
                e.Graphics.DrawLine(red, 0, 42, Width / 3, Height - 18);
                e.Graphics.DrawLine(blue, Width / 4, 20, Width - 20, 2);
                e.Graphics.DrawLine(green, Width / 2, Height - 32, Width - 80, Height - 98);
            }

            int[,] sparks = new int[,]
            {
                { 34, 610, 255, 63, 92 }, { 180, 110, 54, 151, 255 }, { 360, 42, 255, 195, 70 },
                { 704, 92, 85, 220, 155 }, { 920, 540, 255, 63, 92 }, { 1190, 230, 54, 151, 255 },
                { 1370, 620, 255, 195, 70 }, { 540, 690, 125, 112, 255 }, { 1020, 32, 85, 220, 155 }
            };
            for (int i = 0; i < sparks.GetLength(0); i++)
            {
                using (Pen p = new Pen(Color.FromArgb(210, sparks[i, 2], sparks[i, 3], sparks[i, 4]), 2f))
                {
                    int x = sparks[i, 0] % Math.Max(1, Width);
                    int y = sparks[i, 1] % Math.Max(1, Height);
                    e.Graphics.DrawLine(p, x, y, x + 16, y + 5);
                }
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly SaveModel model = new SaveModel();
        private TextBox nameBox;
        private NumericUpDown funBox;
        private NumericUpDown lvBox;
        private NumericUpDown hpBox;
        private NumericUpDown xpBox;
        private NumericUpDown goldBox;
        private NumericUpDown dmgBox;
        private NumericUpDown killsBox;
        private NumericUpDown murderBox;
        private NumericUpDown plotBox;
        private NumericUpDown roomBox;
        private NumericUpDown timeBox;
        private ComboBox routeBox;
        private CheckBox syncLv;
        private CheckBox mirrorFile9;
        private Label savePathLabel;
        private Label statusLabel;
        private Button inventoryButton;
        private bool suppressRoutePreset;
        private bool suppressToggleEvents;
        private CheckBox pacifistToggle;
        private CheckBox neutralToggle;
        private CheckBox genocideToggle;
        private CheckBox customToggle;
        private CheckBox watchGameToggle;
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer gameWatchTimer = new Timer();
        private bool gameWasRunning;
        private bool gameWatchBackedUp;
        private DateTime lastAutoApply = DateTime.MinValue;
        private const int PowerMax = 999999999;

        private readonly int[] hpForLv = new int[] { 0, 20, 24, 28, 32, 36, 40, 44, 48, 52, 56, 60, 64, 68, 72, 76, 80, 84, 88, 92, 99 };
        private readonly int[] xpForLv = new int[] { 0, 0, 10, 30, 70, 120, 200, 300, 500, 800, 1200, 1700, 2500, 3500, 5000, 7000, 10000, 15000, 25000, 50000, 99999 };

        public MainForm()
        {
            Text = "Undertale Mod Menu - Feature Vault Neon";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1440, 860);
            Size = new Size(1540, 900);
            BackColor = Color.FromArgb(4, 6, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            gameWatchTimer.Interval = 3000;
            gameWatchTimer.Tick += delegate { WatchGameTick(); };
            BuildUi();
            LoadLive();
            Shown += delegate
            {
                ProDialog.ShowInfo(this, "FEATURE VAULT MODE", "Quick path:\r\n\r\n1. Load your save.\r\n2. Pick a preset or type numbers.\r\n3. Press Write Save.\r\n\r\nBackups are made before live writes.", Color.FromArgb(54, 151, 255));
                SelfUpdater.CheckForUpdates(this, delegate(string text) { statusLabel.Text = text; }, false);
            };
        }

        private void BuildUi()
        {
            Controls.Clear();

            GradientHeader header = new GradientHeader();
            header.Location = new Point(0, 0);
            header.Size = new Size(ClientSize.Width, 96);
            header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "UNDERTALE MOD MENU";
            title.Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.Location = new Point(28, 12);
            header.Controls.Add(title);

            Label badge = new Label();
            badge.Text = "NEON VAULT MODE";
            badge.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
            badge.ForeColor = Color.FromArgb(255, 214, 120);
            badge.BackColor = Color.FromArgb(28, 10, 16);
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.Location = new Point(392, 24);
            badge.Size = new Size(150, 24);
            header.Controls.Add(badge);

            Label featureBadge = new Label();
            featureBadge.Text = "100K+ FEATURES";
            featureBadge.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
            featureBadge.ForeColor = Color.FromArgb(170, 230, 255);
            featureBadge.BackColor = Color.FromArgb(8, 24, 38);
            featureBadge.TextAlign = ContentAlignment.MiddleCenter;
            featureBadge.Location = new Point(552, 24);
            featureBadge.Size = new Size(132, 24);
            header.Controls.Add(featureBadge);

            savePathLabel = new Label();
            savePathLabel.Text = model.SaveDir;
            savePathLabel.ForeColor = Color.FromArgb(210, 210, 218);
            savePathLabel.AutoSize = false;
            savePathLabel.Width = 780;
            savePathLabel.Height = 32;
            savePathLabel.Location = new Point(32, 61);
            header.Controls.Add(savePathLabel);

            FlowLayoutPanel headerButtons = new FlowLayoutPanel();
            headerButtons.FlowDirection = FlowDirection.LeftToRight;
            headerButtons.WrapContents = false;
            headerButtons.AutoSize = true;
            headerButtons.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            headerButtons.Location = new Point(Math.Max(900, ClientSize.Width - 430), 28);
            header.Controls.Add(headerButtons);

            Button loadHeader = MakeButton("1 Load Save", LoadLive, Color.FromArgb(54, 151, 255));
            Button folderHeader = MakeButton("Find Folder", ChooseFolder, Color.FromArgb(125, 112, 255));
            Button guideHeader = MakeButton("Player Guide", OpenPlayerGuide, Color.FromArgb(85, 220, 155));
            headerButtons.Controls.Add(loadHeader);
            headerButtons.Controls.Add(folderHeader);
            headerButtons.Controls.Add(guideHeader);
            tips.SetToolTip(loadHeader, "Reload undertale.ini, file0, and file9 from the selected save folder.");
            tips.SetToolTip(folderHeader, "Pick a different Undertale save folder.");
            tips.SetToolTip(guideHeader, "Open the simple player guide.");

            Panel body = new NeonBodyPanel();
            body.Location = new Point(0, 96);
            body.Size = new Size(ClientSize.Width, ClientSize.Height - 96);
            body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            body.AutoScroll = true;
            body.BackColor = Color.FromArgb(4, 6, 10);
            Controls.Add(body);

            Label powerTitle = new Label();
            powerTitle.Text = "Player Numbers";
            powerTitle.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
            powerTitle.ForeColor = Color.White;
            powerTitle.AutoSize = true;
            powerTitle.Location = new Point(28, 20);
            body.Controls.Add(powerTitle);

            Label powerHint = new Label();
            powerHint.Text = "Type values here. Nothing changes until Write Save.";
            powerHint.Font = new Font("Segoe UI", 9.5f);
            powerHint.ForeColor = Color.FromArgb(160, 164, 176);
            powerHint.AutoSize = true;
            powerHint.Location = new Point(30, 50);
            body.Controls.Add(powerHint);

            lvBox = AddMainNumber(body, "LEVEL", 28, 82);
            hpBox = AddMainNumber(body, "HP", 28, 192);
            xpBox = AddMainNumber(body, "EXP", 28, 302);
            goldBox = AddMainNumber(body, "GOLD", 28, 412);
            dmgBox = AddMainNumber(body, "DMG", 28, 522);

            Button write = MakeWideButton("WRITE SAVE", delegate { WriteSave(); }, Color.FromArgb(255, 63, 92));
            write.Width = 350;
            write.Height = 56;
            write.Location = new Point(28, 636);
            body.Controls.Add(write);
            tips.SetToolTip(write, "Writes the values to Undertale after creating a backup.");

            Button uncap = MakeWideButton("999,999,999 MAX", delegate { UncapPower(); }, Color.FromArgb(0, 205, 255));
            uncap.Width = 350;
            uncap.Location = new Point(28, 704);
            body.Controls.Add(uncap);
            tips.SetToolTip(uncap, "Set LV, HP, EXP, gold, and damage to 999,999,999.");

            Panel hero = MakeFeatureVaultHero();
            hero.Location = new Point(410, 22);
            body.Controls.Add(hero);

            Panel easyPanel = MakeEasyStartPanel();
            easyPanel.Location = new Point(410, 376);
            body.Controls.Add(easyPanel);

            Panel bottomBar = MakeBottomActionBar();
            bottomBar.Location = new Point(410, 704);
            body.Controls.Add(bottomBar);

            Label toggleTitle = new Label();
            toggleTitle.Text = "Control Center";
            toggleTitle.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
            toggleTitle.ForeColor = Color.White;
            toggleTitle.AutoSize = true;
            toggleTitle.Location = new Point(970, 20);
            body.Controls.Add(toggleTitle);

            Label toggleHint = new Label();
            toggleHint.Text = "Pick a route, then use the big tool buttons below.";
            toggleHint.Font = new Font("Segoe UI", 9f);
            toggleHint.ForeColor = Color.FromArgb(160, 164, 176);
            toggleHint.AutoSize = true;
            toggleHint.Location = new Point(972, 50);
            body.Controls.Add(toggleHint);

            int tx = 970;
            int ty = 82;
            syncLv = MakeToggle("Sync LV Stats", true, Color.FromArgb(54, 151, 255));
            syncLv.Location = new Point(tx, ty);
            body.Controls.Add(syncLv);
            tips.SetToolTip(syncLv, "When ON, changing LV automatically fills normal HP, EXP, and damage.");

            mirrorFile9 = MakeToggle("Mirror file9", true, Color.FromArgb(85, 220, 155));
            mirrorFile9.Location = new Point(tx + 170, ty);
            body.Controls.Add(mirrorFile9);
            tips.SetToolTip(mirrorFile9, "Write the same save data to file9 too. Recommended.");

            watchGameToggle = MakeToggle("Watch Game", false, Color.FromArgb(255, 63, 92));
            watchGameToggle.Location = new Point(tx + 340, ty);
            watchGameToggle.CheckedChanged += delegate { ToggleGameWatch(); };
            body.Controls.Add(watchGameToggle);
            tips.SetToolTip(watchGameToggle, "Wait for Undertale to launch and auto-refresh the edited save/live config.");

            ty += 62;
            pacifistToggle = MakeToggle("Pacifist", false, Color.FromArgb(85, 220, 155));
            pacifistToggle.Location = new Point(tx, ty);
            pacifistToggle.CheckedChanged += delegate { RouteToggleChanged(0, pacifistToggle); };
            body.Controls.Add(pacifistToggle);

            neutralToggle = MakeToggle("Neutral", false, Color.FromArgb(255, 195, 70));
            neutralToggle.Location = new Point(tx + 170, ty);
            neutralToggle.CheckedChanged += delegate { RouteToggleChanged(1, neutralToggle); };
            body.Controls.Add(neutralToggle);

            ty += 62;
            genocideToggle = MakeToggle("Genocide", false, Color.FromArgb(255, 63, 92));
            genocideToggle.Location = new Point(tx, ty);
            genocideToggle.CheckedChanged += delegate { RouteToggleChanged(2, genocideToggle); };
            body.Controls.Add(genocideToggle);

            customToggle = MakeToggle("Custom", true, Color.FromArgb(125, 112, 255));
            customToggle.Location = new Point(tx + 170, ty);
            customToggle.CheckedChanged += delegate { RouteToggleChanged(3, customToggle); };
            body.Controls.Add(customToggle);

            ty += 78;
            Button vault = MakeWideButton("Feature Vault 100K+", delegate { OpenFeatureVault(); }, Color.FromArgb(54, 151, 255));
            vault.Width = 500;
            vault.Location = new Point(tx, ty);
            body.Controls.Add(vault);
            tips.SetToolTip(vault, "Search big one-click presets for routes, flags, stats, rooms, and inventory.");

            ty += 58;
            Button modHub = MakeWideButton("GameJolt Mod Hub", delegate { OpenGameJoltModHub(); }, Color.FromArgb(85, 220, 155));
            modHub.Width = 500;
            modHub.Location = new Point(tx, ty);
            body.Controls.Add(modHub);
            tips.SetToolTip(modHub, "Browse GameJolt Undertale projects and install downloaded ZIP/folder mods safely.");

            ty += 58;
            Button chaos = MakeWideButton("Chaos Console", delegate { OpenChaosConsole(); }, Color.FromArgb(255, 63, 92));
            chaos.Width = 330;
            chaos.Location = new Point(tx, ty);
            body.Controls.Add(chaos);
            tips.SetToolTip(chaos, "Randomizers and advanced save experiments.");

            ty += 58;
            inventoryButton = MakeWideButton("Inventory Forge", delegate { OpenInventoryForge(); }, Color.FromArgb(255, 195, 70));
            inventoryButton.Width = 330;
            inventoryButton.Location = new Point(tx, ty);
            body.Controls.Add(inventoryButton);
            tips.SetToolTip(inventoryButton, "Give yourself regular Undertale items or raw mod-token IDs.");

            ty += 58;
            Button newShell = MakeWideButton("New Save Shell", delegate { NewShell(); }, Color.FromArgb(125, 112, 255));
            newShell.Width = 160;
            newShell.Location = new Point(tx, ty);
            body.Controls.Add(newShell);
            tips.SetToolTip(newShell, "Create a fresh editable save in memory. It writes only after Write Save.");

            Button import = MakeWideButton("Import file0", delegate { ImportFile0(); }, Color.FromArgb(54, 151, 255));
            import.Width = 160;
            import.Location = new Point(tx + 170, ty);
            body.Controls.Add(import);
            tips.SetToolTip(import, "Import an existing file0 or file9 manually.");

            ty += 58;
            Button randomFun = MakeWideButton("Random FUN", delegate { RandomFun(); }, Color.FromArgb(85, 220, 155));
            randomFun.Width = 160;
            randomFun.Location = new Point(tx, ty);
            body.Controls.Add(randomFun);
            tips.SetToolTip(randomFun, "Randomize the Undertale FUN value.");

            Button god = MakeWideButton("God Preset", delegate { ApplyGodPresetFromMain(); }, Color.FromArgb(255, 63, 92));
            god.Width = 160;
            god.Location = new Point(tx + 170, ty);
            body.Controls.Add(god);
            tips.SetToolTip(god, "Max out the big number boxes in memory.");

            ty += 58;
            Button hook = MakeWideButton("Install Live Hook", delegate { InstallLiveHookMod(); }, Color.FromArgb(255, 195, 70));
            hook.Width = 330;
            hook.Location = new Point(tx, ty);
            body.Controls.Add(hook);
            tips.SetToolTip(hook, "Advanced: patch data.win so live config values can apply while Undertale is running.");

            ty += 58;
            Button updateNow = MakeWideButton("Check GitHub Update", delegate { CheckGitHubUpdateNow(); }, Color.FromArgb(54, 151, 255));
            updateNow.Width = 160;
            updateNow.Location = new Point(tx, ty);
            body.Controls.Add(updateNow);
            tips.SetToolTip(updateNow, "Check GitHub for a newer EXE.");

            Button updateSettings = MakeWideButton("Update Settings", delegate { OpenUpdateSettings(); }, Color.FromArgb(125, 112, 255));
            updateSettings.Width = 160;
            updateSettings.Location = new Point(tx + 170, ty);
            body.Controls.Add(updateSettings);
            tips.SetToolTip(updateSettings, "Open update.ini beside the EXE.");

            statusLabel = new Label();
            statusLabel.Text = "";
            statusLabel.ForeColor = Color.FromArgb(224, 226, 235);
            statusLabel.BackColor = Color.FromArgb(13, 15, 22);
            statusLabel.BorderStyle = BorderStyle.FixedSingle;
            statusLabel.AutoSize = false;
            statusLabel.Width = 520;
            statusLabel.Height = 94;
            statusLabel.Location = new Point(410, 594);
            statusLabel.Padding = new Padding(12);
            body.Controls.Add(statusLabel);
            body.AutoScrollMinSize = new Size(0, 830);

            nameBox = HiddenTextBox();
            funBox = HiddenNumber(1, 100, 1);
            killsBox = HiddenNumber(0, 9999, 0);
            murderBox = HiddenNumber(0, 16, 0);
            plotBox = HiddenNumber(-999999, 999999, 0);
            roomBox = HiddenNumber(-999999, 999999, 0);
            timeBox = HiddenNumber(0, 999999999, 0);
            routeBox = new ComboBox();
            routeBox.Items.AddRange(new string[] { "Pacifist", "Neutral", "Genocide", "Custom" });
            routeBox.Visible = false;
            body.Controls.Add(routeBox);

            routeBox.SelectedIndexChanged += delegate { ApplyRoutePreset(); };
            lvBox.ValueChanged += delegate
            {
                if (syncLv.Checked)
                {
                    int lv = (int)lvBox.Value;
                    hpBox.Value = HpForLevel(lv);
                    xpBox.Value = XpForLevel(lv);
                    dmgBox.Value = DamageForLevel(lv);
                }
            };
        }

        private Panel MakeFeatureVaultHero()
        {
            Panel panel = new Panel();
            panel.Size = new Size(520, 330);
            panel.BackColor = Color.FromArgb(5, 7, 14);
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                Point[] shell = new Point[]
                {
                    new Point(28, 8),
                    new Point(panel.Width - 1, 0),
                    new Point(panel.Width - 24, panel.Height - 1),
                    new Point(0, panel.Height - 20)
                };
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(12, 18, 36), Color.FromArgb(5, 7, 14), 70f))
                {
                    e.Graphics.FillPolygon(b, shell);
                }
                using (Pen outer = new Pen(Color.FromArgb(230, 54, 151, 255), 3f))
                using (Pen inner = new Pen(Color.FromArgb(180, 255, 63, 220), 2f))
                using (Pen green = new Pen(Color.FromArgb(190, 90, 255, 0), 3f))
                {
                    e.Graphics.DrawPolygon(outer, shell);
                    e.Graphics.DrawLine(inner, 16, panel.Height - 24, panel.Width - 120, panel.Height - 60);
                    e.Graphics.DrawLine(green, 88, 220, panel.Width - 18, 188);
                }
                using (Pen shine = new Pen(Color.FromArgb(150, 255, 255, 255), 1f))
                {
                    e.Graphics.DrawLine(shine, 40, 22, panel.Width - 70, 8);
                }
            };

            Label feature = new Label();
            feature.Text = "FEATURE";
            feature.Font = new Font("Arial Black", 45f, FontStyle.Bold);
            feature.ForeColor = Color.White;
            feature.BackColor = Color.Transparent;
            feature.AutoSize = true;
            feature.Location = new Point(46, 38);
            panel.Controls.Add(feature);

            Label vault = new Label();
            vault.Text = "VAULT";
            vault.Font = new Font("Arial Black", 66f, FontStyle.Bold);
            vault.ForeColor = Color.FromArgb(105, 255, 18);
            vault.BackColor = Color.Transparent;
            vault.AutoSize = true;
            vault.Location = new Point(44, 108);
            panel.Controls.Add(vault);

            Label mod = new Label();
            mod.Text = "100K+ MOD MENU";
            mod.Font = new Font("Arial Black", 24f, FontStyle.Bold);
            mod.ForeColor = Color.FromArgb(255, 214, 0);
            mod.BackColor = Color.Transparent;
            mod.AutoSize = true;
            mod.Location = new Point(62, 242);
            panel.Controls.Add(mod);

            Label hint = new Label();
            hint.Text = "Uncapped stats, routes, hooks, inventory, chaos";
            hint.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            hint.ForeColor = Color.FromArgb(185, 220, 255);
            hint.BackColor = Color.Transparent;
            hint.AutoSize = true;
            hint.Location = new Point(72, 288);
            panel.Controls.Add(hint);

            return panel;
        }

        private Panel MakeBottomActionBar()
        {
            Panel panel = new Panel();
            panel.Size = new Size(520, 74);
            panel.BackColor = Color.FromArgb(5, 7, 14);
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Rectangle r = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(20, 8, 18), Color.FromArgb(5, 8, 16), 0f))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (Pen p = new Pen(Color.FromArgb(190, 255, 63, 92), 2f))
                {
                    e.Graphics.DrawRectangle(p, r);
                }
            };

            Button chaos = MakeWideButton("CHAOS", delegate { OpenChaosConsole(); }, Color.FromArgb(255, 63, 92));
            chaos.Size = new Size(118, 48);
            chaos.Location = new Point(12, 13);
            panel.Controls.Add(chaos);

            Button forge = MakeWideButton("FORGE", delegate { OpenInventoryForge(); }, Color.FromArgb(255, 195, 70));
            forge.Size = new Size(118, 48);
            forge.Location = new Point(142, 13);
            panel.Controls.Add(forge);

            Button god = MakeWideButton("GOD", delegate { ApplyGodPresetFromMain(); }, Color.FromArgb(255, 63, 92));
            god.Size = new Size(118, 48);
            god.Location = new Point(272, 13);
            panel.Controls.Add(god);

            Button random = MakeWideButton("RANDOM", delegate { RandomizeVisibleNumbers(); }, Color.FromArgb(0, 205, 255));
            random.Size = new Size(118, 48);
            random.Location = new Point(390, 13);
            panel.Controls.Add(random);

            return panel;
        }

        private Panel MakeEasyStartPanel()
        {
            Panel panel = new Panel();
            panel.Size = new Size(520, 190);
            panel.BackColor = Color.FromArgb(12, 17, 28);
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(22, 28, 40), Color.FromArgb(8, 10, 16), 90f))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (Pen glow = new Pen(Color.FromArgb(120, 54, 151, 255), 2f))
                {
                    e.Graphics.DrawLine(glow, 0, 0, panel.Width, 0);
                }
                using (Pen border = new Pen(Color.FromArgb(72, 85, 220, 155), 1f))
                {
                    e.Graphics.DrawRectangle(border, r);
                }
            };

            Label title = new Label();
            title.Text = "Preset Tiles";
            title.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(14, 10);
            title.AutoSize = true;
            panel.Controls.Add(title);

            Label hint = new Label();
            hint.Text = "Fast thumbnail-style boosts. Nothing writes until Write Save.";
            hint.Font = new Font("Segoe UI", 8.5f);
            hint.ForeColor = Color.FromArgb(168, 174, 190);
            hint.Location = new Point(112, 13);
            hint.AutoSize = true;
            panel.Controls.Add(hint);

            Button fresh = MakeWideButton("Fresh Start", delegate { ApplySafeStartPreset(); }, Color.FromArgb(0, 205, 255));
            fresh.Size = new Size(158, 50);
            fresh.Location = new Point(14, 48);
            panel.Controls.Add(fresh);

            Button ruins = MakeWideButton("Ruins Boost", delegate { ApplyRuinsBoostPreset(); }, Color.FromArgb(125, 112, 255));
            ruins.Size = new Size(158, 50);
            ruins.Location = new Point(181, 48);
            panel.Controls.Add(ruins);

            Button judgement = MakeWideButton("Judgement", delegate { ApplyJudgementPreset(); }, Color.FromArgb(255, 63, 92));
            judgement.Size = new Size(158, 50);
            judgement.Location = new Point(348, 48);
            panel.Controls.Add(judgement);

            Button sans = MakeWideButton("Sans Practice", delegate { ApplySansPracticePreset(); }, Color.FromArgb(0, 205, 255));
            sans.Size = new Size(158, 50);
            sans.Location = new Point(14, 116);
            panel.Controls.Add(sans);

            Button omega = MakeWideButton("Omega Ready", delegate { ApplyOmegaReadyPreset(); }, Color.FromArgb(85, 220, 155));
            omega.Size = new Size(158, 50);
            omega.Location = new Point(181, 116);
            panel.Controls.Add(omega);

            Button absolute = MakeWideButton("Absolute Max", delegate { UncapPower(); }, Color.FromArgb(255, 195, 70));
            absolute.Size = new Size(158, 50);
            absolute.Location = new Point(348, 116);
            panel.Controls.Add(absolute);

            tips.SetToolTip(fresh, "Set a clean LV1 pacifist-style save in memory.");
            tips.SetToolTip(ruins, "Set a stronger early-game build in memory.");
            tips.SetToolTip(judgement, "Set late-game judgement-style room and power values in memory.");
            tips.SetToolTip(sans, "Set a high HP and damage practice build in memory.");
            tips.SetToolTip(omega, "Set a near-final battle-ready build in memory.");
            tips.SetToolTip(absolute, "Max out the big player number boxes.");
            return panel;
        }

        private void OpenPlayerGuide()
        {
            using (PlayerGuideForm f = new PlayerGuideForm())
            {
                f.ShowDialog(this);
            }
        }

        private bool EnsureEasySaveShell(string title, Color accent)
        {
            if (model.HasFile0)
            {
                return true;
            }
            if (!ProDialog.ShowConfirm(this, title, "No file0/file9 is loaded yet.\r\n\r\nCreate a fresh editable save shell in memory? It will not touch disk until Write Save.", accent))
            {
                return false;
            }
            model.CreateShell();
            PullToUi();
            return true;
        }

        private void ApplySafeStartPreset()
        {
            if (!EnsureEasySaveShell("CREATE SAFE START", Color.FromArgb(85, 220, 155)))
            {
                return;
            }
            lvBox.Value = 1;
            hpBox.Value = 20;
            xpBox.Value = 0;
            goldBox.Value = 0;
            dmgBox.Value = 10;
            killsBox.Value = 0;
            murderBox.Value = 0;
            plotBox.Value = 0;
            roomBox.Value = 4;
            timeBox.Value = 0;
            syncLv.Checked = true;
            SetRouteToggleStates(0);
            routeBox.SelectedIndex = 0;
            statusLabel.Text = "Safe Start is ready in memory. Press Write Save when you want to commit it.";
        }

        private void ApplyRuinsBoostPreset()
        {
            if (!EnsureEasySaveShell("RUINS BOOST", Color.FromArgb(125, 112, 255)))
            {
                return;
            }
            syncLv.Checked = false;
            SetRouteToggleStates(3);
            routeBox.SelectedIndex = 3;
            lvBox.Value = 5;
            hpBox.Value = 60;
            xpBox.Value = 250;
            goldBox.Value = 500;
            dmgBox.Value = 25;
            roomBox.Value = 4;
            statusLabel.Text = "Ruins Boost is ready in memory. Press Write Save when you want to commit it.";
        }

        private void ApplyJudgementPreset()
        {
            if (!EnsureEasySaveShell("JUDGEMENT PRESET", Color.FromArgb(255, 63, 92)))
            {
                return;
            }
            syncLv.Checked = false;
            SetRouteToggleStates(1);
            routeBox.SelectedIndex = 1;
            lvBox.Value = 19;
            hpBox.Value = 92;
            xpBox.Value = 50000;
            goldBox.Value = 9999;
            dmgBox.Value = 99;
            plotBox.Value = 999;
            roomBox.Value = 231;
            statusLabel.Text = "Judgement preset is ready in memory. Press Write Save when you want to commit it.";
        }

        private void ApplySansPracticePreset()
        {
            if (!EnsureEasySaveShell("SANS PRACTICE", Color.FromArgb(0, 205, 255)))
            {
                return;
            }
            syncLv.Checked = false;
            SetRouteToggleStates(2);
            routeBox.SelectedIndex = 2;
            lvBox.Value = 19;
            hpBox.Value = 999;
            xpBox.Value = 50000;
            goldBox.Value = 9999;
            dmgBox.Value = 999;
            killsBox.Value = 100;
            murderBox.Value = 16;
            roomBox.Value = 231;
            statusLabel.Text = "Sans Practice is ready in memory. Press Write Save when you want to commit it.";
        }

        private void ApplyOmegaReadyPreset()
        {
            if (!EnsureEasySaveShell("OMEGA READY", Color.FromArgb(85, 220, 155)))
            {
                return;
            }
            syncLv.Checked = false;
            SetRouteToggleStates(1);
            routeBox.SelectedIndex = 1;
            lvBox.Value = 17;
            hpBox.Value = 250;
            xpBox.Value = 25000;
            goldBox.Value = 5000;
            dmgBox.Value = 150;
            plotBox.Value = 200;
            roomBox.Value = 220;
            statusLabel.Text = "Omega Ready is ready in memory. Press Write Save when you want to commit it.";
        }

        private NumericUpDown AddMainNumber(Control parent, string labelText, int x, int y)
        {
            Color accent = Color.FromArgb(54, 151, 255);
            if (labelText == "HP") accent = Color.FromArgb(255, 63, 92);
            if (labelText == "EXP") accent = Color.FromArgb(255, 195, 70);
            if (labelText == "GOLD") accent = Color.FromArgb(85, 220, 155);
            if (labelText == "DMG") accent = Color.FromArgb(255, 120, 76);

            Panel card = new Panel();
            card.Location = new Point(x, y);
            card.Size = new Size(350, 92);
            card.BackColor = Color.FromArgb(10, 13, 22);
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                Point[] shell = new Point[]
                {
                    new Point(12, 0),
                    new Point(card.Width - 1, 0),
                    new Point(card.Width - 12, card.Height - 1),
                    new Point(0, card.Height - 1)
                };
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(24, 29, 42), Color.FromArgb(7, 9, 15), 90f))
                {
                    e.Graphics.FillPolygon(b, shell);
                }
                using (SolidBrush wash = new SolidBrush(Color.FromArgb(22, accent)))
                {
                    e.Graphics.FillPolygon(wash, shell);
                }
                using (Pen glow = new Pen(Color.FromArgb(230, accent), 3f))
                {
                    e.Graphics.DrawPolygon(glow, shell);
                }
                using (Pen shine = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                {
                    e.Graphics.DrawLine(shine, 20, 8, card.Width - 40, 3);
                }
            };
            parent.Controls.Add(card);

            Label label = new Label();
            label.Text = labelText;
            label.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
            label.ForeColor = accent;
            label.Location = new Point(20, 10);
            label.AutoSize = true;
            card.Controls.Add(label);

            NumericUpDown box = new NumericUpDown();
            box.Minimum = 0;
            box.Maximum = PowerMax;
            box.Value = 0;
            box.ThousandsSeparator = true;
            box.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
            box.Location = new Point(20, 42);
            box.Width = 306;
            box.Height = 40;
            box.BackColor = Color.FromArgb(6, 6, 9);
            box.ForeColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.TextAlign = HorizontalAlignment.Right;
            card.Controls.Add(box);
            return box;
        }

        private TextBox HiddenTextBox()
        {
            TextBox box = new TextBox();
            box.Visible = false;
            return box;
        }

        private NumericUpDown HiddenNumber(int min, int max, int value)
        {
            NumericUpDown box = new NumericUpDown();
            box.Minimum = min;
            box.Maximum = max;
            box.Value = value;
            box.Visible = false;
            return box;
        }

        private CheckBox MakeToggle(string text, bool isChecked, Color accent)
        {
            CheckBox toggle = new CheckBox();
            toggle.Appearance = Appearance.Button;
            toggle.Text = text;
            toggle.Checked = isChecked;
            toggle.Width = 166;
            toggle.Height = 52;
            toggle.TextAlign = ContentAlignment.MiddleCenter;
            toggle.FlatStyle = FlatStyle.Flat;
            toggle.FlatAppearance.BorderSize = 2;
            toggle.FlatAppearance.BorderColor = accent;
            toggle.BackColor = Color.FromArgb(8, 10, 18);
            toggle.ForeColor = Color.White;
            toggle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            toggle.Cursor = Cursors.Hand;
            toggle.CheckedChanged += delegate
            {
                toggle.Text = (toggle.Checked ? "ON  " : "OFF  ") + text;
                toggle.BackColor = toggle.Checked ? Color.FromArgb(Math.Min(255, accent.R / 2 + 38), Math.Min(255, accent.G / 2 + 32), Math.Min(255, accent.B / 2 + 38)) : Color.FromArgb(8, 10, 18);
                toggle.ForeColor = toggle.Checked ? Color.White : Color.FromArgb(200, 204, 214);
            };
            toggle.MouseEnter += delegate
            {
                if (!toggle.Checked)
                {
                    toggle.BackColor = Color.FromArgb(20, 25, 38);
                }
            };
            toggle.MouseLeave += delegate
            {
                if (!toggle.Checked)
                {
                    toggle.BackColor = Color.FromArgb(8, 10, 18);
                }
            };
            toggle.Text = (toggle.Checked ? "ON  " : "OFF  ") + text;
            toggle.BackColor = toggle.Checked ? Color.FromArgb(Math.Min(255, accent.R / 2 + 38), Math.Min(255, accent.G / 2 + 32), Math.Min(255, accent.B / 2 + 38)) : Color.FromArgb(8, 10, 18);
            toggle.ForeColor = toggle.Checked ? Color.White : Color.FromArgb(200, 204, 214);
            return toggle;
        }

        private void RouteToggleChanged(int routeIndex, CheckBox source)
        {
            if (suppressToggleEvents || !source.Checked)
            {
                return;
            }
            SetRouteToggleStates(routeIndex);
            bool same = routeBox.SelectedIndex == routeIndex;
            routeBox.SelectedIndex = routeIndex;
            if (same)
            {
                ApplyRoutePreset();
            }
        }

        private void SetRouteToggleStates(int routeIndex)
        {
            suppressToggleEvents = true;
            if (pacifistToggle != null) pacifistToggle.Checked = routeIndex == 0;
            if (neutralToggle != null) neutralToggle.Checked = routeIndex == 1;
            if (genocideToggle != null) genocideToggle.Checked = routeIndex == 2;
            if (customToggle != null) customToggle.Checked = routeIndex == 3;
            suppressToggleEvents = false;
        }

        private void RandomizeVisibleNumbers()
        {
            Random r = new Random();
            lvBox.Value = r.Next(1, 1000001);
            hpBox.Value = r.Next(1, PowerMax + 1);
            xpBox.Value = r.Next(0, PowerMax + 1);
            goldBox.Value = r.Next(0, PowerMax + 1);
            dmgBox.Value = r.Next(0, PowerMax + 1);
            syncLv.Checked = false;
            statusLabel.Text = "Randomized the five visible number boxes. Press Write Save to commit.";
        }

        private void ApplyGodPresetFromMain()
        {
            lvBox.Value = PowerMax;
            hpBox.Value = PowerMax;
            xpBox.Value = PowerMax;
            goldBox.Value = PowerMax;
            dmgBox.Value = PowerMax;
            syncLv.Checked = false;
            statusLabel.Text = "God Preset is loaded into the number boxes. Press Write Save to commit.";
        }

        private void ToggleGameWatch()
        {
            if (watchGameToggle == null)
            {
                return;
            }

            if (watchGameToggle.Checked)
            {
                gameWasRunning = false;
                gameWatchBackedUp = false;
                lastAutoApply = DateTime.MinValue;
                gameWatchTimer.Start();
                statusLabel.Text = "Watch Game is ON. Waiting for Undertale to launch, then the save will be auto-applied.";
                WatchGameTick();
            }
            else
            {
                gameWatchTimer.Stop();
                gameWasRunning = false;
                gameWatchBackedUp = false;
                statusLabel.Text = "Watch Game is OFF.";
            }
        }

        private void WatchGameTick()
        {
            if (watchGameToggle == null || !watchGameToggle.Checked)
            {
                return;
            }

            string processName = FindUndertaleProcess();
            if (processName == null)
            {
                if (gameWasRunning)
                {
                    statusLabel.Text = "Undertale closed. Watch Game is still ON and waiting for the next launch.";
                }
                else
                {
                    statusLabel.Text = "Watch Game is ON. Waiting for Undertale to launch...";
                }
                gameWasRunning = false;
                gameWatchBackedUp = false;
                return;
            }

            if (!model.HasFile0)
            {
                statusLabel.Text = "Detected " + processName + ", but no file0/file9 is loaded. Save once in Undertale or use New File0 Shell.";
                gameWasRunning = true;
                return;
            }

            if (!gameWasRunning || (DateTime.Now - lastAutoApply).TotalSeconds >= 5)
            {
                try
                {
                    PushFromUi();
                    string backup = model.WriteLive(mirrorFile9.Checked, !gameWatchBackedUp);
                    gameWatchBackedUp = true;
                    lastAutoApply = DateTime.Now;
                    statusLabel.Text = "Detected " + processName + ". Auto-applied save at " + lastAutoApply.ToLongTimeString() + ". Restart/reload the save screen if the game already cached old values.";
                    if (!string.IsNullOrEmpty(backup))
                    {
                        statusLabel.Text += "\r\nBackup created: " + backup;
                    }
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "Watch Game saw " + processName + ", but auto-apply failed: " + ex.Message;
                }
            }

            gameWasRunning = true;
        }

        private string FindUndertaleProcess()
        {
            Process[] processes = Process.GetProcesses();
            for (int i = 0; i < processes.Length; i++)
            {
                Process p = processes[i];
                try
                {
                    string name = (p.ProcessName ?? "").ToLowerInvariant();
                    string title = "";
                    try
                    {
                        title = (p.MainWindowTitle ?? "").ToLowerInvariant();
                    }
                    catch
                    {
                        title = "";
                    }

                    if (name.Contains("undertale") || title.Contains("undertale"))
                    {
                        return p.ProcessName;
                    }
                    if (name == "runner" && title.Contains("undertale"))
                    {
                        return p.ProcessName;
                    }
                }
                catch
                {
                }
                finally
                {
                    p.Dispose();
                }
            }
            return null;
        }

        private int HpForLevel(int lv)
        {
            if (lv >= 0 && lv < hpForLv.Length)
            {
                return hpForLv[lv];
            }
            long hp = 16L + ((long)lv * 4L);
            if (hp > PowerMax) return PowerMax;
            if (hp < 1) return 1;
            return (int)hp;
        }

        private int XpForLevel(int lv)
        {
            if (lv >= 0 && lv < xpForLv.Length)
            {
                return xpForLv[lv];
            }
            long xp = 99999L + (((long)lv - 20L) * 50000L);
            if (xp > PowerMax) return PowerMax;
            if (xp < 0) return 0;
            return (int)xp;
        }

        private int DamageForLevel(int lv)
        {
            long damage = lv == 20 ? 30L : 8L + ((long)lv * 2L);
            if (damage > PowerMax) return PowerMax;
            if (damage < 0) return 0;
            return (int)damage;
        }

        private Panel MakePanel(string titleText)
        {
            Panel p = new Panel();
            p.BackColor = Color.FromArgb(18, 18, 23);
            p.Margin = new Padding(8);
            p.Padding = new Padding(14);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                Rectangle r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(24, 24, 31), Color.FromArgb(12, 12, 16), 90f))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (Pen pen = new Pen(Color.FromArgb(76, 76, 90), 1f))
                {
                    e.Graphics.DrawRectangle(pen, r);
                }
            };
            Label title = new Label();
            title.Text = titleText;
            title.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(20, 20);
            title.AutoSize = true;
            p.Controls.Add(title);
            return p;
        }

        private TextBox AddText(Control parent, string label, int y, string value)
        {
            AddLabel(parent, label, y);
            TextBox box = new TextBox();
            box.Text = value;
            box.Location = new Point(22, y + 24);
            box.Width = 300;
            box.Height = 28;
            box.BackColor = Color.FromArgb(7, 7, 10);
            box.ForeColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(box);
            return box;
        }

        private ComboBox AddCombo(Control parent, string label, int y, string[] values)
        {
            AddLabel(parent, label, y);
            ComboBox box = new ComboBox();
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Items.AddRange(values);
            box.Location = new Point(22, y + 24);
            box.Width = 300;
            box.BackColor = Color.FromArgb(7, 7, 10);
            box.ForeColor = Color.White;
            parent.Controls.Add(box);
            return box;
        }

        private NumericUpDown AddNumber(Control parent, string label, int y, int min, int max, int value)
        {
            AddLabel(parent, label, y);
            NumericUpDown box = new NumericUpDown();
            box.Minimum = min;
            box.Maximum = max;
            box.Value = value;
            box.Location = new Point(22, y + 24);
            box.Width = 300;
            box.Height = 28;
            box.BackColor = Color.FromArgb(7, 7, 10);
            box.ForeColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(box);
            return box;
        }

        private void AddLabel(Control parent, string text, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(170, 170, 182);
            label.Location = new Point(22, y);
            label.AutoSize = true;
            parent.Controls.Add(label);
        }

        private Button MakeButton(string text, Action click, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 112;
            b.Height = 40;
            b.Margin = new Padding(4);
            StyleButton(b, accent);
            b.Click += delegate { click(); };
            return b;
        }

        private Button MakeWideButton(string text, Action click, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 300;
            b.Height = 46;
            StyleButton(b, accent);
            b.Click += delegate { click(); };
            return b;
        }

        private void StyleButton(Button b, Color accent)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(Math.Min(255, accent.R / 2 + 38), Math.Min(255, accent.G / 2 + 32), Math.Min(255, accent.B / 2 + 38));
            b.BackColor = Color.FromArgb(7, 10, 18);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.8f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(7, 10, 18);
            };
        }

        private void LoadLive()
        {
            try
            {
                model.LoadLive();
                PullToUi();
                savePathLabel.Text = model.SaveDir;
                statusLabel.Text = model.HasFile0
                    ? "Loaded " + model.LoadedSource + ". Inventory Forge is ready."
                    : "Loaded undertale.ini. No file0/file9 found yet.";
            }
            catch (Exception ex)
            {
                ProDialog.ShowInfo(this, "LOAD FAILED", ex.Message, Color.FromArgb(255, 195, 70));
            }
        }

        private void PullToUi()
        {
            funBox.Value = model.Fun();
            if (!model.HasFile0)
            {
                nameBox.Text = "";
                lvBox.Value = 1;
                hpBox.Value = 20;
                xpBox.Value = 0;
                goldBox.Value = 0;
                dmgBox.Value = 10;
                killsBox.Value = 0;
                murderBox.Value = 0;
                plotBox.Value = 0;
                roomBox.Value = 0;
                timeBox.Value = 0;
                routeBox.SelectedIndex = 3;
                SetRouteToggleStates(3);
                inventoryButton.Enabled = false;
                return;
            }

            inventoryButton.Enabled = true;
            nameBox.Text = model.Get(SaveModel.Name);
            lvBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Lv, 1), 1, PowerMax);
            hpBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.MaxHp, 20), 1, PowerMax);
            xpBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Xp, 0), 0, PowerMax);
            goldBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Gold, 0), 0, PowerMax);
            dmgBox.Value = SaveModel.Clamp(model.DamagePower(), 0, PowerMax);
            killsBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Kills, 0), 0, 9999);
            murderBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Flags + 26, 0), 0, 16);
            plotBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Plot, 0), -999999, 999999);
            roomBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Room, 0), -999999, 999999);
            timeBox.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Time, 0), 0, 999999999);
            SetRouteReadout();
        }

        private void PushFromUi()
        {
            model.SetFun((int)funBox.Value);
            if (!model.HasFile0)
            {
                return;
            }
            model.Set(SaveModel.Name, string.IsNullOrWhiteSpace(nameBox.Text) ? "CHARA" : nameBox.Text.Trim());
            model.SetNumber(SaveModel.Lv, (int)lvBox.Value);
            model.SetNumber(SaveModel.MaxHp, (int)hpBox.Value);
            model.SetNumber(SaveModel.MaxEn, (int)hpBox.Value);
            model.SetDamagePower((int)dmgBox.Value);
            model.SetNumber(SaveModel.Xp, (int)xpBox.Value);
            model.SetNumber(SaveModel.Gold, (int)goldBox.Value);
            model.SetNumber(SaveModel.Kills, (int)killsBox.Value);
            model.SetNumber(SaveModel.Flags + 26, (int)murderBox.Value);
            model.SetNumber(SaveModel.Plot, (int)plotBox.Value);
            model.SetNumber(SaveModel.Room, (int)roomBox.Value);
            model.SetNumber(SaveModel.Time, (int)timeBox.Value);
        }

        private void SetRouteReadout()
        {
            suppressRoutePreset = true;
            int murder = (int)murderBox.Value;
            if (murder >= 16)
            {
                routeBox.SelectedIndex = 2;
                SetRouteToggleStates(2);
            }
            else if (murder == 0 && (int)lvBox.Value == 1 && (int)killsBox.Value == 0 && (int)xpBox.Value == 0)
            {
                routeBox.SelectedIndex = 0;
                SetRouteToggleStates(0);
            }
            else if (murder == 0)
            {
                routeBox.SelectedIndex = 1;
                SetRouteToggleStates(1);
            }
            else
            {
                routeBox.SelectedIndex = 3;
                SetRouteToggleStates(3);
            }
            suppressRoutePreset = false;
        }

        private void ApplyRoutePreset()
        {
            if (suppressRoutePreset || routeBox.SelectedIndex < 0 || !model.HasFile0)
            {
                return;
            }
            string route = routeBox.SelectedItem.ToString();
            if (route == "Pacifist")
            {
                lvBox.Value = 1;
                hpBox.Value = 20;
                dmgBox.Value = 10;
                xpBox.Value = 0;
                killsBox.Value = 0;
                murderBox.Value = 0;
            }
            else if (route == "Neutral")
            {
                int lv = Math.Max(2, (int)lvBox.Value);
                lvBox.Value = lv;
                hpBox.Value = HpForLevel(lv);
                dmgBox.Value = DamageForLevel(lv);
                xpBox.Value = Math.Max(10, XpForLevel(lv));
                killsBox.Value = Math.Max(1, (int)killsBox.Value);
                murderBox.Value = 0;
            }
            else if (route == "Genocide")
            {
                lvBox.Value = 20;
                hpBox.Value = 99;
                dmgBox.Value = 30;
                xpBox.Value = 99999;
                killsBox.Value = Math.Max(99, (int)killsBox.Value);
                murderBox.Value = 16;
            }
        }

        private void UncapPower()
        {
            lvBox.Value = PowerMax;
            hpBox.Value = PowerMax;
            xpBox.Value = PowerMax;
            goldBox.Value = PowerMax;
            dmgBox.Value = PowerMax;
            syncLv.Checked = false;
            statusLabel.Text = "Uncapped LV, HP, EXP, gold, and DMG are set to 999,999,999 in memory.";
        }

        private void OpenInventoryForge()
        {
            if (!model.HasFile0)
            {
                ProDialog.ShowInfo(this, "NO FILE0", "Create a file0 shell, import file0, or save once in Undertale first.", Color.FromArgb(255, 195, 70));
                return;
            }
            PushFromUi();
            using (InventoryForgeForm f = new InventoryForgeForm(model))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    statusLabel.Text = "Inventory slots updated in memory. Press Write Save to commit.";
                }
            }
        }

        private void OpenFeatureVault()
        {
            if (!model.HasFile0)
            {
                if (!ProDialog.ShowConfirm(this, "CREATE FEATURE FILE0", "No file0/file9 is loaded yet.\r\n\r\nCreate a fresh player save shell so Feature Vault has something to edit?", Color.FromArgb(54, 151, 255)))
                {
                    return;
                }
                model.CreateShell();
                PullToUi();
            }

            PushFromUi();
            using (FeatureVaultForm f = new FeatureVaultForm(model))
            {
                f.ShowDialog(this);
                PullToUi();
                if (f.AppliedCount > 0)
                {
                    statusLabel.Text = "Feature Vault applied " + f.AppliedCount.ToString() + " feature" + (f.AppliedCount == 1 ? "" : "s") + " in memory. Press Write Save to commit.";
                }
                else
                {
                    statusLabel.Text = "Feature Vault closed without changes.";
                }
            }
        }

        private void OpenGameJoltModHub()
        {
            using (GameJoltModHubForm f = new GameJoltModHubForm())
            {
                f.ShowDialog(this);
                if (!string.IsNullOrEmpty(f.LastStatus))
                {
                    statusLabel.Text = f.LastStatus;
                }
            }
        }

        private void CheckGitHubUpdateNow()
        {
            statusLabel.Text = "Checking GitHub for a newer EXE...";
            SelfUpdater.CheckForUpdates(this, delegate(string text) { statusLabel.Text = text; }, true);
        }

        private void OpenUpdateSettings()
        {
            SelfUpdater.OpenConfig();
            statusLabel.Text = "Opened update.ini. Add your GitHub owner/repo or direct latest-release EXE URL.";
        }

        private void OpenChaosConsole()
        {
            if (!model.HasFile0)
            {
                if (!ProDialog.ShowConfirm(this, "CREATE CHAOS FILE0", "No file0/file9 is loaded yet.\r\n\r\nCreate a fresh player save shell so Chaos Console has something to edit?", Color.FromArgb(255, 63, 92)))
                {
                    return;
                }
                model.CreateShell();
                PullToUi();
            }

            PushFromUi();
            using (ChaosConsoleForm f = new ChaosConsoleForm(model))
            {
                f.ShowDialog(this);
                PullToUi();
                statusLabel.Text = "Chaos Console changed the save in memory. Press Write Save to commit.";
            }
        }

        private void NewShell()
        {
            if (ProDialog.ShowConfirm(this, "NEW FILE0 SHELL", "Create a fresh editable file0 shell in memory?\r\n\r\nThis will not touch the real save folder until Write Save.", Color.FromArgb(125, 112, 255)))
            {
                model.CreateShell();
                PullToUi();
                statusLabel.Text = "New file0 shell ready. Edit it, then Write Save.";
            }
        }

        private void ImportFile0()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Import Undertale file0 or file9";
                d.Filter = "Undertale save files|file0;file9;*.*";
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    model.LoadFile0Text(File.ReadAllText(d.FileName), Path.GetFileName(d.FileName));
                    PullToUi();
                    statusLabel.Text = "Imported " + Path.GetFileName(d.FileName) + ".";
                }
            }
        }

        private void ChooseFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Choose an Undertale save folder";
                d.SelectedPath = Directory.Exists(model.SaveDir) ? model.SaveDir : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    model.SaveDir = d.SelectedPath;
                    LoadLive();
                }
            }
        }

        private void OpenFolder()
        {
            Directory.CreateDirectory(model.SaveDir);
            Process.Start(model.SaveDir);
        }

        private void InstallLiveHookMod()
        {
            string cli = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "UTMT_CLI_v0.8.4.1-Windows", "UndertaleModCli.exe");
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InstallCodexLiveHook.csx");
            if (!File.Exists(script))
            {
                script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "undertale-fun-route-editor", "InstallCodexLiveHook.csx");
            }
            if (!File.Exists(cli))
            {
                ProDialog.ShowInfo(this, "HOOK INSTALLER", "Could not find UndertaleModCli.exe at:\r\n" + cli, Color.FromArgb(255, 195, 70));
                return;
            }
            if (!File.Exists(script))
            {
                ProDialog.ShowInfo(this, "HOOK INSTALLER", "Could not find InstallCodexLiveHook.csx beside the app files.", Color.FromArgb(255, 195, 70));
                return;
            }

            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Choose Undertale data.win to patch";
                d.Filter = "GameMaker data files|data.win;*.win|All files|*.*";
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (!ProDialog.ShowConfirm(this, "INSTALL LIVE HOOK", "Patch this data.win so Undertale reads codex_live.ini while running?\r\n\r\nA backup of the original data.win will be created first.", Color.FromArgb(255, 195, 70)))
                {
                    return;
                }

                string input = d.FileName;
                string dir = Path.GetDirectoryName(input);
                string temp = Path.Combine(dir, "data.codexlive.tmp.win");
                string backup = Path.Combine(dir, "data.win.codex-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));

                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }

                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = cli;
                    psi.Arguments = "load \"" + input + "\" -s \"" + script + "\" -o \"" + temp + "\"";
                    psi.WorkingDirectory = Path.GetDirectoryName(cli);
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                    using (Process p = Process.Start(psi))
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        string error = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                        if (p.ExitCode != 0 || !File.Exists(temp))
                        {
                            ProDialog.ShowInfo(this, "HOOK FAILED", "UndertaleModCli did not create a patched file.\r\n\r\n" + output + "\r\n" + error, Color.FromArgb(255, 195, 70));
                            return;
                        }
                    }

                    File.Copy(input, backup, false);
                    File.Copy(temp, input, true);
                    File.Delete(temp);
                    PushFromUi();
                    model.WriteLiveConfig(true);
                    ProDialog.ShowInfo(this, "LIVE HOOK V2 INSTALLED", "Patched data.win with the stronger DMG hook and wrote codex_live.ini.\r\n\r\nBackup:\r\n" + backup + "\r\n\r\nClose Undertale, then launch it from this patched game folder.", Color.FromArgb(85, 220, 155));
                }
                catch (Exception ex)
                {
                    ProDialog.ShowInfo(this, "HOOK FAILED", ex.Message, Color.FromArgb(255, 195, 70));
                }
            }
        }

        private void RandomFun()
        {
            Random r = new Random();
            funBox.Value = r.Next(1, 101);
        }

        private void WriteSave()
        {
            PushFromUi();
            if (!ProDialog.ShowConfirm(this, "COMMIT SAVE", "Write these values into the Undertale save folder now?\r\n\r\nA timestamped backup is created first.", Color.FromArgb(255, 63, 92)))
            {
                return;
            }
            try
            {
                string backup = model.WriteLive(mirrorFile9.Checked);
                string msg = "Save write complete.";
                if (!string.IsNullOrEmpty(backup))
                {
                    msg += "\r\n\r\nBackup:\r\n" + backup;
                }
                ProDialog.ShowInfo(this, "DONE", msg, Color.FromArgb(85, 220, 155));
                LoadLive();
            }
            catch (Exception ex)
            {
                ProDialog.ShowInfo(this, "WRITE FAILED", ex.Message, Color.FromArgb(255, 195, 70));
            }
        }
    }

    internal sealed class PlayerGuideForm : Form
    {
        public PlayerGuideForm()
        {
            Text = "Player Guide";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 560);
            Size = new Size(820, 600);
            BackColor = Color.FromArgb(4, 6, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            BuildUi();
        }

        private void BuildUi()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 110;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "PLAYER GUIDE";
            title.Font = new Font("Segoe UI Semibold", 25f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.Location = new Point(28, 18);
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Simple path for players: load, pick, write. Backups happen before live writes.";
            sub.Font = new Font("Segoe UI", 9.5f);
            sub.ForeColor = Color.FromArgb(205, 210, 224);
            sub.AutoSize = true;
            sub.Location = new Point(32, 68);
            header.Controls.Add(sub);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.FromArgb(4, 6, 10);
            body.Padding = new Padding(28, 24, 28, 24);
            Controls.Add(body);

            AddStep(body, "1", "Load Save", "Use Load Save for the normal Undertale folder. Use Find Folder only if your save is somewhere else.", Color.FromArgb(54, 151, 255), 28, 24);
            AddStep(body, "2", "Pick What You Want", "Type LEVEL, HP, EXP, GOLD, or DMG numbers. Use route buttons and presets for fast changes.", Color.FromArgb(85, 220, 155), 28, 118);
            AddStep(body, "3", "Write Save", "Nothing touches the save until Write Save. The app makes a backup first, then commits your values.", Color.FromArgb(255, 63, 92), 28, 212);
            AddStep(body, "4", "Use Advanced Tools", "Inventory Forge, Chaos Console, Live Hook, and GameJolt Mod Hub are there when you want more control.", Color.FromArgb(255, 195, 70), 28, 306);

            Panel tip = MakeTipPanel();
            tip.Location = new Point(420, 24);
            body.Controls.Add(tip);

            Button done = MakeGuideButton("Done", Color.FromArgb(85, 220, 155));
            done.Location = new Point(636, 408);
            done.Click += delegate { Close(); };
            body.Controls.Add(done);
        }

        private void AddStep(Control parent, string number, string title, string text, Color accent, int x, int y)
        {
            Panel card = new Panel();
            card.Location = new Point(x, y);
            card.Size = new Size(360, 76);
            card.BackColor = Color.FromArgb(12, 17, 28);
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(21, 26, 38), Color.FromArgb(7, 9, 15), 90f))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (SolidBrush wash = new SolidBrush(Color.FromArgb(16, accent)))
                {
                    e.Graphics.FillRectangle(wash, 0, 0, card.Width, card.Height);
                }
                using (Pen line = new Pen(accent, 2f))
                {
                    e.Graphics.DrawLine(line, 0, 0, card.Width, 0);
                }
                using (Pen border = new Pen(Color.FromArgb(58, 66, 82), 1f))
                {
                    e.Graphics.DrawRectangle(border, r);
                }
            };
            parent.Controls.Add(card);

            Label badge = new Label();
            badge.Text = number;
            badge.Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold);
            badge.ForeColor = accent;
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.Location = new Point(12, 14);
            badge.Size = new Size(44, 44);
            card.Controls.Add(badge);

            Label heading = new Label();
            heading.Text = title;
            heading.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            heading.ForeColor = Color.White;
            heading.AutoSize = true;
            heading.Location = new Point(66, 12);
            card.Controls.Add(heading);

            Label copy = new Label();
            copy.Text = text;
            copy.Font = new Font("Segoe UI", 8.6f);
            copy.ForeColor = Color.FromArgb(180, 186, 202);
            copy.Location = new Point(68, 36);
            copy.Size = new Size(270, 34);
            card.Controls.Add(copy);
        }

        private Panel MakeTipPanel()
        {
            Panel panel = new Panel();
            panel.Size = new Size(330, 360);
            panel.BackColor = Color.FromArgb(10, 13, 22);
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Rectangle r = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(18, 23, 36), Color.FromArgb(7, 9, 15), 90f))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (Pen border = new Pen(Color.FromArgb(72, 85, 220, 155), 1f))
                {
                    e.Graphics.DrawRectangle(border, r);
                }
            };

            Label title = new Label();
            title.Text = "Player Tips";
            title.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(18, 18);
            title.AutoSize = true;
            panel.Controls.Add(title);

            Label copy = new Label();
            copy.Text =
                "- Safe Start makes a clean LV1 save in memory.\r\n\r\n" +
                "- Make Me OP maxes the big player boxes.\r\n\r\n" +
                "- Sync LV Stats fills normal HP, EXP, and DMG for the level.\r\n\r\n" +
                "- Mirror file9 is recommended for Undertale saves.\r\n\r\n" +
                "- Advanced tools are optional. You can ignore them and still use the app.";
            copy.Font = new Font("Segoe UI", 9.2f);
            copy.ForeColor = Color.FromArgb(210, 216, 230);
            copy.Location = new Point(20, 60);
            copy.Size = new Size(290, 260);
            panel.Controls.Add(copy);

            return panel;
        }

        private Button MakeGuideButton(string text, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 114;
            b.Height = 40;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(17, 19, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(17, 19, 28);
            };
            return b;
        }
    }

    internal sealed class GameJoltModInfo
    {
        public string Title;
        public string Url;
        public string Slug;

        public override string ToString()
        {
            return Title;
        }
    }

    internal sealed class GameJoltModHubForm : Form
    {
        private const string CatalogUrl = "https://ssr.gamejolt.net/games/best/tag-undertale";
        private const string BrowserUrl = "https://gamejolt.com/games/best/tag-undertale";

        private readonly List<GameJoltModInfo> allMods = new List<GameJoltModInfo>();
        private readonly ListBox modList = new ListBox();
        private readonly TextBox searchBox = new TextBox();
        private readonly TextBox detailsBox = new TextBox();
        private readonly Label countLabel = new Label();
        private readonly Label statusLabel = new Label();
        private readonly string libraryRoot;
        private string lastStatus = "";

        public string LastStatus
        {
            get { return lastStatus; }
        }

        public GameJoltModHubForm()
        {
            libraryRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "UndertaleGameJoltMods");
            Text = "GameJolt Mod Hub";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1060, 680);
            Size = new Size(1120, 740);
            BackColor = Color.FromArgb(4, 6, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            BuildUi();
            Shown += delegate { RefreshCatalog(); };
        }

        private void BuildUi()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 108;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "GAMEJOLT MOD HUB";
            title.Font = new Font("Segoe UI Semibold", 25f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.Location = new Point(28, 18);
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Browse Undertale-tagged GameJolt projects, then install a downloaded ZIP or folder.";
            sub.Font = new Font("Segoe UI", 9.5f);
            sub.ForeColor = Color.FromArgb(215, 218, 228);
            sub.AutoSize = true;
            sub.Location = new Point(32, 66);
            header.Controls.Add(sub);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(28, 24, 28, 28);
            body.BackColor = Color.FromArgb(4, 6, 10);
            Controls.Add(body);

            Label searchLabel = new Label();
            searchLabel.Text = "Search";
            searchLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            searchLabel.ForeColor = Color.FromArgb(190, 196, 210);
            searchLabel.AutoSize = true;
            searchLabel.Location = new Point(30, 24);
            body.Controls.Add(searchLabel);

            searchBox.Location = new Point(30, 48);
            searchBox.Size = new Size(438, 30);
            searchBox.BackColor = Color.FromArgb(8, 11, 18);
            searchBox.ForeColor = Color.White;
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.TextChanged += delegate { ApplyFilter(); };
            body.Controls.Add(searchBox);

            countLabel.Text = "";
            countLabel.ForeColor = Color.FromArgb(168, 174, 190);
            countLabel.AutoSize = true;
            countLabel.Location = new Point(30, 84);
            body.Controls.Add(countLabel);

            modList.Location = new Point(30, 112);
            modList.Size = new Size(438, 438);
            modList.BackColor = Color.FromArgb(8, 10, 16);
            modList.ForeColor = Color.White;
            modList.BorderStyle = BorderStyle.FixedSingle;
            modList.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            modList.SelectedIndexChanged += delegate { UpdateDetails(); };
            body.Controls.Add(modList);

            detailsBox.Location = new Point(498, 48);
            detailsBox.Size = new Size(560, 190);
            detailsBox.Multiline = true;
            detailsBox.ReadOnly = true;
            detailsBox.ScrollBars = ScrollBars.Vertical;
            detailsBox.BackColor = Color.FromArgb(8, 10, 16);
            detailsBox.ForeColor = Color.FromArgb(226, 228, 236);
            detailsBox.BorderStyle = BorderStyle.FixedSingle;
            detailsBox.Font = new Font("Segoe UI", 9.3f);
            body.Controls.Add(detailsBox);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Location = new Point(498, 260);
            buttons.Size = new Size(560, 188);
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;
            buttons.BackColor = Color.Transparent;
            body.Controls.Add(buttons);

            buttons.Controls.Add(MakeHubButton("Refresh List", RefreshCatalog, Color.FromArgb(54, 151, 255), 172));
            buttons.Controls.Add(MakeHubButton("Open Selected Page", OpenSelectedPage, Color.FromArgb(125, 112, 255), 172));
            buttons.Controls.Add(MakeHubButton("Install Downloaded ZIP", InstallDownloadedZip, Color.FromArgb(85, 220, 155), 172));
            buttons.Controls.Add(MakeHubButton("Install Downloaded Folder", InstallDownloadedFolder, Color.FromArgb(255, 195, 70), 172));
            buttons.Controls.Add(MakeHubButton("Open Mod Library", OpenLibrary, Color.FromArgb(255, 120, 76), 172));
            buttons.Controls.Add(MakeHubButton("Done", delegate { Close(); }, Color.FromArgb(255, 63, 92), 172));

            statusLabel.Text = "Loading GameJolt list...";
            statusLabel.ForeColor = Color.FromArgb(224, 226, 235);
            statusLabel.BackColor = Color.FromArgb(13, 15, 22);
            statusLabel.BorderStyle = BorderStyle.FixedSingle;
            statusLabel.AutoSize = false;
            statusLabel.Location = new Point(498, 470);
            statusLabel.Size = new Size(560, 80);
            statusLabel.Padding = new Padding(12);
            body.Controls.Add(statusLabel);
        }

        private Button MakeHubButton(string text, Action click, Color accent, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 50;
            b.Margin = new Padding(0, 0, 12, 12);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(12, 17, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(12, 17, 28);
            };
            b.Click += delegate { click(); };
            return b;
        }

        private void RefreshCatalog()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                statusLabel.Text = "Contacting GameJolt...";
                Application.DoEvents();
                allMods.Clear();
                allMods.AddRange(DownloadCatalog());
                ApplyFilter();
                lastStatus = "GameJolt Mod Hub loaded " + allMods.Count.ToString() + " Undertale-tagged project" + (allMods.Count == 1 ? "" : "s") + ".";
                statusLabel.Text = lastStatus;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not load GameJolt list: " + ex.Message;
                ProDialog.ShowInfo(this, "GAMEJOLT LOAD FAILED", ex.Message, Color.FromArgb(255, 195, 70));
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private List<GameJoltModInfo> DownloadCatalog()
        {
            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch
            {
            }

            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "UndertaleSaveStudioPro/1.0";
                string html = client.DownloadString(CatalogUrl);
                return ParseCatalog(html);
            }
        }

        private List<GameJoltModInfo> ParseCatalog(string html)
        {
            List<GameJoltModInfo> result = new List<GameJoltModInfo>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Regex linkRegex = new Regex("<a\\b[^>]*href\\s*=\\s*[\"'](?<href>/games/[^\"'#?]+/\\d+)(?:[?#][^\"']*)?[\"'][^>]*>(?<body>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match match in linkRegex.Matches(html))
            {
                string href = match.Groups["href"].Value;
                string title = CleanTitle(StripHtml(match.Groups["body"].Value));
                if (IsNoiseTitle(title))
                {
                    Match titleMatch = Regex.Match(match.Value, "title\\s*=\\s*[\"'](?<title>[^\"']+)[\"']", RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        title = CleanTitle(titleMatch.Groups["title"].Value);
                    }
                }

                if (IsNoiseTitle(title))
                {
                    continue;
                }

                string url = "https://gamejolt.com" + href;
                if (!seen.Add(url))
                {
                    continue;
                }

                result.Add(new GameJoltModInfo
                {
                    Title = title,
                    Url = url,
                    Slug = href
                });
            }
            return result;
        }

        private static string StripHtml(string value)
        {
            string noTags = Regex.Replace(value ?? "", "<[^>]+>", " ");
            return WebUtility.HtmlDecode(noTags);
        }

        private static string CleanTitle(string value)
        {
            string cleaned = WebUtility.HtmlDecode(value ?? "");
            cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
            return cleaned;
        }

        private static bool IsNoiseTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }
            string lower = title.Trim().ToLowerInvariant();
            if (lower == "image" || lower == "store" || lower == "search" || lower == "get app" || lower == "log in" || lower == "sign up" || lower == "next" || lower == "last")
            {
                return true;
            }
            int pageNumber;
            return int.TryParse(title, out pageNumber);
        }

        private void ApplyFilter()
        {
            string q = (searchBox.Text ?? "").Trim().ToLowerInvariant();
            modList.BeginUpdate();
            modList.Items.Clear();
            foreach (GameJoltModInfo mod in allMods)
            {
                if (q.Length == 0 || mod.Title.ToLowerInvariant().Contains(q) || mod.Url.ToLowerInvariant().Contains(q))
                {
                    modList.Items.Add(mod);
                }
            }
            modList.EndUpdate();
            countLabel.Text = modList.Items.Count.ToString() + " shown / " + allMods.Count.ToString() + " loaded";
            if (modList.Items.Count > 0)
            {
                modList.SelectedIndex = 0;
            }
            else
            {
                UpdateDetails();
            }
        }

        private GameJoltModInfo SelectedMod()
        {
            return modList.SelectedItem as GameJoltModInfo;
        }

        private void UpdateDetails()
        {
            GameJoltModInfo mod = SelectedMod();
            if (mod == null)
            {
                detailsBox.Text = "No project selected.";
                return;
            }
            detailsBox.Text = mod.Title + Environment.NewLine + mod.Url + Environment.NewLine + Environment.NewLine +
                "Open the project page, download its Windows ZIP/folder, then install it here. Full fangames are copied into the mod library. data.win-style mods are layered onto a fresh copy of your Undertale game folder.";
        }

        private void OpenSelectedPage()
        {
            GameJoltModInfo mod = SelectedMod();
            string url = mod == null ? BrowserUrl : mod.Url;
            try
            {
                Process.Start(url);
                lastStatus = "Opened GameJolt page: " + url;
                statusLabel.Text = lastStatus;
            }
            catch (Exception ex)
            {
                ProDialog.ShowInfo(this, "OPEN FAILED", ex.Message, Color.FromArgb(255, 195, 70));
            }
        }

        private void InstallDownloadedZip()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Choose the downloaded GameJolt mod ZIP";
                d.Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*";
                d.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string temp = Path.Combine(Path.GetTempPath(), "UTSS_GameJolt_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
                try
                {
                    Directory.CreateDirectory(temp);
                    statusLabel.Text = "Extracting " + Path.GetFileName(d.FileName) + "...";
                    Application.DoEvents();
                    SafeExtractZip(d.FileName, temp);
                    string modName = SelectedMod() == null ? Path.GetFileNameWithoutExtension(d.FileName) : SelectedMod().Title;
                    InstallModFolder(temp, modName);
                }
                catch (Exception ex)
                {
                    ProDialog.ShowInfo(this, "INSTALL FAILED", ex.Message, Color.FromArgb(255, 195, 70));
                    statusLabel.Text = "Install failed: " + ex.Message;
                }
                finally
                {
                    DeleteTempExtract(temp);
                }
            }
        }

        private void InstallDownloadedFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Choose the downloaded or extracted GameJolt mod folder";
                d.SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    string modName = SelectedMod() == null ? Path.GetFileName(d.SelectedPath) : SelectedMod().Title;
                    InstallModFolder(d.SelectedPath, modName);
                }
                catch (Exception ex)
                {
                    ProDialog.ShowInfo(this, "INSTALL FAILED", ex.Message, Color.FromArgb(255, 195, 70));
                    statusLabel.Text = "Install failed: " + ex.Message;
                }
            }
        }

        private void InstallModFolder(string sourceFolder, string displayName)
        {
            string modRoot = FindLikelyModRoot(sourceFolder);
            string installName = SafeFolderName(displayName);
            if (installName.Length == 0)
            {
                installName = SafeFolderName(Path.GetFileName(modRoot));
            }

            if (LooksLikeStandaloneGame(modRoot))
            {
                if (!ProDialog.ShowConfirm(this, "INSTALL STANDALONE MOD", "This looks like a full GameJolt fangame or standalone build.\r\n\r\nCopy it into your managed mod library now?", Color.FromArgb(85, 220, 155)))
                {
                    return;
                }

                string outFolder = UniqueInstallFolder(installName);
                CopyDirectory(modRoot, outFolder, true);
                FinishInstall("Installed standalone mod", outFolder);
                return;
            }

            if (LooksLikeDataWinMod(modRoot))
            {
                string gameFolder = PickUndertaleGameFolder();
                if (string.IsNullOrEmpty(gameFolder))
                {
                    return;
                }
                if (!LooksLikeUndertaleFolder(gameFolder))
                {
                    if (!ProDialog.ShowConfirm(this, "UNUSUAL GAME FOLDER", "That folder does not look like a normal Undertale folder.\r\n\r\nContinue anyway and create a modded copy?", Color.FromArgb(255, 195, 70)))
                    {
                        return;
                    }
                }
                if (!ProDialog.ShowConfirm(this, "CREATE MODDED COPY", "The app will copy your Undertale folder, then layer the downloaded mod files onto that copy.\r\n\r\nYour original game folder will not be changed.", Color.FromArgb(54, 151, 255)))
                {
                    return;
                }

                string outFolder = UniqueInstallFolder(installName);
                statusLabel.Text = "Copying clean Undertale folder...";
                Application.DoEvents();
                CopyDirectory(gameFolder, outFolder, true);
                statusLabel.Text = "Layering GameJolt mod files...";
                Application.DoEvents();
                CopyDirectory(modRoot, outFolder, true);
                FinishInstall("Created modded Undertale copy", outFolder);
                return;
            }

            if (!ProDialog.ShowConfirm(this, "UNKNOWN MOD TYPE", "This download does not have a top-level EXE or data.win.\r\n\r\nCopy it into the mod library anyway?", Color.FromArgb(255, 195, 70)))
            {
                return;
            }

            string libraryFolder = UniqueInstallFolder(installName);
            CopyDirectory(modRoot, libraryFolder, true);
            FinishInstall("Copied unknown mod package", libraryFolder);
        }

        private string PickUndertaleGameFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Choose your clean Undertale game folder";
                string steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Undertale");
                if (Directory.Exists(steamPath))
                {
                    d.SelectedPath = steamPath;
                }
                else
                {
                    d.SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }
                return d.ShowDialog(this) == DialogResult.OK ? d.SelectedPath : null;
            }
        }

        private void FinishInstall(string action, string outFolder)
        {
            lastStatus = action + ": " + outFolder;
            statusLabel.Text = lastStatus;
            ProDialog.ShowInfo(this, "MOD INSTALLED", action + ".\r\n\r\nFolder:\r\n" + outFolder, Color.FromArgb(85, 220, 155));
            try
            {
                Process.Start(outFolder);
            }
            catch
            {
            }
        }

        private void OpenLibrary()
        {
            Directory.CreateDirectory(libraryRoot);
            try
            {
                Process.Start(libraryRoot);
                lastStatus = "Opened mod library: " + libraryRoot;
                statusLabel.Text = lastStatus;
            }
            catch (Exception ex)
            {
                ProDialog.ShowInfo(this, "OPEN FAILED", ex.Message, Color.FromArgb(255, 195, 70));
            }
        }

        private static void SafeExtractZip(string zipFile, string destination)
        {
            string root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                root += Path.DirectorySeparatorChar;
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipFile))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Blocked a ZIP entry that tried to write outside the extract folder.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        entry.ExtractToFile(target, true);
                    }
                }
            }
        }

        private static void DeleteTempExtract(string folder)
        {
            try
            {
                string tempRoot = Path.GetFullPath(Path.GetTempPath());
                string full = Path.GetFullPath(folder);
                if (full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full))
                {
                    Directory.Delete(full, true);
                }
            }
            catch
            {
            }
        }

        private static string FindLikelyModRoot(string folder)
        {
            string current = folder;
            for (int i = 0; i < 5; i++)
            {
                if (LooksLikeStandaloneGame(current) || LooksLikeDataWinMod(current))
                {
                    return current;
                }

                string[] files = SafeGetFiles(current, "*", SearchOption.TopDirectoryOnly);
                string[] dirs = SafeGetDirectories(current, "*", SearchOption.TopDirectoryOnly);
                if (files.Length == 0 && dirs.Length == 1)
                {
                    current = dirs[0];
                    continue;
                }

                string dataWin = SafeFindFirst(current, "data.win");
                if (!string.IsNullOrEmpty(dataWin))
                {
                    return Path.GetDirectoryName(dataWin);
                }

                string exe = SafeFindFirst(current, "*.exe");
                if (!string.IsNullOrEmpty(exe))
                {
                    return Path.GetDirectoryName(exe);
                }

                break;
            }
            return current;
        }

        private static bool LooksLikeStandaloneGame(string folder)
        {
            return SafeGetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static bool LooksLikeDataWinMod(string folder)
        {
            if (File.Exists(Path.Combine(folder, "data.win")))
            {
                return true;
            }
            return SafeGetFiles(folder, "*.win", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static bool LooksLikeUndertaleFolder(string folder)
        {
            if (File.Exists(Path.Combine(folder, "data.win")))
            {
                return true;
            }
            if (File.Exists(Path.Combine(folder, "UNDERTALE.exe")) || File.Exists(Path.Combine(folder, "Undertale.exe")) || File.Exists(Path.Combine(folder, "undertale.exe")))
            {
                return true;
            }
            foreach (string exe in SafeGetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
                if (name.Contains("undertale"))
                {
                    return true;
                }
            }
            return false;
        }

        private string UniqueInstallFolder(string displayName)
        {
            Directory.CreateDirectory(libraryRoot);
            string safe = SafeFolderName(displayName);
            if (safe.Length == 0)
            {
                safe = "GameJolt_Mod";
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(libraryRoot, safe + "_" + stamp);
            int counter = 2;
            while (Directory.Exists(path))
            {
                path = Path.Combine(libraryRoot, safe + "_" + stamp + "_" + counter.ToString(CultureInfo.InvariantCulture));
                counter++;
            }
            return path;
        }

        private static string SafeFolderName(string name)
        {
            string safe = Regex.Replace(name ?? "", "^(Free\\s+)+", "", RegexOptions.IgnoreCase).Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }
            safe = Regex.Replace(safe, "\\s+", " ").Trim();
            if (safe.Length > 54)
            {
                safe = safe.Substring(0, 54).Trim();
            }
            return safe;
        }

        private static string SafeFindFirst(string folder, string pattern)
        {
            string[] found = SafeGetFiles(folder, pattern, SearchOption.AllDirectories);
            return found.Length == 0 ? null : found[0];
        }

        private static string[] SafeGetFiles(string folder, string pattern, SearchOption option)
        {
            try
            {
                return Directory.GetFiles(folder, pattern, option);
            }
            catch
            {
                return new string[0];
            }
        }

        private static string[] SafeGetDirectories(string folder, string pattern, SearchOption option)
        {
            try
            {
                return Directory.GetDirectories(folder, pattern, option);
            }
            catch
            {
                return new string[0];
            }
        }

        private static void CopyDirectory(string source, string destination, bool overwrite)
        {
            string root = Path.GetFullPath(source);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                root += Path.DirectorySeparatorChar;
            }

            Directory.CreateDirectory(destination);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(root.Length);
                Directory.CreateDirectory(Path.Combine(destination, rel));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(root.Length);
                string target = Path.Combine(destination, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, overwrite);
            }
        }
    }

    internal delegate void FeatureApply(SaveModel model, Random random);

    internal sealed class FeatureDef
    {
        public string Category;
        public string Name;
        public string Description;
        public Color Accent;
        public FeatureApply Apply;

        public FeatureDef(string category, string name, string description, Color accent, FeatureApply apply)
        {
            Category = category;
            Name = name;
            Description = description;
            Accent = accent;
            Apply = apply;
        }
    }

    internal sealed class FeatureVaultForm : Form
    {
        private readonly SaveModel model;
        private readonly List<FeatureDef> features;
        private readonly ListBox categoryList = new ListBox();
        private readonly FlowLayoutPanel featureGrid = new FlowLayoutPanel();
        private readonly TextBox searchBox = new TextBox();
        private readonly Label countLabel = new Label();
        private readonly Label logLabel = new Label();
        private readonly Label pageLabel = new Label();
        private readonly ToolTip tips = new ToolTip();
        private readonly Random random = new Random();
        private readonly RoomWarp[] allRooms;
        private readonly RoomWarp[] playerRooms;
        private int appliedCount;
        private int pageIndex;
        private const int PowerMax = 999999999;
        private const int PageSize = 96;

        public int AppliedCount
        {
            get { return appliedCount; }
        }

        public FeatureVaultForm(SaveModel model)
        {
            this.model = model;
            allRooms = RoomCatalog.Load();
            playerRooms = RoomCatalog.PlayerRooms();
            features = BuildFeatures();
            Text = "Feature Vault 100K+";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1220, 720);
            Size = new Size(1280, 780);
            BackColor = Color.FromArgb(5, 7, 12);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildUi();
            RefreshFeatureButtons();
        }

        private void BuildUi()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 104;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "FEATURE VAULT";
            title.Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(28, 16);
            title.AutoSize = true;
            header.Controls.Add(title);

            countLabel.Text = features.Count.ToString() + " loaded tools";
            countLabel.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            countLabel.ForeColor = Color.FromArgb(170, 230, 255);
            countLabel.BackColor = Color.FromArgb(8, 24, 38);
            countLabel.TextAlign = ContentAlignment.MiddleCenter;
            countLabel.Location = new Point(292, 28);
            countLabel.Size = new Size(180, 26);
            header.Controls.Add(countLabel);

            Label subtitle = new Label();
            subtitle.Text = "One-click tools with plain labels: stats, routes, flags, room teleports, items, battles, and chaos runs.";
            subtitle.ForeColor = Color.FromArgb(224, 226, 235);
            subtitle.Location = new Point(32, 68);
            subtitle.AutoSize = true;
            header.Controls.Add(subtitle);

            Panel sidebar = MakeVaultPanel(new Point(24, 126), new Size(226, 520));
            Controls.Add(sidebar);

            Label catTitle = new Label();
            catTitle.Text = "Categories";
            catTitle.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
            catTitle.ForeColor = Color.White;
            catTitle.Location = new Point(16, 14);
            catTitle.AutoSize = true;
            sidebar.Controls.Add(catTitle);

            categoryList.Location = new Point(16, 48);
            categoryList.Size = new Size(194, 450);
            categoryList.BackColor = Color.FromArgb(8, 10, 17);
            categoryList.ForeColor = Color.White;
            categoryList.BorderStyle = BorderStyle.FixedSingle;
            categoryList.IntegralHeight = false;
            categoryList.SelectedIndexChanged += delegate { pageIndex = 0; RefreshFeatureButtons(); };
            sidebar.Controls.Add(categoryList);

            categoryList.Items.Add("All Features");
            foreach (string category in features.Select(delegate(FeatureDef f) { return f.Category; }).Distinct().OrderBy(delegate(string s) { return s; }))
            {
                categoryList.Items.Add(category);
            }
            categoryList.SelectedIndex = 0;

            searchBox.Location = new Point(270, 126);
            searchBox.Size = new Size(802, 28);
            searchBox.BackColor = Color.FromArgb(8, 10, 17);
            searchBox.ForeColor = Color.White;
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.TextChanged += delegate { pageIndex = 0; RefreshFeatureButtons(); };
            Controls.Add(searchBox);

            Label searchLabel = new Label();
            searchLabel.Text = "Search";
            searchLabel.ForeColor = Color.FromArgb(170, 170, 182);
            searchLabel.Location = new Point(270, 104);
            searchLabel.AutoSize = true;
            Controls.Add(searchLabel);

            featureGrid.Location = new Point(270, 166);
            featureGrid.Size = new Size(802, 480);
            featureGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            featureGrid.AutoScroll = true;
            featureGrid.WrapContents = true;
            featureGrid.BackColor = Color.FromArgb(5, 7, 12);
            Controls.Add(featureGrid);

            logLabel.Text = "Ready.";
            logLabel.ForeColor = Color.FromArgb(224, 226, 235);
            logLabel.BackColor = Color.FromArgb(12, 17, 28);
            logLabel.BorderStyle = BorderStyle.FixedSingle;
            logLabel.Location = new Point(24, 662);
            logLabel.Size = new Size(510, 46);
            logLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            logLabel.Padding = new Padding(10);
            Controls.Add(logLabel);

            Button prevPage = MakeVaultButton("Prev", Color.FromArgb(125, 112, 255));
            prevPage.Location = new Point(548, 664);
            prevPage.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            prevPage.Click += delegate
            {
                if (pageIndex > 0)
                {
                    pageIndex -= 1;
                    RefreshFeatureButtons();
                }
            };
            Controls.Add(prevPage);

            pageLabel.Text = "Page 1";
            pageLabel.ForeColor = Color.FromArgb(224, 226, 235);
            pageLabel.BackColor = Color.FromArgb(12, 17, 28);
            pageLabel.BorderStyle = BorderStyle.FixedSingle;
            pageLabel.TextAlign = ContentAlignment.MiddleCenter;
            pageLabel.Location = new Point(668, 664);
            pageLabel.Size = new Size(92, 42);
            pageLabel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(pageLabel);

            Button nextPage = MakeVaultButton("Next", Color.FromArgb(125, 112, 255));
            nextPage.Location = new Point(764, 664);
            nextPage.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            nextPage.Click += delegate
            {
                pageIndex += 1;
                RefreshFeatureButtons();
            };
            Controls.Add(nextPage);

            Button randomVisible = MakeVaultButton("Random Visible", Color.FromArgb(255, 195, 70));
            randomVisible.Location = new Point(884, 664);
            randomVisible.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            randomVisible.Click += delegate { ApplyRandomVisible(); };
            Controls.Add(randomVisible);

            Button liveConfig = MakeVaultButton("Live Config", Color.FromArgb(54, 151, 255));
            liveConfig.Location = new Point(1004, 664);
            liveConfig.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            liveConfig.Click += delegate
            {
                model.WriteLiveConfig(true);
                logLabel.Text = "codex_live.ini refreshed from the current in-memory feature build.";
            };
            Controls.Add(liveConfig);

            Button done = MakeVaultButton("Done", Color.FromArgb(85, 220, 155));
            done.Location = new Point(1124, 664);
            done.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            done.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(done);
        }

        private Panel MakeVaultPanel(Point location, Size size)
        {
            Panel p = new Panel();
            p.Location = location;
            p.Size = size;
            p.BackColor = Color.FromArgb(10, 13, 22);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(18, 23, 36), Color.FromArgb(7, 9, 15), 90f))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (Pen pen = new Pen(Color.FromArgb(62, 72, 95), 1f))
                {
                    e.Graphics.DrawRectangle(pen, r);
                }
            };
            return p;
        }

        private Button MakeVaultButton(string text, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 116;
            b.Height = 42;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(12, 17, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(12, 17, 28);
            };
            return b;
        }

        private void RefreshFeatureButtons()
        {
            featureGrid.SuspendLayout();
            featureGrid.Controls.Clear();
            List<FeatureDef> visible = FilteredFeatures();
            int total = visible.Count;
            int maxPage = total == 0 ? 0 : (total - 1) / PageSize;
            if (pageIndex < 0) pageIndex = 0;
            if (pageIndex > maxPage) pageIndex = maxPage;
            int start = pageIndex * PageSize;
            int end = Math.Min(total, start + PageSize);
            countLabel.Text = total.ToString() + " visible / " + features.Count.ToString();
            pageLabel.Text = total == 0 ? "Page 0/0" : "Page " + (pageIndex + 1).ToString() + "/" + (maxPage + 1).ToString();

            for (int i = start; i < end; i++)
            {
                FeatureDef def = visible[i];
                Button b = MakeFeatureButton(def);
                featureGrid.Controls.Add(b);
            }
            featureGrid.ResumeLayout();
        }

        private Button MakeFeatureButton(FeatureDef def)
        {
            Button b = new Button();
            b.Text = def.Name + "\r\n" + Shorten(def.Description, 54);
            b.Tag = def;
            b.Width = 252;
            b.Height = 76;
            b.Margin = new Padding(6);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = def.Accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(12, 17, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.TextAlign = ContentAlignment.MiddleCenter;
            tips.SetToolTip(b, def.Category + ": " + def.Description);
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, def.Accent.R / 3 + 28), Math.Min(255, def.Accent.G / 3 + 28), Math.Min(255, def.Accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(12, 17, 28);
            };
            b.Click += delegate { ApplyFeature(def); };
            return b;
        }

        private string Shorten(string value, int max)
        {
            string text = Regex.Replace(value ?? "", "\\s+", " ").Trim();
            if (text.Length <= max)
            {
                return text;
            }
            return text.Substring(0, Math.Max(0, max - 3)).TrimEnd() + "...";
        }

        private List<FeatureDef> FilteredFeatures()
        {
            string category = categoryList.SelectedItem == null ? "All Features" : categoryList.SelectedItem.ToString();
            string search = (searchBox.Text ?? "").Trim().ToLowerInvariant();
            List<FeatureDef> visible = new List<FeatureDef>();
            foreach (FeatureDef def in features)
            {
                if (category != "All Features" && def.Category != category)
                {
                    continue;
                }
                if (search.Length > 0)
                {
                    string haystack = (def.Name + " " + def.Category + " " + def.Description).ToLowerInvariant();
                    if (!haystack.Contains(search))
                    {
                        continue;
                    }
                }
                visible.Add(def);
            }
            return visible;
        }

        private void ApplyRandomVisible()
        {
            List<FeatureDef> visible = FilteredFeatures();
            if (visible.Count == 0)
            {
                logLabel.Text = "No visible features to randomize.";
                return;
            }
            FeatureDef def = visible[random.Next(visible.Count)];
            ApplyFeature(def);
        }

        private void ApplyFeature(FeatureDef def)
        {
            try
            {
                def.Apply(model, random);
                appliedCount += 1;
                logLabel.Text = "Applied " + def.Name + ". Total applied this session: " + appliedCount.ToString() + ".";
            }
            catch (Exception ex)
            {
                logLabel.Text = "Feature failed: " + ex.Message;
            }
        }

        private List<FeatureDef> BuildFeatures()
        {
            List<FeatureDef> list = new List<FeatureDef>();
            AddStatPresets(list);
            AddLevelLadder(list);
            AddFunFeatures(list);
            AddRouteFeatures(list);
            AddRoomFeatures(list);
            AddInventoryFeatures(list);
            AddBattleFlagFeatures(list);
            AddWorldFeatures(list);
            AddChaosFeatures(list);
            AddFeatureMatrix(list);
            return list;
        }

        private void AddFeature(List<FeatureDef> list, string category, string name, string description, Color accent, FeatureApply apply)
        {
            list.Add(new FeatureDef(category, name, description, accent, apply));
        }

        private void AddStatFeature(List<FeatureDef> list, string name, int lv, int hp, int damage, int xp, int gold, int kills)
        {
            AddFeature(list, "Power Presets", name, "Sets LV, HP, DMG, EXP, gold, and kills.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r)
            {
                m.SetCoreStats(lv, hp, damage, xp, gold, kills);
            });
        }

        private void AddStatPresets(List<FeatureDef> list)
        {
            AddStatFeature(list, "Fresh Start", 1, 20, 10, 0, 0, 0);
            AddStatFeature(list, "Ruins Boost", 3, 28, 14, 30, 120, 2);
            AddStatFeature(list, "Snowdin Ready", 7, 44, 22, 300, 450, 8);
            AddStatFeature(list, "Waterfall Guard", 10, 56, 28, 1200, 900, 18);
            AddStatFeature(list, "Hotland Core", 14, 72, 36, 5000, 1800, 34);
            AddStatFeature(list, "MTT Prep", 17, 84, 42, 15000, 2800, 55);
            AddStatFeature(list, "Judgment Hall", 20, 99, 30, 99999, 9999, 99);
            AddStatFeature(list, "No Hit Challenge", 1, 1, 1, 0, 0, 0);
            AddStatFeature(list, "Glass Cannon", 1, 20, 999999999, 0, 0, 0);
            AddStatFeature(list, "Tank Build", 1, 999999999, 10, 0, 0, 0);
            AddStatFeature(list, "Gold Farm", 8, 48, 24, 500, 999999999, 10);
            AddStatFeature(list, "EXP Farm", 20, 99, 30, 999999999, 0, 100);
            AddStatFeature(list, "Undyne Practice", 12, 64, 32, 2500, 1600, 25);
            AddStatFeature(list, "Sans Practice", 20, 99, 999999999, 99999, 9999, 999);
            AddStatFeature(list, "Omega Ready", 20, 999999999, 999999999, 999999999, 999999999, 999999);
            AddStatFeature(list, "Pacifist Armor", 1, 999, 10, 0, 999, 0);
            AddStatFeature(list, "Neutral Bruiser", 13, 68, 250, 3500, 2400, 31);
            AddStatFeature(list, "Absolute Max", PowerMax, PowerMax, PowerMax, PowerMax, PowerMax, 999999);
        }

        private void AddLevelLadder(List<FeatureDef> list)
        {
            for (int lv = 1; lv <= 30; lv++)
            {
                int capture = lv;
                AddFeature(list, "LV Ladder", "LV " + capture.ToString() + " Auto Stats", "Sets LV with matching HP, DMG, and EXP.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r)
                {
                    m.SetCoreStats(capture, LevelHp(capture), LevelDamage(capture), LevelXp(capture), m.GetNumber(SaveModel.Gold, 0), m.GetNumber(SaveModel.Kills, 0));
                });
            }
        }

        private void AddFunFeatures(List<FeatureDef> list)
        {
            int[] values = new int[] { 1, 2, 13, 20, 30, 40, 45, 46, 47, 50, 56, 61, 62, 63, 65, 66, 70, 80, 81, 90, 91, 92, 99, 100 };
            for (int i = 0; i < values.Length; i++)
            {
                int fun = values[i];
                AddFeature(list, "FUN Timeline", "FUN " + fun.ToString(), "Sets undertale.ini FUN to " + fun.ToString() + ".", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r)
                {
                    m.SetFun(fun);
                });
            }
        }

        private void AddRouteFeatures(List<FeatureDef> list)
        {
            AddFeature(list, "Route Control", "Clean Pacifist", "Clears LV, EXP, kills, and murder flag.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r)
            {
                m.SetCoreStats(1, 20, 10, 0, m.GetNumber(SaveModel.Gold, 0), 0);
                m.SetFlag(26, 0);
                m.SetNumber(SaveModel.Plot, 0);
            });
            AddFeature(list, "Route Control", "Neutral Light", "Sets a low-kill neutral state.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r)
            {
                m.SetCoreStats(4, 32, 16, 70, m.GetNumber(SaveModel.Gold, 0), 4);
                m.SetFlag(26, 0);
            });
            AddFeature(list, "Route Control", "Neutral Heavy", "Sets a high-kill neutral state.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r)
            {
                m.SetCoreStats(15, 76, 38, 7000, m.GetNumber(SaveModel.Gold, 0), 75);
                m.SetFlag(26, 0);
            });
            AddFeature(list, "Route Control", "Genocide Start", "Starts the murder-route override.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r)
            {
                m.SetFlag(26, 4);
                m.SetNumber(SaveModel.Kills, Math.Max(20, m.GetNumber(SaveModel.Kills, 0)));
            });
            AddFeature(list, "Route Control", "Genocide Locked", "Sets murder-route override to the max threshold.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r)
            {
                m.SetFlag(26, 16);
                m.SetCoreStats(20, 99, Math.Max(30, m.DamagePower()), 99999, m.GetNumber(SaveModel.Gold, 0), 999);
            });
            AddFeature(list, "Route Control", "Abort Genocide", "Drops murder route back to neutral.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r)
            {
                m.SetFlag(26, 0);
                m.SetNumber(SaveModel.Kills, Math.Min(10, m.GetNumber(SaveModel.Kills, 0)));
            });
            AddFeature(list, "Route Control", "Toriel Cleared", "Marks early Ruins progress.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 25); m.SetNumber(SaveModel.Room, 43); });
            AddFeature(list, "Route Control", "Snowdin Cleared", "Marks Snowdin progress.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 80); m.SetNumber(SaveModel.Room, 81); });
            AddFeature(list, "Route Control", "Waterfall Cleared", "Marks Waterfall progress.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 130); m.SetNumber(SaveModel.Room, 141); });
            AddFeature(list, "Route Control", "Hotland Cleared", "Marks Hotland progress.", Color.FromArgb(255, 120, 76), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 180); m.SetNumber(SaveModel.Room, 226); });
            AddFeature(list, "Route Control", "Core Cleared", "Marks CORE progress.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 200); m.SetNumber(SaveModel.Room, 231); });
            AddFeature(list, "Route Control", "True Lab Prep", "Sets late pacifist prep flags.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetFlag(251, 1); m.SetFlag(252, 1); m.SetNumber(SaveModel.Plot, 210); });
            AddFeature(list, "Route Control", "Alphys Date Ready", "Sets common date-prep flags.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r) { m.SetFlag(402, 1); m.SetFlag(397, 1); });
            AddFeature(list, "Route Control", "Mercy Chain", "Turns on several mercy route flags.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetFlags(new int[] { 27, 45, 52, 53, 54, 67, 81 }, 1); });
            AddFeature(list, "Route Control", "Reset Mercy Flags", "Clears several mercy route flags.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { m.SetFlags(new int[] { 27, 45, 52, 53, 54, 67, 81 }, 0); });
            AddFeature(list, "Route Control", "Bosses Alive", "Clears common boss defeated flags.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetFlags(new int[] { 52, 53, 54, 57, 67, 81, 251, 252, 350, 397, 402, 425 }, 0); });
            AddFeature(list, "Route Control", "Bosses Cleared", "Sets common boss defeated flags.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r) { m.SetFlags(new int[] { 52, 53, 54, 57, 67, 81, 251, 252, 350, 397, 402, 425 }, 1); });
            AddFeature(list, "Route Control", "Random Murder Meter", "Randomizes the murder-route override value.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r) { m.SetFlag(26, r.Next(0, 17)); });
        }

        private void AddRoomFeatures(List<FeatureDef> list)
        {
            int[] ids = new int[] { 4, 6, 32, 37, 43, 44, 56, 68, 71, 73, 76, 78, 81, 82, 91, 104, 123, 131, 141, 153, 165, 176, 184, 193, 205, 213, 226, 231, 237, 241, 246, 253, 264, 275, 296, 326 };
            for (int i = 0; i < ids.Length; i++)
            {
                int roomId = ids[i];
                string roomName = RoomName(roomId);
                AddFeature(list, "Room Warps", "Warp " + roomName, "Sets current room to " + roomName + ".", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r)
                {
                    m.SetNumber(SaveModel.Room, roomId);
                });
            }
        }

        private void AddInventoryFeatures(List<FeatureDef> list)
        {
            AddInventoryFeature(list, "Best Heals", new int[] { 11, 43, 43, 40, 40, 21, 17, 17 }, 3, 4);
            AddInventoryFeature(list, "Pie Stack", new int[] { 11, 11, 11, 11, 11, 11, 11, 11 }, 3, 4);
            AddInventoryFeature(list, "Legendary Heals", new int[] { 40, 40, 40, 40, 40, 40, 40, 40 }, 3, 4);
            AddInventoryFeature(list, "Snowman Army", new int[] { 16, 16, 16, 16, 16, 16, 16, 16 }, 3, 4);
            AddInventoryFeature(list, "Temmie Mode", new int[] { 22, 22, 22, 22, 22, 22, 22, 22 }, 3, 4);
            AddInventoryFeature(list, "Weapon Museum", new int[] { 3, 13, 14, 25, 45, 47, 49, 51 }, 52, 53);
            AddInventoryFeature(list, "Armor Closet", new int[] { 4, 12, 15, 24, 44, 46, 48, 50 }, 3, 53);
            AddInventoryFeature(list, "Real Knife Kit", new int[] { 52, 53, 11, 40, 40, 43, 43, 21 }, 52, 53);
            AddInventoryFeature(list, "Punch Card Pack", new int[] { 26, 26, 26, 26, 26, 26, 26, 26 }, 3, 4);
            AddInventoryFeature(list, "Spider Buffet", new int[] { 7, 10, 7, 10, 7, 10, 7, 10 }, 3, 4);
            AddInventoryFeature(list, "Sans Snacks", new int[] { 17, 17, 17, 19, 21, 21, 40, 43 }, 3, 4);
            AddInventoryFeature(list, "Empty Bag", new int[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 3, 4);
            AddInventoryFeature(list, "Boss Attack Tokens", new int[] { 9001, 9002, 9003, 9004, 9005, 9006, 9007, 9008 }, 52, 53);
            AddInventoryFeature(list, "Bone Loadout", new int[] { 9002, 9002, 9006, 9006, 9008, 9008, 11, 40 }, 52, 53);
            AddInventoryFeature(list, "Spear Loadout", new int[] { 9003, 9003, 9003, 9007, 9007, 11, 40, 43 }, 52, 53);
            AddInventoryFeature(list, "Blaster Loadout", new int[] { 9001, 9001, 9001, 9001, 9005, 9005, 11, 40 }, 52, 53);
            AddInventoryFeature(list, "Random Normal Items", null, 3, 4);
            AddInventoryFeature(list, "Random Battle Tokens", new int[] { 9001, 9002, 9003, 9004, 9005, 9006, 9007, 9008 }, 52, 53);
        }

        private void AddInventoryFeature(List<FeatureDef> list, string name, int[] ids, int weapon, int armor)
        {
            AddFeature(list, "Inventory Kits", name, "Sets inventory and equipment.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r)
            {
                int[] next = new int[8];
                if (ids == null)
                {
                    int[] normal = ItemCatalog.NormalIds();
                    for (int i = 0; i < next.Length; i++)
                    {
                        next[i] = normal[r.Next(normal.Length)];
                    }
                }
                else if (name == "Random Battle Tokens")
                {
                    int[] tokens = ItemCatalog.BattleTokenIds();
                    for (int i = 0; i < next.Length; i++)
                    {
                        next[i] = tokens[r.Next(tokens.Length)];
                    }
                }
                else
                {
                    for (int i = 0; i < next.Length; i++)
                    {
                        next[i] = i < ids.Length ? ids[i] : 0;
                    }
                }
                m.SetInventory(next);
                m.Equip(weapon, armor);
            });
        }

        private void AddBattleFlagFeatures(List<FeatureDef> list)
        {
            int[] flags = new int[] { 27, 45, 52, 53, 54, 57, 67, 81, 202, 203, 204, 205, 251, 252, 300, 350, 397, 402, 425, 493 };
            for (int i = 0; i < flags.Length; i++)
            {
                int flag = flags[i];
                AddFeature(list, "Battle Flags", "Flag " + flag.ToString() + " ON", "Sets global flag " + flag.ToString() + " to 1.", Color.FromArgb(255, 120, 76), delegate(SaveModel m, Random r) { m.SetFlag(flag, 1); });
                AddFeature(list, "Battle Flags", "Flag " + flag.ToString() + " OFF", "Sets global flag " + flag.ToString() + " to 0.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { m.SetFlag(flag, 0); });
            }
            AddFeature(list, "Battle Flags", "Major Boss Chain", "Sets several boss-progress flags.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r) { m.SetFlags(new int[] { 45, 52, 53, 54, 57, 67, 81, 251, 252, 350, 397, 402, 425 }, 1); });
            AddFeature(list, "Battle Flags", "Clear Boss Chain", "Clears several boss-progress flags.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetFlags(new int[] { 45, 52, 53, 54, 57, 67, 81, 251, 252, 350, 397, 402, 425 }, 0); });
            AddFeature(list, "Battle Flags", "Random Boss Counters", "Randomizes common boss counter flags.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r)
            {
                m.SetFlag(202, r.Next(0, 21));
                m.SetFlag(203, r.Next(0, 17));
                m.SetFlag(204, r.Next(0, 19));
                m.SetFlag(205, r.Next(0, 41));
                m.SetFlag(493, r.Next(0, 13));
            });
        }

        private void AddWorldFeatures(List<FeatureDef> list)
        {
            AddFeature(list, "World State", "Plot 0", "Sets plot to 0.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 0); });
            AddFeature(list, "World State", "Plot 50", "Sets plot to 50.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 50); });
            AddFeature(list, "World State", "Plot 100", "Sets plot to 100.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 100); });
            AddFeature(list, "World State", "Plot 150", "Sets plot to 150.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 150); });
            AddFeature(list, "World State", "Plot 200", "Sets plot to 200.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, 200); });
            AddFeature(list, "World State", "Random Plot", "Randomizes plot.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Plot, r.Next(0, 221)); });
            AddFeature(list, "World State", "Time Zero", "Sets playtime to 0.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Time, 0); });
            AddFeature(list, "World State", "Time 1 Hour", "Sets playtime to 3600.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Time, 3600); });
            AddFeature(list, "World State", "Time 9 Hours", "Sets playtime to 32400.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Time, 32400); });
            AddFeature(list, "World State", "Random Time", "Randomizes playtime.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Time, r.Next(0, 999999)); });
            AddFeature(list, "World State", "Music Off", "Sets current song to -1.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Song, -1); });
            AddFeature(list, "World State", "Music Random", "Randomizes current song id.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { m.SetNumber(SaveModel.Song, r.Next(-1, 500)); });
            AddFeature(list, "Phone & Menu", "Clear Phones", "Clears phone slots.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetPhoneSlots(0); });
            AddFeature(list, "Phone & Menu", "Phone Page 1", "Sets phone slots to 1.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetPhoneSlots(1); });
            AddFeature(list, "Phone & Menu", "Phone Page 2", "Sets phone slots to 2.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.SetPhoneSlots(2); });
            AddFeature(list, "Phone & Menu", "Menu Choice 1", "Sets menu choices to 1.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(543, 1); m.SetNumber(544, 1); m.SetNumber(545, 1); });
            AddFeature(list, "Phone & Menu", "Menu Choice 0", "Sets menu choices to 0.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r) { m.SetNumber(543, 0); m.SetNumber(544, 0); m.SetNumber(545, 0); });
            AddFeature(list, "Phone & Menu", "Random Menu Choices", "Randomizes menu choices.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r) { m.SetNumber(543, r.Next(0, 4)); m.SetNumber(544, r.Next(0, 4)); m.SetNumber(545, r.Next(0, 4)); });
        }

        private void AddChaosFeatures(List<FeatureDef> list)
        {
            AddFeature(list, "Chaos Tools", "Random Core", "Randomizes core player numbers.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r)
            {
                int lv = r.Next(1, 5001);
                m.SetCoreStats(lv, SaveModel.Clamp(16 + (lv * 4), 1, PowerMax), r.Next(0, PowerMax + 1), r.Next(0, PowerMax + 1), r.Next(0, PowerMax + 1), r.Next(0, 500));
            });
            AddFeature(list, "Chaos Tools", "Random Player Room", "Warps to a random player room.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { SetRandomRoom(m, r, true); });
            AddFeature(list, "Chaos Tools", "Random Any Room", "Warps to a random loaded room.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { SetRandomRoom(m, r, false); });
            AddFeature(list, "Chaos Tools", "Random Inventory", "Randomizes regular inventory.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r)
            {
                int[] normal = ItemCatalog.NormalIds();
                int[] ids = new int[8];
                for (int i = 0; i < ids.Length; i++) ids[i] = normal[r.Next(normal.Length)];
                m.SetInventory(ids);
            });
            AddFeature(list, "Chaos Tools", "Random Tokens", "Randomizes battle-token inventory.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r)
            {
                int[] tokens = ItemCatalog.BattleTokenIds();
                int[] ids = new int[8];
                for (int i = 0; i < ids.Length; i++) ids[i] = tokens[r.Next(tokens.Length)];
                m.SetInventory(ids);
            });
            AddFeature(list, "Chaos Tools", "Random Route", "Randomizes murder flag and kills.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { m.SetFlag(26, r.Next(0, 17)); m.SetNumber(SaveModel.Kills, r.Next(0, 250)); });
            AddFeature(list, "Chaos Tools", "Random FUN", "Randomizes FUN.", Color.FromArgb(255, 195, 70), delegate(SaveModel m, Random r) { m.SetFun(r.Next(1, 101)); });
            AddFeature(list, "Chaos Tools", "Scramble Flags 0-50", "Randomizes early global flags.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r) { for (int i = 0; i <= 50; i++) m.SetFlag(i, r.Next(0, 3)); });
            AddFeature(list, "Chaos Tools", "Clear Flags 0-50", "Clears early global flags.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { for (int i = 0; i <= 50; i++) m.SetFlag(i, 0); });
            AddFeature(list, "Chaos Tools", "Scramble Flags 200-260", "Randomizes mid-game global flags.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r) { for (int i = 200; i <= 260; i++) m.SetFlag(i, r.Next(0, 4)); });
            AddFeature(list, "Chaos Tools", "Clear Flags 200-260", "Clears mid-game global flags.", Color.FromArgb(125, 112, 255), delegate(SaveModel m, Random r) { for (int i = 200; i <= 260; i++) m.SetFlag(i, 0); });
            AddFeature(list, "Chaos Tools", "Random Equipment", "Randomizes weapon and armor.", Color.FromArgb(85, 220, 155), delegate(SaveModel m, Random r)
            {
                int[] weapons = new int[] { 3, 13, 14, 25, 45, 47, 49, 51, 52 };
                int[] armors = new int[] { 4, 12, 15, 24, 44, 46, 48, 50, 53, 64 };
                m.Equip(weapons[r.Next(weapons.Length)], armors[r.Next(armors.Length)]);
            });
            AddFeature(list, "Chaos Tools", "Live Config Now", "Refreshes codex_live.ini.", Color.FromArgb(54, 151, 255), delegate(SaveModel m, Random r) { m.WriteLiveConfig(true); });
            AddFeature(list, "Chaos Tools", "Full Chaos Run", "Randomizes stats, room, inventory, route, FUN, and plot.", Color.FromArgb(255, 63, 92), delegate(SaveModel m, Random r)
            {
                int lv = r.Next(1, 5001);
                m.SetCoreStats(lv, SaveModel.Clamp(16 + (lv * 4), 1, PowerMax), r.Next(0, PowerMax + 1), r.Next(0, PowerMax + 1), r.Next(0, PowerMax + 1), r.Next(0, 500));
                SetRandomRoom(m, r, true);
                int[] normal = ItemCatalog.NormalIds();
                int[] ids = new int[8];
                for (int i = 0; i < ids.Length; i++) ids[i] = normal[r.Next(normal.Length)];
                m.SetInventory(ids);
                m.SetFlag(26, r.Next(0, 17));
                m.SetFun(r.Next(1, 101));
                m.SetNumber(SaveModel.Plot, r.Next(0, 221));
            });
        }

        private void AddFeatureMatrix(List<FeatureDef> list)
        {
            AddMatrixPower(list, 20000);
            AddMatrixRoutes(list, 15000);
            AddMatrixRooms(list, 20000);
            AddMatrixInventory(list, 20000);
            AddMatrixFlags(list, 15000);
            AddMatrixChaos(list, 10000);
        }

        private void AddMatrixPower(List<FeatureDef> list, int count)
        {
            Color accent = Color.FromArgb(85, 220, 155);
            for (int i = 0; i < count; i++)
            {
                int seed = i;
                AddFeature(list, "100K Power Presets", "Boost Stats #" + seed.ToString("00000"), "Sets LV, HP, DMG, EXP, gold, kills, and FUN.", accent, delegate(SaveModel m, Random r)
                {
                    int lv = MatrixPick(seed, 1, 5000) + 1;
                    int hp = MatrixPick(seed, 2, PowerMax - 1) + 1;
                    int damage = MatrixPick(seed, 3, PowerMax);
                    int xp = MatrixPick(seed, 4, PowerMax);
                    int gold = MatrixPick(seed, 5, PowerMax);
                    int kills = MatrixPick(seed, 6, 999999);
                    m.SetCoreStats(lv, hp, damage, xp, gold, kills);
                    m.SetFun(MatrixPick(seed, 7, 100) + 1);
                });
            }
        }

        private void AddMatrixRoutes(List<FeatureDef> list, int count)
        {
            Color accent = Color.FromArgb(255, 63, 92);
            for (int i = 0; i < count; i++)
            {
                int seed = i;
                AddFeature(list, "100K Route Presets", "Change Route #" + seed.ToString("00000"), "Sets route flags, kills, plot, FUN, LV, and EXP.", accent, delegate(SaveModel m, Random r)
                {
                    int murder = MatrixPick(seed, 11, 17);
                    m.SetFlag(26, murder);
                    m.SetNumber(SaveModel.Kills, MatrixPick(seed, 12, 999999));
                    m.SetNumber(SaveModel.Plot, MatrixPick(seed, 13, 221));
                    m.SetFun(MatrixPick(seed, 14, 100) + 1);
                    if (murder >= 16)
                    {
                        m.SetNumber(SaveModel.Lv, 20);
                        m.SetNumber(SaveModel.Xp, 99999);
                    }
                    else if (murder == 0 && MatrixPick(seed, 15, 2) == 0)
                    {
                        m.SetNumber(SaveModel.Lv, 1);
                        m.SetNumber(SaveModel.Xp, 0);
                    }
                });
            }
        }

        private void AddMatrixRooms(List<FeatureDef> list, int count)
        {
            Color accent = Color.FromArgb(54, 151, 255);
            RoomWarp[] rooms = allRooms == null || allRooms.Length == 0 ? new RoomWarp[] { new RoomWarp(4, "room_area1") } : allRooms;
            for (int i = 0; i < count; i++)
            {
                int seed = i;
                RoomWarp room = rooms[seed % rooms.Length];
                AddFeature(list, "100K Room Teleports", "Teleport To " + CleanRoomName(room.Name), "Sets current room and plot for " + CleanRoomName(room.Name) + ".", accent, delegate(SaveModel m, Random r)
                {
                    RoomWarp chosen = rooms[MatrixPick(seed, 21, rooms.Length)];
                    m.SetNumber(SaveModel.Room, chosen.Id);
                    m.SetNumber(SaveModel.Plot, MatrixPick(seed, 22, 221));
                });
            }
        }

        private void AddMatrixInventory(List<FeatureDef> list, int count)
        {
            Color accent = Color.FromArgb(255, 195, 70);
            int[] normal = ItemCatalog.NormalIds();
            int[] tokens = ItemCatalog.BattleTokenIds();
            if (normal.Length == 0) normal = new int[] { 0 };
            if (tokens.Length == 0) tokens = new int[] { 9001 };
            int[] weapons = new int[] { 3, 13, 14, 25, 45, 47, 49, 51, 52 };
            int[] armors = new int[] { 4, 12, 15, 24, 44, 46, 48, 50, 53, 64 };
            for (int i = 0; i < count; i++)
            {
                int seed = i;
                AddFeature(list, "100K Inventory Kits", "Fill Inventory #" + seed.ToString("00000"), "Sets inventory slots, weapon, and armor.", accent, delegate(SaveModel m, Random r)
                {
                    int[] ids = new int[8];
                    for (int slot = 0; slot < ids.Length; slot++)
                    {
                        bool useToken = MatrixPick(seed, 31 + slot, 5) == 0;
                        ids[slot] = useToken ? tokens[MatrixPick(seed, 41 + slot, tokens.Length)] : normal[MatrixPick(seed, 51 + slot, normal.Length)];
                    }
                    m.SetInventory(ids);
                    m.Equip(weapons[MatrixPick(seed, 61, weapons.Length)], armors[MatrixPick(seed, 62, armors.Length)]);
                });
            }
        }

        private void AddMatrixFlags(List<FeatureDef> list, int count)
        {
            Color accent = Color.FromArgb(125, 112, 255);
            for (int i = 0; i < count; i++)
            {
                int seed = i;
                int baseFlag = seed % 512;
                AddFeature(list, "100K Save Flag Tools", "Set Flags Near " + baseFlag.ToString(), "Changes five global flags and plot value.", accent, delegate(SaveModel m, Random r)
                {
                    int start = seed % 512;
                    for (int offset = 0; offset < 5; offset++)
                    {
                        m.SetFlag((start + offset) % 512, MatrixPick(seed, 71 + offset, 17));
                    }
                    m.SetNumber(SaveModel.Plot, MatrixPick(seed, 80, 221));
                });
            }
        }

        private void AddMatrixChaos(List<FeatureDef> list, int count)
        {
            Color accent = Color.FromArgb(255, 120, 76);
            RoomWarp[] rooms = allRooms == null || allRooms.Length == 0 ? new RoomWarp[] { new RoomWarp(4, "room_area1") } : allRooms;
            int[] normal = ItemCatalog.NormalIds();
            int[] tokens = ItemCatalog.BattleTokenIds();
            if (normal.Length == 0) normal = new int[] { 0 };
            if (tokens.Length == 0) tokens = new int[] { 9001 };
            for (int i = 0; i < count; i++)
            {
                int seed = i;
                AddFeature(list, "100K Chaos Presets", "Full Chaos #" + seed.ToString("00000"), "Randomizes stats, room, route, items, FUN, and flags.", accent, delegate(SaveModel m, Random r)
                {
                    int lv = MatrixPick(seed, 91, 5000) + 1;
                    int hp = MatrixPick(seed, 92, PowerMax - 1) + 1;
                    int damage = MatrixPick(seed, 93, PowerMax);
                    int xp = MatrixPick(seed, 94, PowerMax);
                    int gold = MatrixPick(seed, 95, PowerMax);
                    int kills = MatrixPick(seed, 96, 999999);
                    m.SetCoreStats(lv, hp, damage, xp, gold, kills);
                    m.SetFun(MatrixPick(seed, 97, 100) + 1);
                    m.SetFlag(26, MatrixPick(seed, 98, 17));
                    m.SetNumber(SaveModel.Plot, MatrixPick(seed, 99, 221));
                    m.SetNumber(SaveModel.Room, rooms[MatrixPick(seed, 100, rooms.Length)].Id);
                    int[] ids = new int[8];
                    for (int slot = 0; slot < ids.Length; slot++)
                    {
                        ids[slot] = MatrixPick(seed, 110 + slot, 3) == 0 ? tokens[MatrixPick(seed, 120 + slot, tokens.Length)] : normal[MatrixPick(seed, 130 + slot, normal.Length)];
                    }
                    m.SetInventory(ids);
                    for (int offset = 0; offset < 6; offset++)
                    {
                        m.SetFlag(MatrixPick(seed, 140 + offset, 512), MatrixPick(seed, 150 + offset, 17));
                    }
                });
            }
        }

        private int MatrixPick(int seed, int salt, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }
            unchecked
            {
                int x = seed;
                x ^= salt * 374761393;
                x = (x * 1103515245) + 12345;
                x ^= (x >> 13);
                x *= 1274126177;
                if (x == int.MinValue)
                {
                    x = 0;
                }
                if (x < 0)
                {
                    x = -x;
                }
                return x % modulo;
            }
        }

        private void SetRandomRoom(SaveModel m, Random r, bool playerOnly)
        {
            RoomWarp[] source = playerOnly ? playerRooms : allRooms;
            if (source == null || source.Length == 0)
            {
                source = allRooms;
            }
            if (source == null || source.Length == 0)
            {
                return;
            }
            RoomWarp room = source[r.Next(source.Length)];
            m.SetNumber(SaveModel.Room, room.Id);
        }

        private string RoomName(int id)
        {
            RoomWarp room = allRooms.FirstOrDefault(delegate(RoomWarp r) { return r.Id == id; });
            return room == null ? "room_" + id.ToString() : room.Name;
        }

        private string CleanRoomName(string name)
        {
            string text = Regex.Replace(name ?? "", "^room_", "", RegexOptions.IgnoreCase);
            text = text.Replace('_', ' ').Trim();
            if (text.Length == 0)
            {
                return "Room";
            }
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
        }

        private int LevelHp(int lv)
        {
            int[] hp = new int[] { 0, 20, 24, 28, 32, 36, 40, 44, 48, 52, 56, 60, 64, 68, 72, 76, 80, 84, 88, 92, 99 };
            if (lv >= 0 && lv < hp.Length)
            {
                return hp[lv];
            }
            long value = 16L + ((long)lv * 4L);
            if (value > PowerMax) return PowerMax;
            return (int)value;
        }

        private int LevelDamage(int lv)
        {
            long value = lv == 20 ? 30L : 8L + ((long)lv * 2L);
            if (value > PowerMax) return PowerMax;
            return (int)value;
        }

        private int LevelXp(int lv)
        {
            int[] xp = new int[] { 0, 0, 10, 30, 70, 120, 200, 300, 500, 800, 1200, 1700, 2500, 3500, 5000, 7000, 10000, 15000, 25000, 50000, 99999 };
            if (lv >= 0 && lv < xp.Length)
            {
                return xp[lv];
            }
            long value = 99999L + (((long)lv - 20L) * 50000L);
            if (value > PowerMax) return PowerMax;
            if (value < 0) return 0;
            return (int)value;
        }
    }

    internal sealed class InventoryForgeForm : Form
    {
        private readonly SaveModel model;
        private readonly ComboBox[] combos = new ComboBox[8];
        private readonly NumericUpDown[] raws = new NumericUpDown[8];
        private bool syncing;

        public InventoryForgeForm(SaveModel model)
        {
            this.model = model;
            Text = "Inventory Forge";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 620);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(9, 9, 13);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildUi();
            LoadSlots();
        }

        private void BuildUi()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "INVENTORY FORGE";
            title.Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(28, 18);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Normal item IDs plus experimental raw attack tokens for modded builds.";
            subtitle.ForeColor = Color.FromArgb(218, 218, 224);
            subtitle.Location = new Point(32, 58);
            subtitle.AutoSize = true;
            header.Controls.Add(subtitle);

            Panel list = new Panel();
            list.Location = new Point(24, 112);
            list.Size = new Size(700, 310);
            list.BackColor = Color.FromArgb(17, 17, 22);
            list.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(Color.FromArgb(76, 76, 90)))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, list.Width - 1, list.Height - 1);
                }
            };
            Controls.Add(list);

            for (int i = 0; i < 8; i++)
            {
                int y = 18 + (i * 35);
                Label slot = new Label();
                slot.Text = "Slot " + (i + 1).ToString();
                slot.ForeColor = Color.FromArgb(190, 190, 200);
                slot.Location = new Point(18, y + 4);
                slot.Width = 58;
                list.Controls.Add(slot);

                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Location = new Point(84, y);
                combo.Width = 410;
                combo.BackColor = Color.FromArgb(7, 7, 10);
                combo.ForeColor = Color.White;
                combo.Items.AddRange(ItemCatalog.Items);
                combo.SelectedIndexChanged += ComboChanged;
                list.Controls.Add(combo);
                combos[i] = combo;

                NumericUpDown raw = new NumericUpDown();
                raw.Minimum = 0;
                raw.Maximum = 999999;
                raw.Location = new Point(510, y);
                raw.Width = 150;
                raw.BackColor = Color.FromArgb(7, 7, 10);
                raw.ForeColor = Color.White;
                raw.BorderStyle = BorderStyle.FixedSingle;
                raw.ValueChanged += RawChanged;
                list.Controls.Add(raw);
                raws[i] = raw;
            }

            Button heals = MakeButton("Best Heals", delegate { SetPreset(new int[] { 11, 43, 43, 40, 40, 21, 17, 17 }); }, Color.FromArgb(85, 220, 155));
            heals.Location = new Point(24, 440);
            Controls.Add(heals);

            Button gear = MakeButton("Weapons + Armor", delegate { SetPreset(new int[] { 52, 53, 50, 51, 49, 48, 47, 46 }); }, Color.FromArgb(54, 151, 255));
            gear.Location = new Point(184, 440);
            Controls.Add(gear);

            Button attacks = MakeButton("Boss Attacks", delegate
            {
                ProDialog.ShowInfo(this, "RAW ATTACK TOKENS", "These IDs are written into inventory slots, but vanilla Undertale does not map inventory items to battle objects.\r\n\r\nUse them with a mod that reads these raw IDs.", Color.FromArgb(255, 195, 70));
                SetPreset(new int[] { 9001, 9002, 9003, 9004, 9005, 9006, 9007, 9008 });
            }, Color.FromArgb(255, 63, 92));
            attacks.Location = new Point(344, 440);
            Controls.Add(attacks);

            Button clear = MakeButton("Clear", delegate { SetPreset(new int[] { 0, 0, 0, 0, 0, 0, 0, 0 }); }, Color.FromArgb(125, 112, 255));
            clear.Location = new Point(504, 440);
            Controls.Add(clear);

            Button apply = MakeButton("Apply", delegate { ApplyAndClose(); }, Color.FromArgb(85, 220, 155));
            apply.Location = new Point(424, 510);
            Controls.Add(apply);

            Button cancel = MakeButton("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); }, Color.FromArgb(100, 100, 112));
            cancel.Location = new Point(584, 510);
            Controls.Add(cancel);
        }

        private Button MakeButton(string text, Action click, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 140;
            b.Height = 42;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(17, 19, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(17, 19, 28);
            };
            b.Click += delegate { click(); };
            return b;
        }

        private void LoadSlots()
        {
            int[] ids = model.Inventory();
            for (int i = 0; i < 8; i++)
            {
                SetSlot(i, ids[i]);
            }
        }

        private void SetPreset(int[] ids)
        {
            for (int i = 0; i < 8; i++)
            {
                SetSlot(i, i < ids.Length ? ids[i] : 0);
            }
        }

        private void SetSlot(int index, int id)
        {
            syncing = true;
            raws[index].Value = SaveModel.Clamp(id, 0, 999999);
            ItemDef def = ItemCatalog.Find(id);
            combos[index].SelectedItem = def == null ? null : def;
            syncing = false;
        }

        private void ComboChanged(object sender, EventArgs e)
        {
            if (syncing) return;
            ComboBox combo = (ComboBox)sender;
            for (int i = 0; i < combos.Length; i++)
            {
                if (combos[i] == combo && combo.SelectedItem is ItemDef)
                {
                    syncing = true;
                    raws[i].Value = ((ItemDef)combo.SelectedItem).Id;
                    syncing = false;
                    return;
                }
            }
        }

        private void RawChanged(object sender, EventArgs e)
        {
            if (syncing) return;
            NumericUpDown raw = (NumericUpDown)sender;
            for (int i = 0; i < raws.Length; i++)
            {
                if (raws[i] == raw)
                {
                    syncing = true;
                    ItemDef def = ItemCatalog.Find((int)raw.Value);
                    combos[i].SelectedItem = def == null ? null : def;
                    syncing = false;
                    return;
                }
            }
        }

        private void ApplyAndClose()
        {
            int[] ids = new int[8];
            for (int i = 0; i < 8; i++)
            {
                ids[i] = (int)raws[i].Value;
            }
            model.SetInventory(ids);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class ChaosConsoleForm : Form
    {
        private readonly SaveModel model;
        private readonly Random random = new Random();
        private const int PowerMax = 999999999;
        private readonly TextBox roomSearchBox = new TextBox();
        private readonly ListBox roomList = new ListBox();
        private readonly Label logLabel = new Label();
        private readonly RoomWarp[] allRooms;
        private readonly RoomWarp[] playerRooms;

        public ChaosConsoleForm(SaveModel model)
        {
            this.model = model;
            allRooms = RoomCatalog.Load();
            playerRooms = RoomCatalog.PlayerRooms();
            Text = "Player Chaos Console";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 760);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(8, 8, 12);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildUi();
        }

        private void BuildUi()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "PLAYER CHAOS CONSOLE";
            title.Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(28, 18);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Randomize rooms, battle tokens, route flags, items, stats, plot, and raw save lines.";
            subtitle.ForeColor = Color.FromArgb(222, 222, 230);
            subtitle.Location = new Point(32, 58);
            subtitle.AutoSize = true;
            header.Controls.Add(subtitle);

            Panel board = MakePanel(new Point(22, 112), new Size(846, 560));
            Controls.Add(board);

            AddLabel(board, "Room Teleport", 22, 18);
            Label roomHint = new Label();
            roomHint.Text = "Search a room, select it, then warp. Use Safe Random for normal playable rooms.";
            roomHint.ForeColor = Color.FromArgb(176, 186, 204);
            roomHint.Location = new Point(22, 42);
            roomHint.AutoSize = true;
            board.Controls.Add(roomHint);

            roomSearchBox.Location = new Point(22, 68);
            roomSearchBox.Width = 360;
            roomSearchBox.Height = 28;
            roomSearchBox.BackColor = Color.FromArgb(7, 7, 10);
            roomSearchBox.ForeColor = Color.White;
            roomSearchBox.BorderStyle = BorderStyle.FixedSingle;
            roomSearchBox.TextChanged += delegate { RefreshRoomList(); };
            board.Controls.Add(roomSearchBox);

            roomList.Location = new Point(22, 104);
            roomList.Size = new Size(360, 96);
            roomList.BackColor = Color.FromArgb(7, 7, 10);
            roomList.ForeColor = Color.White;
            roomList.BorderStyle = BorderStyle.FixedSingle;
            roomList.IntegralHeight = false;
            roomList.DoubleClick += delegate { WarpSelectedRoom(); };
            board.Controls.Add(roomList);
            RefreshRoomList();
            SelectCurrentRoom();

            Button selectedRoom = MakeButton("Warp Selected", delegate { WarpSelectedRoom(); }, Color.FromArgb(54, 151, 255));
            selectedRoom.Location = new Point(402, 72);
            selectedRoom.Size = new Size(172, 44);
            board.Controls.Add(selectedRoom);

            Button randomRoom = MakeButton("Safe Random", delegate { RandomRoom(false); }, Color.FromArgb(85, 220, 155));
            randomRoom.Location = new Point(586, 72);
            randomRoom.Size = new Size(172, 44);
            board.Controls.Add(randomRoom);

            Button anyRoom = MakeButton("Any Room", delegate { RandomRoom(true); }, Color.FromArgb(255, 195, 70));
            anyRoom.Location = new Point(402, 128);
            anyRoom.Size = new Size(172, 44);
            board.Controls.Add(anyRoom);

            Button resetSearch = MakeButton("Clear Search", delegate { roomSearchBox.Text = ""; }, Color.FromArgb(125, 112, 255));
            resetSearch.Location = new Point(586, 128);
            resetSearch.Size = new Size(172, 44);
            board.Controls.Add(resetSearch);

            int x1 = 22;
            int x2 = 306;
            int x3 = 590;
            int y = 226;
            board.Controls.Add(TileButton("Random Stats", x1, y, delegate { RandomStats(); }, Color.FromArgb(85, 220, 155)));
            board.Controls.Add(TileButton("Random Items", x2, y, delegate { RandomItems(); }, Color.FromArgb(255, 195, 70)));
            board.Controls.Add(TileButton("Battle Tokens", x3, y, delegate { BattleTokens(); }, Color.FromArgb(255, 63, 92)));
            y += 70;
            board.Controls.Add(TileButton("Random Route", x1, y, delegate { RandomRoute(); }, Color.FromArgb(125, 112, 255)));
            board.Controls.Add(TileButton("Battle Flags", x2, y, delegate { RandomBattleFlags(); }, Color.FromArgb(255, 120, 76)));
            board.Controls.Add(TileButton("Secret FUN", x3, y, delegate { SecretFun(); }, Color.FromArgb(54, 151, 255)));
            y += 70;
            board.Controls.Add(TileButton("God Player", x1, y, delegate { GodPlayer(); }, Color.FromArgb(85, 220, 155)));
            board.Controls.Add(TileButton("Pain Mode", x2, y, delegate { PainMode(); }, Color.FromArgb(255, 195, 70)));
            board.Controls.Add(TileButton("Genocide Flags", x3, y, delegate { GenocideFlags(); }, Color.FromArgb(255, 63, 92)));
            y += 70;
            board.Controls.Add(TileButton("Chaos Run", x1, y, delegate { ChaosRun(); }, Color.FromArgb(255, 63, 92)));
            board.Controls.Add(TileButton("Raw Line Lab", x2, y, delegate { RawLineLab(); }, Color.FromArgb(125, 112, 255)));
            board.Controls.Add(TileButton("Refresh Live", x3, y, delegate { model.WriteLiveConfig(true); Log("codex_live.ini refreshed from this in-memory save."); }, Color.FromArgb(54, 151, 255)));

            logLabel.Text = "Ready. Nothing writes to disk until the main window's Write Save button.";
            logLabel.ForeColor = Color.FromArgb(205, 205, 214);
            logLabel.Location = new Point(24, 494);
            logLabel.Size = new Size(794, 46);
            board.Controls.Add(logLabel);

            Button close = MakeButton("Done", delegate { Close(); }, Color.FromArgb(85, 220, 155));
            close.Location = new Point(728, 676);
            Controls.Add(close);
        }

        private Panel MakePanel(Point location, Size size)
        {
            Panel p = new Panel();
            p.Location = location;
            p.Size = size;
            p.BackColor = Color.FromArgb(17, 17, 23);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(76, 76, 90), 1f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                }
            };
            return p;
        }

        private void AddLabel(Control parent, string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(180, 180, 190);
            label.Location = new Point(x, y);
            label.AutoSize = true;
            parent.Controls.Add(label);
        }

        private Button TileButton(string text, int x, int y, Action click, Color accent)
        {
            Button b = MakeButton(text, click, accent);
            b.Location = new Point(x, y);
            b.Width = 224;
            b.Height = 48;
            return b;
        }

        private Button MakeButton(string text, Action click, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 140;
            b.Height = 42;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(17, 19, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(17, 19, 28);
            };
            b.Click += delegate { click(); };
            return b;
        }

        private void RefreshRoomList()
        {
            string search = (roomSearchBox.Text ?? "").Trim().ToLowerInvariant();
            RoomWarp previous = roomList.SelectedItem as RoomWarp;
            roomList.BeginUpdate();
            roomList.Items.Clear();
            foreach (RoomWarp room in allRooms)
            {
                string haystack = (room.Id.ToString() + " " + room.Name + " " + room.Name.Replace('_', ' ')).ToLowerInvariant();
                if (search.Length == 0 || haystack.Contains(search))
                {
                    roomList.Items.Add(room);
                }
            }
            if (previous != null)
            {
                for (int i = 0; i < roomList.Items.Count; i++)
                {
                    RoomWarp item = roomList.Items[i] as RoomWarp;
                    if (item != null && item.Id == previous.Id)
                    {
                        roomList.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (roomList.SelectedIndex < 0 && roomList.Items.Count > 0)
            {
                roomList.SelectedIndex = 0;
            }
            roomList.EndUpdate();
        }

        private void SelectCurrentRoom()
        {
            int current = model.GetNumber(SaveModel.Room, 4);
            RoomWarp match = allRooms.FirstOrDefault(delegate(RoomWarp room) { return room.Id == current; });
            if (match != null)
            {
                string clean = match.Name.ToLowerInvariant().Replace('_', ' ');
                roomSearchBox.Text = clean;
                RefreshRoomList();
                for (int i = 0; i < roomList.Items.Count; i++)
                {
                    RoomWarp item = roomList.Items[i] as RoomWarp;
                    if (item != null && item.Id == match.Id)
                    {
                        roomList.SelectedIndex = i;
                        return;
                    }
                }
                roomList.Items.Insert(0, match);
                roomList.SelectedIndex = 0;
            }
            else if (roomList.Items.Count > 0)
            {
                roomList.SelectedIndex = 0;
            }
        }

        private void WarpSelectedRoom()
        {
            RoomWarp room = roomList.SelectedItem as RoomWarp;
            if (room == null) return;
            model.SetNumber(SaveModel.Room, room.Id);
            Log("Teleport set to " + room.ToString() + ". Press Write Save in the main window to commit.");
        }

        private void RandomRoom(bool anyRoom)
        {
            RoomWarp[] source = anyRoom ? allRooms : playerRooms;
            if (source.Length == 0) source = allRooms;
            RoomWarp room = source[random.Next(source.Length)];
            roomSearchBox.Text = "";
            RefreshRoomList();
            for (int i = 0; i < roomList.Items.Count; i++)
            {
                RoomWarp item = roomList.Items[i] as RoomWarp;
                if (item != null && item.Id == room.Id)
                {
                    roomList.SelectedIndex = i;
                    break;
                }
            }
            model.SetNumber(SaveModel.Room, room.Id);
            Log((anyRoom ? "Any-room teleport" : "Safe random teleport") + " set to " + room.ToString() + ".");
        }

        private void RandomStats()
        {
            int lv = random.Next(1, 5001);
            int hp = SaveModel.Clamp(16 + (lv * 4), 1, PowerMax);
            model.SetFun(random.Next(1, 101));
            model.SetNumber(SaveModel.Lv, lv);
            model.SetNumber(SaveModel.MaxHp, hp);
            model.SetNumber(SaveModel.MaxEn, hp);
            model.SetDamagePower(random.Next(0, PowerMax + 1));
            model.SetNumber(SaveModel.Xp, random.Next(0, PowerMax + 1));
            model.SetNumber(SaveModel.Gold, random.Next(0, PowerMax + 1));
            model.SetNumber(SaveModel.Kills, random.Next(0, 201));
            model.SetNumber(SaveModel.Time, random.Next(0, 999999));
            Log("Randomized uncapped FUN, LV, HP, DMG, EXP, gold, kills, and time.");
        }

        private void RandomItems()
        {
            int[] normal = ItemCatalog.NormalIds();
            int[] ids = new int[8];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = normal[random.Next(normal.Length)];
            }
            model.SetInventory(ids);
            Log("Inventory filled with random regular Undertale items.");
        }

        private void BattleTokens()
        {
            int[] tokens = ItemCatalog.BattleTokenIds();
            int[] ids = new int[8];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = tokens[i % tokens.Length];
            }
            model.SetInventory(ids);
            Log("Inventory filled with raw boss attack tokens. These need a mod to become real attacks.");
        }

        private void RandomRoute()
        {
            int murder = random.Next(0, 17);
            model.SetNumber(SaveModel.Flags + 26, murder);
            if (murder == 0)
            {
                model.SetNumber(SaveModel.Kills, random.Next(0, 25));
            }
            else
            {
                model.SetNumber(SaveModel.Kills, Math.Max(murder * 6, random.Next(1, 160)));
            }
            Log("Route override flag randomized to " + murder.ToString() + "/16.");
        }

        private void RandomBattleFlags()
        {
            int[] oneBitFlags = new int[] { 27, 45, 52, 53, 54, 57, 67, 81, 251, 252, 350, 397, 402, 425 };
            foreach (int flag in oneBitFlags)
            {
                model.SetNumber(SaveModel.Flags + flag, random.Next(0, 2));
            }
            model.SetNumber(SaveModel.Flags + 202, random.Next(0, 21));
            model.SetNumber(SaveModel.Flags + 203, random.Next(0, 17));
            model.SetNumber(SaveModel.Flags + 204, random.Next(0, 19));
            model.SetNumber(SaveModel.Flags + 205, random.Next(0, 41));
            model.SetNumber(SaveModel.Flags + 493, random.Next(0, 13));
            model.SetNumber(SaveModel.Plot, random.Next(0, 220));
            Log("Randomized major battle, route, plot, and boss-progress flags.");
        }

        private void SecretFun()
        {
            int[] funs = new int[] { 2, 40, 45, 46, 47, 56, 61, 62, 63, 65, 66, 80, 81, 91 };
            int fun = funs[random.Next(funs.Length)];
            model.SetFun(fun);
            Log("FUN set to secret-leaning value " + fun.ToString() + ".");
        }

        private void GodPlayer()
        {
            model.SetFun(66);
            model.SetNumber(SaveModel.Lv, PowerMax);
            model.SetNumber(SaveModel.MaxHp, PowerMax);
            model.SetNumber(SaveModel.MaxEn, PowerMax);
            model.SetDamagePower(PowerMax);
            model.SetNumber(SaveModel.Xp, PowerMax);
            model.SetNumber(SaveModel.Gold, PowerMax);
            model.SetNumber(SaveModel.Kills, 999);
            model.SetInventory(new int[] { 11, 43, 43, 40, 52, 53, 9001, 9002 });
            Log("God Player build applied: LV/HP/DMG/EXP/gold uncapped to 999,999,999.");
        }

        private void PainMode()
        {
            model.SetFun(13);
            model.SetNumber(SaveModel.Lv, 1);
            model.SetNumber(SaveModel.MaxHp, 20);
            model.SetNumber(SaveModel.MaxEn, 20);
            model.SetDamagePower(10);
            model.SetNumber(SaveModel.Xp, 0);
            model.SetNumber(SaveModel.Gold, 0);
            model.SetNumber(SaveModel.Kills, 0);
            model.SetNumber(SaveModel.Flags + 26, 0);
            model.SetInventory(new int[] { 22, 22, 22, 22, 22, 22, 22, 22 });
            Log("Pain Mode applied: LV1, no gold, no kills, all Temmie Flakes.");
        }

        private void GenocideFlags()
        {
            model.SetNumber(SaveModel.Flags + 202, 20);
            model.SetNumber(SaveModel.Flags + 45, 4);
            model.SetNumber(SaveModel.Flags + 52, 1);
            model.SetNumber(SaveModel.Flags + 53, 1);
            model.SetNumber(SaveModel.Flags + 54, 1);
            model.SetNumber(SaveModel.Flags + 57, 2);
            model.SetNumber(SaveModel.Flags + 203, 16);
            model.SetNumber(SaveModel.Flags + 67, 1);
            model.SetNumber(SaveModel.Flags + 81, 1);
            model.SetNumber(SaveModel.Flags + 252, 1);
            model.SetNumber(SaveModel.Flags + 204, 18);
            model.SetNumber(SaveModel.Flags + 251, 1);
            model.SetNumber(SaveModel.Flags + 350, 1);
            model.SetNumber(SaveModel.Flags + 402, 1);
            model.SetNumber(SaveModel.Flags + 397, 1);
            model.SetNumber(SaveModel.Flags + 205, 40);
            model.SetNumber(SaveModel.Flags + 425, 1);
            model.SetNumber(SaveModel.Flags + 27, 0);
            model.SetNumber(SaveModel.Flags + 26, 16);
            model.SetNumber(SaveModel.Lv, 20);
            model.SetNumber(SaveModel.MaxHp, 99);
            model.SetNumber(SaveModel.MaxEn, 99);
            model.SetDamagePower(30);
            model.SetNumber(SaveModel.Kills, 999);
            Log("Genocide route chain flags pushed to the murder-level 16 threshold.");
        }

        private void ChaosRun()
        {
            RandomRoom(false);
            RandomStats();
            RandomItems();
            RandomRoute();
            RandomBattleFlags();
            if (random.Next(0, 2) == 1)
            {
                BattleTokens();
            }
            Log("Full Chaos Run applied. Write Save from the main window when you are ready.");
        }

        private void RawLineLab()
        {
            using (RawLineLabForm f = new RawLineLabForm(model))
            {
                f.ShowDialog(this);
                SelectCurrentRoom();
                Log("Returned from Raw Line Lab.");
            }
        }

        private void Log(string text)
        {
            logLabel.Text = text;
        }
    }

    internal sealed class RawLineLabForm : Form
    {
        private readonly SaveModel model;
        private readonly NumericUpDown lineBox = new NumericUpDown();
        private readonly TextBox lineValue = new TextBox();
        private readonly NumericUpDown flagBox = new NumericUpDown();
        private readonly NumericUpDown flagValue = new NumericUpDown();
        private readonly Label status = new Label();
        private readonly Random random = new Random();

        public RawLineLabForm(SaveModel model)
        {
            this.model = model;
            Text = "Raw Line Lab";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(8, 8, 12);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildUi();
            ReadLine();
            ReadFlag();
        }

        private void BuildUi()
        {
            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 84;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "RAW LINE LAB";
            title.Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Location = new Point(26, 18);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Edit any file0 line or any global flag directly.";
            subtitle.ForeColor = Color.FromArgb(222, 222, 230);
            subtitle.Location = new Point(30, 54);
            subtitle.AutoSize = true;
            header.Controls.Add(subtitle);

            AddLabel("Line number", 28, 112);
            lineBox.Minimum = 1;
            lineBox.Maximum = 2000;
            lineBox.Value = 57;
            lineBox.Location = new Point(28, 136);
            lineBox.Width = 130;
            StyleNumber(lineBox);
            Controls.Add(lineBox);

            AddLabel("Line value", 178, 112);
            lineValue.Location = new Point(178, 136);
            lineValue.Width = 260;
            lineValue.BackColor = Color.FromArgb(7, 7, 10);
            lineValue.ForeColor = Color.White;
            lineValue.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(lineValue);

            Button readLine = MakeButton("Read", delegate { ReadLine(); }, Color.FromArgb(54, 151, 255));
            readLine.Location = new Point(456, 132);
            Controls.Add(readLine);
            Button setLine = MakeButton("Set", delegate { SetLine(); }, Color.FromArgb(85, 220, 155));
            setLine.Location = new Point(526, 132);
            setLine.Width = 58;
            Controls.Add(setLine);

            AddLabel("Flag index", 28, 206);
            flagBox.Minimum = 0;
            flagBox.Maximum = 999;
            flagBox.Value = 26;
            flagBox.Location = new Point(28, 230);
            flagBox.Width = 130;
            StyleNumber(flagBox);
            Controls.Add(flagBox);

            AddLabel("Flag value", 178, 206);
            flagValue.Minimum = -999999;
            flagValue.Maximum = 999999;
            flagValue.Location = new Point(178, 230);
            flagValue.Width = 130;
            StyleNumber(flagValue);
            Controls.Add(flagValue);

            Button readFlag = MakeButton("Read Flag", delegate { ReadFlag(); }, Color.FromArgb(54, 151, 255));
            readFlag.Location = new Point(328, 226);
            Controls.Add(readFlag);
            Button setFlag = MakeButton("Set Flag", delegate { SetFlag(); }, Color.FromArgb(85, 220, 155));
            setFlag.Location = new Point(432, 226);
            Controls.Add(setFlag);
            Button randomFlag = MakeButton("Random", delegate { RandomFlag(); }, Color.FromArgb(255, 195, 70));
            randomFlag.Location = new Point(526, 226);
            randomFlag.Width = 58;
            Controls.Add(randomFlag);

            status.Text = "Line 57 is flag 26. Regular inventory item slots begin at lines 13, 15, 17...";
            status.ForeColor = Color.FromArgb(205, 205, 214);
            status.Location = new Point(30, 298);
            status.Size = new Size(540, 44);
            Controls.Add(status);

            Button done = MakeButton("Done", delegate { Close(); }, Color.FromArgb(85, 220, 155));
            done.Location = new Point(474, 356);
            done.Width = 110;
            Controls.Add(done);
        }

        private void AddLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(180, 180, 190);
            label.Location = new Point(x, y);
            label.AutoSize = true;
            Controls.Add(label);
        }

        private void StyleNumber(NumericUpDown box)
        {
            box.BackColor = Color.FromArgb(7, 7, 10);
            box.ForeColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
        }

        private Button MakeButton(string text, Action click, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 88;
            b.Height = 34;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(17, 19, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(17, 19, 28);
            };
            b.Click += delegate { click(); };
            return b;
        }

        private void ReadLine()
        {
            int line = (int)lineBox.Value;
            lineValue.Text = model.Get(line - 1);
            status.Text = "Read line " + line.ToString() + ".";
        }

        private void SetLine()
        {
            int line = (int)lineBox.Value;
            model.Set(line - 1, lineValue.Text);
            status.Text = "Set line " + line.ToString() + ".";
        }

        private void ReadFlag()
        {
            int flag = (int)flagBox.Value;
            flagValue.Value = SaveModel.Clamp(model.GetNumber(SaveModel.Flags + flag, 0), -999999, 999999);
            status.Text = "Read flag " + flag.ToString() + " from line " + (SaveModel.Flags + flag + 1).ToString() + ".";
        }

        private void SetFlag()
        {
            int flag = (int)flagBox.Value;
            model.SetNumber(SaveModel.Flags + flag, (int)flagValue.Value);
            status.Text = "Set flag " + flag.ToString() + " on line " + (SaveModel.Flags + flag + 1).ToString() + ".";
        }

        private void RandomFlag()
        {
            flagValue.Value = random.Next(0, 101);
            SetFlag();
        }
    }

    internal sealed class ProDialog : Form
    {
        private bool answer;

        private ProDialog(string title, string message, Color accent, bool confirm)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(520, confirm ? 320 : 290);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(9, 9, 13);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            GradientHeader header = new GradientHeader();
            header.Dock = DockStyle.Top;
            header.Height = 82;
            Controls.Add(header);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(26, 22);
            header.Controls.Add(titleLabel);

            Label body = new Label();
            body.Text = message;
            body.ForeColor = Color.FromArgb(225, 225, 232);
            body.Location = new Point(30, 106);
            body.Size = new Size(455, 110);
            body.AutoEllipsis = true;
            Controls.Add(body);

            Panel accentLine = new Panel();
            accentLine.BackColor = accent;
            accentLine.Location = new Point(30, 222);
            accentLine.Size = new Size(455, 3);
            Controls.Add(accentLine);

            if (confirm)
            {
                Button yes = MakeButton("Commit", accent);
                yes.Location = new Point(230, 244);
                yes.Click += delegate { answer = true; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(yes);

                Button no = MakeButton("Cancel", Color.FromArgb(100, 100, 112));
                no.Location = new Point(365, 244);
                no.Click += delegate { answer = false; DialogResult = DialogResult.Cancel; Close(); };
                Controls.Add(no);
            }
            else
            {
                Button ok = MakeButton("OK", accent);
                ok.Location = new Point(365, 235);
                ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
                Controls.Add(ok);
            }
        }

        private Button MakeButton(string text, Color accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 110;
            b.Height = 38;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 2;
            b.BackColor = Color.FromArgb(17, 19, 28);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += delegate
            {
                b.BackColor = Color.FromArgb(Math.Min(255, accent.R / 3 + 28), Math.Min(255, accent.G / 3 + 28), Math.Min(255, accent.B / 3 + 34));
            };
            b.MouseLeave += delegate
            {
                b.BackColor = Color.FromArgb(17, 19, 28);
            };
            return b;
        }

        public static void ShowInfo(IWin32Window owner, string title, string message, Color accent)
        {
            using (ProDialog d = new ProDialog(title, message, accent, false))
            {
                d.ShowDialog(owner);
            }
        }

        public static bool ShowConfirm(IWin32Window owner, string title, string message, Color accent)
        {
            using (ProDialog d = new ProDialog(title, message, accent, true))
            {
                d.ShowDialog(owner);
                return d.answer;
            }
        }
    }
}
