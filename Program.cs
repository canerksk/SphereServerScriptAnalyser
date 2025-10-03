using AutoUpdaterDotNET;
using Microsoft.VisualBasic.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace SphereServerScriptAnalyser
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            CultureInfo selectedCulture;


            Assembly assembly = Assembly.GetExecutingAssembly();

            // Assembly versiyonunu al
            Version version = assembly.GetName().Version;
            string productVersion = FileVersionInfo
            .GetVersionInfo(Assembly.GetExecutingAssembly().Location)
            .ProductVersion;

            try
            {
                // 1) Kullanıcı ayarına bak
                var userLang = Properties.Main.Default.Language;

                if (!string.IsNullOrWhiteSpace(userLang))
                {
                    // Settings'te kayıtlı dil varsa → onu al
                    selectedCulture = new CultureInfo(userLang);
                }
                else
                {
                    // Settings boş → sistem kültürünü dene
                    var systemCulture = CultureInfo.InstalledUICulture;

                    if (systemCulture != null)
                        selectedCulture = systemCulture;
                    else
                        selectedCulture = new CultureInfo("en-US"); // fallback
                }
            }
            catch
            {
                // herhangi bir hata olursa → fallback en-US
                selectedCulture = new CultureInfo("en-US");
            }

            // Thread kültürlerini ayarla
            Thread.CurrentThread.CurrentCulture = selectedCulture;
            Thread.CurrentThread.CurrentUICulture = selectedCulture;

            // Normal WinForms init
            ApplicationConfiguration.Initialize();
            Form1 frm1 = new Form1();

            // Debug amaçlı form başlığında aktif dili göster
            Console.WriteLine("Active culture: " + selectedCulture.Name);
            //frm1.Text += $" ({selectedCulture.Name} - v{version})";
            frm1.Text += $" ({selectedCulture.Name} - v{productVersion})";

            Console.WriteLine($"Assembly Version: {version}");
            // AutoUpdater ayarları
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.ReportErrors = true;
            AutoUpdater.Start("https://uosoft.com.tr/api/sphere/script-analyser/Updater.xml");
            AutoUpdater.Mandatory = true;
            AutoUpdater.UpdateMode = Mode.Forced;
            AutoUpdater.TopMost = true;
            // Burada versiyonu geçir
            //AutoUpdater.InstalledVersion = version;
            AutoUpdater.InstalledVersion = new Version(productVersion);
            AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdater.InstallationPath = Application.StartupPath;

            Application.Run(frm1);
        }


        public static void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    DialogResult dialogResult;

                    if (args.Mandatory.Value)
                    {
                        dialogResult = MessageBox.Show(
                            $"Yeni bir sürüm mevcut: {args.CurrentVersion}. Mevcut sürüm: {args.InstalledVersion}. Bu güncelleme zorunludur. Güncellemeyi başlatmak için Tamam'a basın.",
                            "Güncelleme Mevcut",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        dialogResult = MessageBox.Show(
                            $"Yeni bir sürüm mevcut: {args.CurrentVersion}. Mevcut sürüm: {args.InstalledVersion}. Uygulamayı şimdi güncellemek ister misiniz?",
                            "Güncelleme Mevcut",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);
                    }

                    if (dialogResult == DialogResult.Yes || dialogResult == DialogResult.OK)
                    {
                        try
                        {
                            if (AutoUpdater.DownloadUpdate(args))
                            {
                                Application.Exit();
                            }
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show(exception.Message, exception.GetType().ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                //else
                //{
                    //MessageBox.Show("Uygulama güncel!", "Güncelleme Yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //}
            }
            else
            {
                MessageBox.Show(
                    args.Error is WebException
                        ? "Güncelleme sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edin ve tekrar deneyin."
                        : args.Error.Message,
                    "Güncelleme Kontrol Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }



    }
}
