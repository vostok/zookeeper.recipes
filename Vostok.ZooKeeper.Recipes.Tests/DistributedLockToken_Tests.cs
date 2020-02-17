using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Recipes.Tests
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
    }
}