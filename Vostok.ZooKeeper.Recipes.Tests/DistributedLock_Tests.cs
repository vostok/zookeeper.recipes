using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;

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
        public async Task Should_works_alone()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            var token = await @lock.AcquireAsync();
            Thread.Sleep(100.Milliseconds());
            token.IsAcquired.Should().BeTrue();
            token.Dispose();

            Action check = () => ZooKeeperClient.GetChildren(folder).ChildrenNames.Should().BeEmpty();
            check.ShouldPassIn(DefaultTimeout);
        }

        [Test]
        public void Should_works()
        {
            var @lock = new DistributedLock(ZooKeeperClient, new DistributedLockSettings(folder), Log);

            var task1 = @lock.AcquireAsync();
            var task2 = @lock.AcquireAsync();
            var task3 = @lock.AcquireAsync();

            var token1 = task1.ShouldCompleteIn(DefaultTimeout);
            task2.ShouldNotCompleteIn(1.Seconds());
            task3.ShouldNotCompleteIn(1.Seconds());
            token1.IsAcquired.Should().BeTrue();
            token1.Dispose();

            var token2 = task2.ShouldCompleteIn(DefaultTimeout);
            task3.ShouldNotCompleteIn(1.Seconds());
            token1.IsAcquired.Should().BeFalse();
            token2.IsAcquired.Should().BeTrue();
            token2.Dispose();

            var token3 = task3.ShouldCompleteIn(DefaultTimeout);
            token1.IsAcquired.Should().BeFalse();
            token2.IsAcquired.Should().BeFalse();
            token3.IsAcquired.Should().BeTrue();
            token3.Dispose();

            Action check = () => ZooKeeperClient.GetChildren(folder).ChildrenNames.Should().BeEmpty();
            check.ShouldPassIn(DefaultTimeout);
        }
    }
}