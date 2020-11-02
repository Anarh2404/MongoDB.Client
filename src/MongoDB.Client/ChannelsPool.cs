﻿using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MongoDB.Client
{
    internal class ChannelsPool
    {
        private readonly EndPoint _endPoint;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ChannelsPool> _logger;
        private ImmutableList<Channel> _channels = ImmutableList<Channel>.Empty;
        private readonly SemaphoreSlim _channelAllocateLock = new SemaphoreSlim(1);

        public ChannelsPool(EndPoint endPoint, ILoggerFactory loggerFactory)
        {
            _endPoint = endPoint;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ChannelsPool>();
        }

        public ValueTask<Channel> GetChannelAsync(CancellationToken cancellationToken)
        {
            for (int i = 0; i < _channels.Count; i++)
            {
                var channel = _channels[i];
                if (channel.IsBusy == false)
                {
                    return new ValueTask<Channel>(channel);
                }
            }

            return AllocateNewChannel(cancellationToken);
        }

        private async ValueTask<Channel> AllocateNewChannel(CancellationToken cancellationToken)
        {
            await _channelAllocateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Channel channel;
                for (int i = 0; i < _channels.Count; i++)
                {
                    channel = _channels[i];
                    if (channel.IsBusy == false)
                    {
                        return channel;
                    }
                }
                
                _logger.LogInformation("Allocating new channel");
                channel = new Channel(_endPoint, _loggerFactory);
                _ = await channel.InitConnectAsync(cancellationToken).ConfigureAwait(false);
                _channels = _channels.Add(channel);
                return channel;
            }
            finally
            {
                _channelAllocateLock.Release();
            }
        }
    }
}