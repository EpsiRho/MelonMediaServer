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
        public static bool CleanerActive { get; set; }
        public static void StartCleaner()
        {
            if(CleanerActive)
            {
                return;
            }
            CleanerActive = true;

            Task.Run(CleanerThread);
        }
        private static void CleanerThread()
        {
            var mongoDb = StateManager.DbClient.GetDatabase("Melon");
            var collection = mongoDb.GetCollection<PlayQueue>("Queues");
            while (CleanerActive && StateManager.MelonSettings.QueueCleanupWaitInHours != -1)
            {
                var queues = collection.AsQueryable();
                foreach(var queue in queues)
                {
                    if (queue.Favorite)
                    {
                        continue;
                    }

                    var time = DateTime.Now.ToUniversalTime() - queue.LastListen;
                    if(time > TimeSpan.FromHours(StateManager.MelonSettings.QueueCleanupWaitInHours))
                    {
                        collection.DeleteOne(x => x._id == queue._id);
                    }
                }
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
            CleanerActive = false;
        }
    }
}
