﻿using System;
using System.Net.Http;
using Common.Logging;
using LaunchDarkly.Client;
using LaunchDarkly.Common;

namespace LaunchDarkly.Xamarin
{
    internal static class Factory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Factory));

        internal static IFlagCacheManager CreateFlagCacheManager(Configuration configuration, 
                                                                 IPersistentStorage persister,
                                                                 IFlagChangedEventManager flagChangedEventManager,
                                                                 User user)
        {
            if (configuration._flagCacheManager != null)
            {
                return configuration._flagCacheManager;
            }
            else
            {
                var inMemoryCache = new UserFlagInMemoryCache();
                var deviceCache = configuration.PersistFlagValues ? new UserFlagDeviceCache(persister) as IUserFlagCache : new NullUserFlagCache();
                return new FlagCacheManager(inMemoryCache, deviceCache, flagChangedEventManager, user);
            }
        }

        internal static IConnectivityStateManager CreateConnectivityStateManager(Configuration configuration)
        {
            return configuration._connectivityStateManager ?? new DefaultConnectivityStateManager();
        }

        internal static Func<IMobileUpdateProcessor> CreateUpdateProcessorFactory(Configuration configuration, User user,
            IFlagCacheManager flagCacheManager, bool inBackground)
        {
            return () =>
            {
                if (configuration._updateProcessorFactory != null)
                {
                    return configuration._updateProcessorFactory(configuration, flagCacheManager, user);
                }

                var featureFlagRequestor = new FeatureFlagRequestor(configuration, user);
                if (configuration.IsStreamingEnabled && !inBackground)
                {
                    return new MobileStreamingProcessor(configuration, flagCacheManager, featureFlagRequestor, user, null);
                }
                else
                {
                    return new MobilePollingProcessor(featureFlagRequestor,
                                                      flagCacheManager,
                                                      user,
                                                      inBackground ? configuration.BackgroundPollingInterval : configuration.PollingInterval,
                                                      inBackground ? configuration.BackgroundPollingInterval : TimeSpan.Zero);
                }
            };
        }

        internal static IEventProcessor CreateEventProcessor(Configuration configuration)
        {
            if (configuration._eventProcessor != null)
            {
                return configuration._eventProcessor;
            }
            HttpClient httpClient = Util.MakeHttpClient(configuration.HttpRequestConfiguration, MobileClientEnvironment.Instance);
            return new DefaultEventProcessor(configuration.EventProcessorConfiguration, null, httpClient, Constants.EVENTS_PATH);
        }

        internal static IPersistentStorage CreatePersistentStorage(Configuration configuration)
        {
            return configuration._persistentStorage ?? new DefaultPersistentStorage();
        }

        internal static IDeviceInfo CreateDeviceInfo(Configuration configuration)
        {
            return configuration._deviceInfo ?? new DefaultDeviceInfo();
        }

        internal static IFlagChangedEventManager CreateFlagChangedEventManager(Configuration configuration)
        {
            return configuration._flagChangedEventManager ?? new FlagChangedEventManager();
        }
    }
}
