﻿using System;
using Android.OS;

namespace LaunchDarkly.Xamarin.PlatformSpecific
{
    internal static partial class AsyncScheduler
    {
        private static void PlatformScheduleAction(Action a)
        {
            // Note that this logic is different from the implementation of the equivalent method in Xamarin Essentials
            // (https://github.com/xamarin/Essentials/blob/master/Xamarin.Essentials/MainThread/MainThread.android.cs);
            // it creates a new Handler object each time rather than lazily creating a static one. This avoids a potential
            // race condition, at the expense of creating more ephemeral objects. However, in our use case we do not
            // expect this method to be called very frequently since we are using it for flag change listeners only.
            var handler = new Handler(Looper.MainLooper);
            handler.Post(a);
        }
    }
}
