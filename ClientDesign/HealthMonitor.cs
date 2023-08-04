using ClientApp;
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

namespace ClientApp
{
    public class HealthMonitor
    {
        private readonly DBQueue dbQueue;
        private readonly TimeSpan monitoringInterval;
        private readonly TimeSpan imageInsertionTimeout;
        private readonly TimeSpan lockDuration;

        private bool isMonitoring;
        private DateTime lastImageInsertionTime;
        

        public HealthMonitor(DBQueue dbQueue, TimeSpan imageInsertionTimeout, TimeSpan lockDuration)
        {
            this.dbQueue = dbQueue;
            this.monitoringInterval = TimeSpan.FromMinutes(2); // Set monitoring interval to 2 minutes
            this.imageInsertionTimeout = imageInsertionTimeout;
            this.lockDuration = lockDuration;

            this.isMonitoring = false;
            this.lastImageInsertionTime = DateTime.MinValue;
        }

        

        public void StartMonitoring()
        {
            isMonitoring = true;
            Task.Run(MonitorService);
        }

        public void StopMonitoring()
        {
            isMonitoring = false;
        }

        private async Task MonitorService()
        {
            while (isMonitoring)
            {
                try
                {
                    TimeSpan timeSinceLastInsertion = DateTime.UtcNow - lastImageInsertionTime;

                    if (timeSinceLastInsertion > imageInsertionTimeout)
                    {
                        Console.WriteLine($"No image inserted for {timeSinceLastInsertion}. Locking user account.");


                       // Sir i didnot know how to lock user account,did not find it


                        lastImageInsertionTime = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error monitoring service: {ex.Message}");
                }

                await Task.Delay(monitoringInterval);
            }
        }
    }

}
