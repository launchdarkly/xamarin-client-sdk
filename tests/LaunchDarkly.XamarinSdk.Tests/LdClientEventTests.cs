﻿using System;
using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Xamarin.Tests
{
    public class LdClientEventTests : BaseTest
    {
        private static readonly User user = User.WithKey("userkey");
        private MockEventProcessor eventProcessor = new MockEventProcessor();

        public LdClient MakeClient(User user, string flagsJson)
        {
            var config = TestUtil.ConfigWithFlagsJson(user, "appkey", flagsJson);
            config.EventProcessor(eventProcessor);
            return TestUtil.CreateClient(config.Build(), user);
        }

        [Fact]
        public void IdentifySendsIdentifyEvent()
        {
            using (LdClient client = MakeClient(user, "{}"))
            {
                User user1 = User.WithKey("userkey1");
                client.Identify(user1, TimeSpan.FromSeconds(1));
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user), // there's always an initial identify event
                    e => CheckIdentifyEvent(e, user1));
            }
        }

        [Fact]
        public void TrackSendsCustomEvent()
        {
            using (LdClient client = MakeClient(user, "{}"))
            {
                client.Track("eventkey");
                Assert.Collection(eventProcessor.Events, 
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        CustomEvent ce = Assert.IsType<CustomEvent>(e);
                        Assert.Equal("eventkey", ce.Key);
                        Assert.Equal(user.Key, ce.User.Key);
                        Assert.Equal(LdValue.Null, ce.Data);
                        Assert.Null(ce.MetricValue);
                    });
            }
        }

        [Fact]
        public void TrackWithDataSendsCustomEvent()
        {
            using (LdClient client = MakeClient(user, "{}"))
            {
                LdValue data = LdValue.Of("hi");
                client.Track("eventkey", data);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        CustomEvent ce = Assert.IsType<CustomEvent>(e);
                        Assert.Equal("eventkey", ce.Key);
                        Assert.Equal(user.Key, ce.User.Key);
                        Assert.Equal(data, ce.Data);
                        Assert.Null(ce.MetricValue);
                    });
            }
        }

        [Fact]
        public void TrackWithMetricValueSendsCustomEvent()
        {
            using (LdClient client = MakeClient(user, "{}"))
            {
                LdValue data = LdValue.Of("hi");
                double metricValue = 1.5;
                client.Track("eventkey", data, metricValue);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        CustomEvent ce = Assert.IsType<CustomEvent>(e);
                        Assert.Equal("eventkey", ce.Key);
                        Assert.Equal(user.Key, ce.User.Key);
                        Assert.Equal(data, ce.Data);
                        Assert.Equal(metricValue, ce.MetricValue);
                    });
            }
        }

        [Fact]
        public void VariationSendsFeatureEventForValidFlag()
        {
            string flagsJson = @"{""flag"":{
                ""value"":""a"",""variation"":1,""version"":1000,
                ""trackEvents"":true, ""debugEventsUntilDate"":2000 }}";
            using (LdClient client = MakeClient(user, flagsJson))
            {
                string result = client.StringVariation("flag", "b");
                Assert.Equal("a", result);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("a", fe.Value.AsString);
                        Assert.Equal(1, fe.Variation);
                        Assert.Equal(1000, fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.True(fe.TrackEvents);
                        Assert.Equal(2000, fe.DebugEventsUntilDate);
                        Assert.Null(fe.Reason);
                    });
            }
        }

        [Fact]
        public void FeatureEventUsesFlagVersionIfProvided()
        {
            string flagsJson = @"{""flag"":{
                ""value"":""a"",""variation"":1,""version"":1000,
                ""flagVersion"":1500 }}";
            using (LdClient client = MakeClient(user, flagsJson))
            {
                string result = client.StringVariation("flag", "b");
                Assert.Equal("a", result);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("a", fe.Value.AsString);
                        Assert.Equal(1, fe.Variation);
                        Assert.Equal(1500, fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                    });
            }
        }

        [Fact]
        public void VariationSendsFeatureEventForDefaultValue()
        {
            string flagsJson = @"{""flag"":{
                ""value"":null,""variation"":null,""version"":1000 }}";
            using (LdClient client = MakeClient(user, flagsJson))
            {
                string result = client.StringVariation("flag", "b");
                Assert.Equal("b", result);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("b", fe.Value.AsString);
                        Assert.Null(fe.Variation);
                        Assert.Equal(1000, fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.Null(fe.Reason);
                    });
            }
        }

        [Fact]
        public void VariationSendsFeatureEventForUnknownFlag()
        {
            using (LdClient client = MakeClient(user, "{}"))
            {
                string result = client.StringVariation("flag", "b");
                Assert.Equal("b", result);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("b", fe.Value.AsString);
                        Assert.Null(fe.Variation);
                        Assert.Null(fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.Null(fe.Reason);
                    });
            }
        }

        [Fact]
        public void VariationSendsFeatureEventForUnknownFlagWhenClientIsNotInitialized()
        {
            var config = TestUtil.ConfigWithFlagsJson(user, "appkey", "")
                .UpdateProcessorFactory(MockUpdateProcessorThatNeverInitializes.Factory())
                .EventProcessor(eventProcessor);
            config.EventProcessor(eventProcessor);

            using (LdClient client = TestUtil.CreateClient(config.Build(), user))
            {
                string result = client.StringVariation("flag", "b");
                Assert.Equal("b", result);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e =>
                    {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("b", fe.Value.AsString);
                        Assert.Null(fe.Variation);
                        Assert.Null(fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.Null(fe.Reason);
                    });
            }
        }

        [Fact]
        public void VariationSendsFeatureEventWithTrackingAndReasonIfTrackReasonIsTrue()
        {
            string flagsJson = @"{""flag"":{
                ""value"":""a"",""variation"":1,""version"":1000,
                ""trackReason"":true, ""reason"":{""kind"":""OFF""}
                }}";
            using (LdClient client = MakeClient(user, flagsJson))
            {
                string result = client.StringVariation("flag", "b");
                Assert.Equal("a", result);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("a", fe.Value.AsString);
                        Assert.Equal(1, fe.Variation);
                        Assert.Equal(1000, fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.True(fe.TrackEvents);
                        Assert.Null(fe.DebugEventsUntilDate);
                        Assert.Equal(EvaluationReason.OffReason, fe.Reason);
                    });
            }
        }

        [Fact]
        public void VariationDetailSendsFeatureEventWithReasonForValidFlag()
        {
            string flagsJson = @"{""flag"":{
                ""value"":""a"",""variation"":1,""version"":1000,
                ""trackEvents"":true, ""debugEventsUntilDate"":2000,
                ""reason"":{""kind"":""OFF""}
                }}";
            using (LdClient client = MakeClient(user, flagsJson))
            {
                EvaluationDetail<string> result = client.StringVariationDetail("flag", "b");
                Assert.Equal("a", result.Value);
                Assert.Equal(EvaluationReason.OffReason, result.Reason);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("a", fe.Value.AsString);
                        Assert.Equal(1, fe.Variation);
                        Assert.Equal(1000, fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.True(fe.TrackEvents);
                        Assert.Equal(2000, fe.DebugEventsUntilDate);
                        Assert.Equal(EvaluationReason.OffReason, fe.Reason);
                    });
            }
        }

        [Fact]
        public void VariationDetailSendsFeatureEventWithReasonForUnknownFlag()
        {
            using (LdClient client = MakeClient(user, "{}"))
            {
                EvaluationDetail<string> result = client.StringVariationDetail("flag", "b");
                var expectedReason = EvaluationReason.ErrorReason(EvaluationErrorKind.FLAG_NOT_FOUND);
                Assert.Equal("b", result.Value);
                Assert.Equal(expectedReason, result.Reason);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("b", fe.Value.AsString);
                        Assert.Null(fe.Variation);
                        Assert.Null(fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.False(fe.TrackEvents);
                        Assert.Null(fe.DebugEventsUntilDate);
                        Assert.Equal(expectedReason, fe.Reason);
                    });
            }
        }

        [Fact]
        public void VariationSendsFeatureEventWithReasonForUnknownFlagWhenClientIsNotInitialized()
        {
            var config = TestUtil.ConfigWithFlagsJson(user, "appkey", "")
                .UpdateProcessorFactory(MockUpdateProcessorThatNeverInitializes.Factory())
                .EventProcessor(eventProcessor);
            config.EventProcessor(eventProcessor);

            using (LdClient client = TestUtil.CreateClient(config.Build(), user))
            {
                EvaluationDetail<string> result = client.StringVariationDetail("flag", "b");
                var expectedReason = EvaluationReason.ErrorReason(EvaluationErrorKind.CLIENT_NOT_READY);
                Assert.Equal("b", result.Value);
                Assert.Equal(expectedReason, result.Reason);
                Assert.Collection(eventProcessor.Events,
                    e => CheckIdentifyEvent(e, user),
                    e => {
                        FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
                        Assert.Equal("flag", fe.Key);
                        Assert.Equal("b", fe.Value.AsString);
                        Assert.Null(fe.Variation);
                        Assert.Null(fe.Version);
                        Assert.Equal("b", fe.Default.AsString);
                        Assert.False(fe.TrackEvents);
                        Assert.Null(fe.DebugEventsUntilDate);
                        Assert.Equal(expectedReason, fe.Reason);
                    });
            }
        }

        private void CheckIdentifyEvent(Event e, User u)
        {
            IdentifyEvent ie = Assert.IsType<IdentifyEvent>(e);
            Assert.Equal(u.Key, ie.User.Key);
        }
    }
}
