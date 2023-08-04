using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Win32;
using System.Diagnostics;
using Timer = System.Timers.Timer;
using System.Data.SQLite;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Text;
using System.Runtime.InteropServices;

namespace ClientApp
{
    class ScreenCapture
    {
        private static Timer screenshotTimer;
        private static string serverUrl = "https://localhost:8080";
        private static bool isLocked = false;
        private static DBQueue dbQueue;
        private static ImageSender imageSender;
        private static DateTime lastInputTime = DateTime.UtcNow;
        private static TimeSpan idleThreshold = TimeSpan.FromSeconds(12);
        private static Configuration config;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public int dwTime;
        }

        static async Task Main(string[] args)
        {
            
            screenshotTimer = new Timer();
            screenshotTimer.Interval = TimeSpan.FromSeconds(3).TotalMilliseconds; 
            HeartBeat heartBeat = new HeartBeat("https://localhost:8080", 10, 10); //10 seconds, response timeout: 20 seconds
            Task heartBeatTask = heartBeat.StartAsync();
            string databaseFilePath = "C:/Users/ankit/Desktop/SqliteDBtest.db;Version=3;"; 
            string connectionString = $"Data Source={databaseFilePath};Version=3;";
            dbQueue = new DBQueue(connectionString, "C:/Users/ankit/Desktop/database");
            imageSender = new ImageSender(dbQueue, serverUrl, 3);
            config = new Configuration();

            screenshotTimer.Elapsed += async (sender, e) =>
            {
                if (!isLocked & !IsActivityDetected())
                {
                    Bitmap screenshot = CaptureScreen();
                    string imagePath = SaveScreenshot(screenshot);
                    byte[] screenshotBytes = ImageToByteArray(screenshot);

                    Console.WriteLine($"Screenshot saved: {imagePath}");

                    
                    await dbQueue.InsertImageAsync(screenshotBytes);
                }
            };
            screenshotTimer.Start();

            
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

           
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();

           
            screenshotTimer.Stop();
            screenshotTimer.Dispose();
        }

        

        static Bitmap CaptureScreen()
        {
            Rectangle screenBounds = new Rectangle(0, 0, 1920, 1080); 
            Bitmap screenshot = new Bitmap(screenBounds.Width, screenBounds.Height);
            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size);
            }
            return screenshot;
        }

        static byte[] ImageToByteArray(Image image)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
        static string SaveScreenshot(Bitmap screenshot)
        {
            string imageName = $"screenshot_{DateTime.Now:yyyyMMddHHmmssfff}.jpeg";
            string imagePath = Path.Combine("C:/Users/ankit/Desktop/images" , imageName);

            screenshot.Save(imagePath, ImageFormat.Jpeg);
            return imagePath;
        }


        static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                    isLocked = true;
                    Console.WriteLine("Machine locked or logged off.");
                    break;

                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                    isLocked = false;
                    Console.WriteLine("Machine unlocked or logged on.");
                    break;
            }
        }

        static bool IsActivityDetected()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            GetLastInputInfo(ref lii);

            DateTime lastInput = DateTime.Now.AddMilliseconds(-lii.dwTime);

            if (DateTime.UtcNow - lastInput > idleThreshold)
            {
                return false;
            }

            lastInputTime = DateTime.UtcNow;
            return true;
        }
        public class WindowsHookHelper
        {
            public delegate IntPtr HookDelegate(
                Int32 Code, IntPtr wParam, IntPtr lParam);

            [DllImport("User32.dll")]
            public static extern IntPtr CallNextHookEx(
                IntPtr hHook, Int32 nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("User32.dll")]
            public static extern IntPtr UnhookWindowsHookEx(IntPtr hHook);


            [DllImport("User32.dll")]
            public static extern IntPtr SetWindowsHookEx(
                Int32 idHook, HookDelegate lpfn, IntPtr hmod,
                Int32 dwThreadId);
        }
    }


      
}
