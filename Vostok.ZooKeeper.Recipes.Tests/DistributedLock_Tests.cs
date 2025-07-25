﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

// ReSharper disable AccessToDisposedClosure

namespace Vostok.ZooKeeper.Recipes.Tests
{
    [TestFixture]
    internal class DistributedLock_Tests : TestsBase
    {
        private readonly string folder = "/lock";

        [SetUp]
        public new void SetUp()
        {
            ZooKeeperClient.Delete(folder);
        }

        [Test]
        public async Task AcquireAsync_should_work_alone()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            var token = await @lock.AcquireAsync();
            token.IsAcquired.Should().BeTrue();
            token.Dispose();

            CheckNoLocks();
        }

#if NET
        [Test]
        public async Task AcquireAsync_should_work_alone_AsyncDispose()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);
            await using (var token = await @lock.AcquireAsync())
            {
                token.IsAcquired.Should().BeTrue();

            }
            CheckNoLocks();
        }
#endif

        [Test]
        public void AcquireAsync_should_use_curator_node_path_format()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);
            using (@lock.AcquireAsync().GetAwaiter().GetResult())
            {
                var nodes = ZooKeeperClient.GetChildren(folder);
                nodes.Status.Should().Be(ZooKeeperStatus.Ok);
                var fullPath = nodes.ChildrenNames.Single();
                Console.WriteLine(fullPath);
                // format should be "_c_9502bfd9-ba9d-43cd-8759-2564d8abc4db-lock-0000000000"
                var nodePathEx = new Regex("_c_([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})-lock-0000000000");
                var match = nodePathEx.Match(fullPath);
                match.Success.Should().BeTrue();
                match.Groups.Count.Should().Be(2);
                Guid.TryParse(match.Groups[1].Value, out _).Should().BeTrue(); //_c_{guid}-lock-{seq num 10 digits}
            }
        }

        [Test]
        public async Task AcquireAsync_should_work_concurrently()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);
            var counter = 0;
            var working = 0;

            async Task MakeJob(int i)
            {
                using (var token = await @lock.AcquireAsync())
                {
                    Interlocked.Increment(ref working).Should().Be(1);

                    Log.Info("Making some work..");
                    await Task.Delay(10.Milliseconds());

                    Interlocked.Add(ref counter, i);

                    Interlocked.Decrement(ref working);
                }
            }

            var tasks = Enumerable.Range(0, 10)
                .Select(i => Task.Run(async () => await MakeJob(i)))
                .ToList();

            await Task.WhenAll(tasks);

            counter.Should().Be(45);
            working.Should().Be(0);

            CheckNoLocks();
        }

        [Test]
        public void AcquireAsync_should_wait_for_a_connection()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            Ensemble.Stop();

            var task = @lock.AcquireAsync();
            task.ShouldNotCompleteIn(1.Seconds());

            Ensemble.Start();

            var token = task.ShouldCompleteIn(DefaultTimeout);
            token.IsAcquired.Should().BeTrue();
            token.Dispose();

            CheckNoLocks();
        }

        [Test]
        public async Task AcquireAsync_should_pass_lock_on_session_expire()
        {
            using (var localClient = CreateZooKeeperClient())
            {
                var lock1 = new DistributedLock(localClient, new DistributedLockSettings(folder), Log);
                var lock2 = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

                var task1 = lock1.AcquireAsync();
                var token1 = task1.ShouldCompleteIn(DefaultTimeout);

                var task2 = lock2.AcquireAsync();
                task2.ShouldNotCompleteIn(1.Seconds());

                token1.IsAcquired.Should().BeTrue();

                await KillSessionAsync(localClient);

                var token2 = task2.ShouldCompleteIn(DefaultTimeout);
                new Action(() => token1.IsAcquired.Should().BeFalse()).ShouldPassIn(DefaultTimeout);
                token2.IsAcquired.Should().BeTrue();

                token1.Dispose();

                token1.IsAcquired.Should().BeFalse();
                token2.IsAcquired.Should().BeTrue();
                token2.Dispose();

                CheckNoLocks();
            }
        }

        [Test]
        public void AcquireAsync_should_throw_on_cancel()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            var cts = new CancellationTokenSource();
            var task1 = @lock.AcquireAsync(cts.Token);
            var token1 = task1.ShouldCompleteIn(DefaultTimeout);

            var task2 = @lock.AcquireAsync(cts.Token);
            task2.ShouldNotCompleteIn(1.Seconds());

            cts.Cancel();

            task2.ShouldCompleteWithErrorIn<OperationCanceledException>(DefaultTimeout);

            token1.Dispose();

            CheckNoLocks();
        }

        [Test]
        public void TryAcquireAsync_should_throw_on_cancel()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            var cts = new CancellationTokenSource();
            var task1 = @lock.AcquireAsync(cts.Token);
            var token1 = task1.ShouldCompleteIn(DefaultTimeout);

            var task2 = @lock.TryAcquireAsync(1.Hours(), cts.Token);
            task2.ShouldNotCompleteIn(1.Seconds());

            cts.Cancel();

            task2.ShouldCompleteWithErrorIn<OperationCanceledException>(DefaultTimeout);

            token1.Dispose();

            CheckNoLocks();
        }

        [Test]
        public void TryAcquireAsync_should_return_null_on_timeout()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            var cts = new CancellationTokenSource();
            var task1 = @lock.AcquireAsync(cts.Token);
            var token1 = task1.ShouldCompleteIn(DefaultTimeout);

            var task2 = @lock.TryAcquireAsync(1.Seconds(), cts.Token);
            task2.ShouldCompleteIn(2.Seconds()).Should().BeNull();

            token1.Dispose();

            CheckNoLocks();
        }

        [Test]
        public void AcquireAsync_should_throw_on_client_dispose_while_acquire()
        {
            var localClient = CreateZooKeeperClient();

            var @lock = new DistributedLock(localClient, new DistributedLockSettings(folder), Log);

            localClient.Dispose();

            @lock.AcquireAsync().ShouldCompleteWithErrorIn<Exception>(DefaultTimeout);
        }

        [Test]
        public void Token_dispose_should_throw_on_client_dispose()
        {
            var localClient = CreateZooKeeperClient();

            var @lock = new DistributedLock(localClient, new DistributedLockSettings(folder), Log);

            var token = @lock.AcquireAsync().ShouldCompleteIn(DefaultTimeout);

            localClient.Dispose();

            var check = () => token.Dispose();
            check.Should().Throw<Exception>();
        }

        private void CheckNoLocks()
        {
            Action check = () => ZooKeeperClient.GetChildren(folder).ChildrenNames.Should().BeEmpty();
            check.ShouldPassIn(DefaultTimeout);
        }
    }
}