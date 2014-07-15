﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Microsoft.AspNet.SignalR.Redis
{
    public class RedisConnection : IRedisConnection
    {
        private StackExchange.Redis.ISubscriber _redisSubscriber;
        private ConnectionMultiplexer _connection;
        private TraceSource _trace;

        public async Task ConnectAsync(string connectionString, TraceSource trace)
        {
            _connection = await ConnectionMultiplexer.ConnectAsync(connectionString);

            _connection.ConnectionFailed += OnConnectionFailed;
            _connection.ConnectionRestored += OnConnectionRestored;
            _connection.ErrorMessage += OnError;

            _trace = trace;

            _redisSubscriber = _connection.GetSubscriber();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public void Close(string key, bool allowCommandsToComplete = true)
        {
            if (_redisSubscriber != null)
            {
                _redisSubscriber.Unsubscribe(key);
            }

            if (_connection != null)
            {
                _connection.Close(allowCommandsToComplete);
            }

            _connection.Dispose();
        }

        public async Task SubscribeAsync(string key, Action<int, RedisMessage> onMessage)
        {
            await _redisSubscriber.SubscribeAsync(key, (channel, data) =>
            {
                var message = RedisMessage.FromBytes(data, _trace);
                onMessage(0, message);
            });
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
            }
        }

        public async Task ScriptEvaluateAsync(int database, string script, string key, byte[] messageArguments)
        {
            if (_connection == null)
            {
                throw new InvalidOperationException(Resources.Error_RedisConnectionNotStarted);
            }

            var keys = new RedisKey[] { key };

            var arguments = new RedisValue[] { messageArguments };

            await _connection.GetDatabase(database).ScriptEvaluateAsync(script,
                keys,
                arguments);
        }

        public event Action<Exception> ConnectionFailed;

        public event Action<Exception> ConnectionRestored;

        public event Action<Exception> ErrorMessage;

        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs args)
        {
            var handler = ConnectionFailed;
            handler(args.Exception);
        }

        private void OnConnectionRestored(object sender, ConnectionFailedEventArgs args)
        {
            var handler = ConnectionRestored;
            handler(args.Exception);
        }

        private void OnError(object sender, RedisErrorEventArgs args)
        {
            var handler = ErrorMessage;
            handler(new InvalidOperationException(args.Message));
        }
    }
}