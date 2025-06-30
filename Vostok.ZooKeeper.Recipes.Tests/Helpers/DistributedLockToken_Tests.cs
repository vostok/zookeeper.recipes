using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes.Tests.Helpers
{
    [TestFixture]
    internal class DistributedLockToken_Tests : TestsBase
    {
        private readonly string path = "/token";

        [Test]
        public async Task Dispose_should_delete_node()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(path, CreateMode.Persistent), Log);
            created.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            var token = new DistributedLockToken(ZooKeeperClient, created.NewPath, Log);
            token.IsAcquired.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            token.Dispose();

            token.IsAcquired.Should().BeFalse();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeFalse();
        }

#if NET
        [Test]
        public async Task DisposeAsync_should_delete_node()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(path, CreateMode.Persistent), Log);
            created.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            var token = new DistributedLockToken(ZooKeeperClient, created.NewPath, Log);
            token.IsAcquired.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            await token.DisposeAsync();

            token.IsAcquired.Should().BeFalse();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeFalse();
        }
#endif
        [Test]
        public async Task IsAcquired_should_become_false_on_node_deletion()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(path, CreateMode.Persistent), Log);
            created.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            var token = new DistributedLockToken(ZooKeeperClient, created.NewPath, Log);
            token.IsAcquired.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            ZooKeeperClient.Delete(created.NewPath).EnsureSuccess();

            Action check = () => token.IsAcquired.Should().BeFalse();
            check.ShouldPassIn(DefaultTimeout);

            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeFalse();

            token.Dispose();
            token.IsAcquired.Should().BeFalse();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeFalse();
        }

        [Test]
        public async Task CancellationToken_should_be_triggered_on_node_deletion()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(path, CreateMode.Persistent), Log);
            created.IsSuccessful.Should().BeTrue();

            var token = new DistributedLockToken(ZooKeeperClient, created.NewPath, Log);
            token.IsAcquired.Should().BeTrue();

            var delay = Task.Delay(-1, token.CancellationToken);
            delay.IsCompleted.Should().BeFalse();

            ZooKeeperClient.Delete(created.NewPath).EnsureSuccess();

            delay.ShouldCompleteWithErrorIn<TaskCanceledException>(DefaultTimeout);
        }

        [Test]
        public async Task CancellationToken_should_be_triggered_on_dispose()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(path, CreateMode.Persistent), Log);
            created.IsSuccessful.Should().BeTrue();

            var token = new DistributedLockToken(ZooKeeperClient, created.NewPath, Log);
            token.IsAcquired.Should().BeTrue();

            var delay = Task.Delay(-1, token.CancellationToken);
            delay.IsCompleted.Should().BeFalse();

            token.Dispose();

            delay.ShouldCompleteWithErrorIn<TaskCanceledException>(DefaultTimeout);
        }
    }
}