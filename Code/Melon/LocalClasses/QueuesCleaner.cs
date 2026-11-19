using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.LocalClasses
{
    public static class QueuesCleaner
    {
        public static async Task RunAsync(CancellationToken stopping)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            try
            {
                while (await timer.WaitForNextTickAsync(stopping))
                {
                    var hours = StateManager.MelonSettings.QueueCleanupWaitInHours;
                    if (hours == -1 || !ServerAdministration.Instance.GetStatus().DatabaseConnected) continue;
                    try
                    {
                        var queues = StateManager.DbClient.GetDatabase("Melon").GetCollection<PlayQueue>("Queues");
                        var threshold = DateTime.UtcNow - TimeSpan.FromHours(hours);
                        await queues.DeleteManyAsync(q => !q.Favorite && q.LastListen < threshold, stopping);
                    }
                    catch (Exception ex) when (!stopping.IsCancellationRequested)
                    {
                        Serilog.Log.Warning(ex, "Queue cleanup failed");
                    }
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        }
    }
}
