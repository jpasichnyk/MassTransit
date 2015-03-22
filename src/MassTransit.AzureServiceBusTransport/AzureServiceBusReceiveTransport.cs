﻿// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.AzureServiceBusTransport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Configuration;
    using Contexts;
    using Internals.Extensions;
    using Logging;
    using MassTransit.Pipeline;
    using Pipeline;
    using Policies;
    using Transports;


    public class AzureServiceBusReceiveTransport :
        IReceiveTransport
    {
        readonly IServiceBusHost _host;
        readonly Uri _inputAddress;
        readonly ILog _log = Logger.Get<AzureServiceBusReceiveTransport>();
        readonly IRetryPolicy _retryPolicy;
        readonly ReceiveSettings _settings;
        readonly TopicSubscriptionSettings[] _subscriptionSettings;

        public AzureServiceBusReceiveTransport(IServiceBusHost host, ReceiveSettings settings, IRetryPolicy retryPolicy,
            params TopicSubscriptionSettings[] subscriptionSettings)
        {
            _host = host;
            _settings = settings;
            _retryPolicy = retryPolicy;
            _subscriptionSettings = subscriptionSettings;

            _inputAddress = host.Settings.GetInputAddress(settings.QueueDescription);
        }

        public Uri InputAddress
        {
            get { return _inputAddress; }
        }

        public ReceiveTransportHandle Start(IPipe<ReceiveContext> receivePipe)
        {
            if (_log.IsDebugEnabled)
                _log.DebugFormat("Starting receive transport: {0}", new Uri(_host.Settings.ServiceUri, _settings.QueueDescription.Path));

            var stopTokenSource = new CancellationTokenSource();

            IPipe<ConnectionContext> connectionPipe = Pipe.New<ConnectionContext>(x =>
            {
                x.Repeat(stopTokenSource.Token);
                x.Retry(_retryPolicy, stopTokenSource.Token);

                x.Filter(new PrepareReceiveQueueFilter(_settings));

                foreach (TopicSubscriptionSettings subscriptionSetting in _subscriptionSettings)
                    x.Filter(new BindTopicSubscriptionFilter(subscriptionSetting));

                x.Filter(new MessageReceiverFilter(receivePipe));
            });

            var receiveTask = Receiver(stopTokenSource.Token, connectionPipe);

            return new Handle(stopTokenSource, receiveTask);
        }

        async Task Receiver(CancellationToken stopTokenSource, IPipe<ConnectionContext> connectionPipe)
        {
            await Repeat.UntilCancelled(stopTokenSource, async () =>
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("Connecting receive transport: {0}", _host.Settings.GetInputAddress(_settings.QueueDescription));

                try
                {
                    var context = new ServiceBusConnectionContext(_host, stopTokenSource);

                    await connectionPipe.Send(context);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (_log.IsErrorEnabled)
                        _log.ErrorFormat("Azure Service Bus connection failed: {0}", ex.Message);
                }
            });
        }


        class Handle :
            ReceiveTransportHandle
        {
            readonly CancellationTokenSource _stop;
            readonly Task _receiveTask;

            public Handle(CancellationTokenSource cancellationTokenSource, Task receiveTask)
            {
                _stop = cancellationTokenSource;
                _receiveTask = receiveTask;
            }

            void IDisposable.Dispose()
            {
                _stop.Cancel();
            }

            async Task ReceiveTransportHandle.Stop(CancellationToken cancellationToken)
            {
                _stop.Cancel();

                await _receiveTask.WithCancellation(cancellationToken);
            }
        }
    }
}