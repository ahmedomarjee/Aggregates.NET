﻿using NServiceBus;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates
{

    public static class ConsumerConfiguration
    {
        public static void SetParallelism(this ExposeSettings settings, Int32 Parallelism)
        {
            settings.GetSettings().Set("Parallelism", Parallelism);
        }
        public static void SetBucketHeartbeats(this ExposeSettings settings, Int32 Seconds)
        {
            settings.GetSettings().Set("BucketHeartbeats", Seconds);
        }
        public static void SetBucketExpiration(this ExposeSettings settings, Int32 Seconds)
        {
            settings.GetSettings().Set("BucketExpiration", Seconds);
        }
        public static void SetBucketCount(this ExposeSettings settings, Int32 Count)
        {
            settings.GetSettings().Set("BucketCount", Count);
        }
        public static void SetBucketsHandled(this ExposeSettings settings, Int32 Count)
        {
            settings.GetSettings().Set("BucketsHandled", Count);
        }
        public static void ParallelHandlers(this ExposeSettings settings, Boolean Parrallel)
        {
            settings.GetSettings().Set("ParallelHandlers", Parrallel);
        }
        public static void EventDropIsFatal(this ExposeSettings settings, Boolean Fatal)
        {
            settings.GetSettings().Set("EventDropIsFatal", Fatal);
        }
        public static void MaxProcessingQueueSize(this ExposeSettings settings, Int32 Size)
        {
            settings.GetSettings().Set("MaxQueueSize", Size);
        }
        /// <summary>
        /// When true, application will not consume events unless ALL buckets are claimed
        /// Recommended unless you don't care about ordering
        /// </summary>
        public static void PauseOnFreeBuckets(this ExposeSettings settings, Boolean Pause)
        {
            settings.GetSettings().Set("PauseOnFreeBuckets", Pause);
        }
    }
}
