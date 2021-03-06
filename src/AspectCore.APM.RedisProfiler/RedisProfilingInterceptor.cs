﻿using System.Linq;
using System.Threading.Tasks;
using AspectCore.APM.Profiler;
using AspectCore.DynamicProxy;
using AspectCore.Injector;
using StackExchange.Redis;

#pragma warning disable 4014

namespace AspectCore.APM.RedisProfiler
{
    public sealed class RedisProfilingInterceptor : AbstractInterceptor
    {
        public async override Task Invoke(AspectContext context, AspectDelegate next)
        {
            var profilers = context.ServiceProvider.ResolveMany<IProfiler<RedisProfilingContext>>();
            if (profilers.Any())
            {
                var connectionMultiplexer = context.ServiceProvider.ResolveRequired<IConnectionMultiplexer>();
                var profilerContext = new object();
                AspectRedisDatabaseProfilerContext.Context = profilerContext;
                connectionMultiplexer.BeginProfiling(profilerContext);
                await context.Invoke(next);
                var profiledResult = connectionMultiplexer.FinishProfiling(profilerContext);
                var redisProfiledCommands = profiledResult.Select(x =>
                    RedisProfilingCommand.Create(
                        x.Command, x.EndPoint, x.Db, x.CommandCreated, x.CreationToEnqueued,
                        x.EnqueuedToSending, x.SentToResponse, x.ResponseToCompletion, x.ElapsedTime, 
                        connectionMultiplexer.ClientName, connectionMultiplexer.OperationCount)).ToArray();
                foreach (var profiler in profilers)
                {
                    profiler.Invoke(new RedisProfilingContext(redisProfiledCommands));
                }
                AspectRedisDatabaseProfilerContext.Context = null;
            }
            else
            {
                await context.Invoke(next);
            }
        }
    }
}