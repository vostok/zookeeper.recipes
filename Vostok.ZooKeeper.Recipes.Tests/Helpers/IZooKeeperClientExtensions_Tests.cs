using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Recipes.Helpers;

// ReSharper disable AccessToDisposedClosure
#pragma warning disable 4014

namespace Vostok.ZooKeeper.Recipes.Tests.Helpers
{
    [TestFixture]
    internal class IZooKeeperClientExtensions_Tests : TestsBase
    {
        private readonly string folder = "/protected";
        private readonly string prefix = "/protected/prefix";

        [SetUp]
        public new void SetUp()
        {
            ZooKeeperClient.Delete(folder);
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.PersistentSequential)]
        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.EphemeralSequential)]
        public async Task CreateProtectedAsync_should_create_node(CreateMode createMode)
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, createMode), Log);
            created.IsSuccessful.Should().BeTrue();

            created.NewPath.Should().StartWith(prefix);
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();
        }

        [Test]
        public async Task CreateProtectedAsync_should_return_no_network_errors()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest("asdf", CreateMode.Persistent), Log);
            created.Status.Should().Be(ZooKeeperStatus.BadArguments);
        }

        [Test]
        public async Task CreateProtectedAsync_should_retry_until_connected()
        {
            Ensemble.Stop();

            var task = ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, CreateMode.Persistent), Log);

            await Task.Delay(1.Seconds());

            task.ShouldNotCompleteIn(1.Seconds());

            Ensemble.Start();

            task.ShouldCompleteIn(DefaultTimeout).IsSuccessful.Should().BeTrue();

            var children = await ZooKeeperClient.GetChildrenAsync(folder);
            children.ChildrenNames.Count.Should().Be(1);
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.PersistentSequential)]
        public async Task CreateProtectedAsync_should_create_exactly_one_node(CreateMode createMode)
        {
            var times = 20;

            for (var i = 0; i < times; i++)
            {
                var task = Task.CompletedTask;
                if (times % 4 == 0)
                    task = Task.Run(async () => await KillSessionAsync(ZooKeeperClient).SilentlyContinue());

                if (times % 4 == 1)
                    task = Task.Run(async () => await RestartEnsembleAsync().SilentlyContinue());

                var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, createMode), Log);
                created.IsSuccessful.Should().BeTrue();

                await task;
            }

            var children = await ZooKeeperClient.GetChildrenAsync(folder);
            children.ChildrenNames.Count.Should().Be(times);
        }

        [Test]
        public void CreateProtectedAsync_should_return_error_if_client_disposed()
        {
            var client = CreateZooKeeperClient();
            client.Dispose();

            client.CreateProtectedAsync(new CreateRequest(prefix, CreateMode.Persistent), Log)
                .ShouldCompleteIn(DefaultTimeout)
                .Status
                .Should()
                .Be(ZooKeeperStatus.Died);
        }

        [TestCase(CreateMode.Persistent)]
        [TestCase(CreateMode.PersistentSequential)]
        [TestCase(CreateMode.Ephemeral)]
        [TestCase(CreateMode.EphemeralSequential)]
        public async Task DeleteProtectedAsync_should_delete_node(CreateMode createMode)
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, createMode), Log);
            created.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            var deleted = await ZooKeeperClient.DeleteProtectedAsync(new DeleteRequest(created.NewPath), Log);
            deleted.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeFalse();
        }

        [Test]
        public async Task DeleteProtectedAsync_should_delete_all_sequential_nodes()
        {
            var id = Guid.NewGuid();
            
            var created1 = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, CreateMode.PersistentSequential), Log, id);
            created1.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created1.NewPath).Exists.Should().BeTrue();

            var created2 = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, CreateMode.PersistentSequential), Log, id);
            created2.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created2.NewPath).Exists.Should().BeTrue();
            created2.NewPath.Should().NotBe(created1.NewPath);

            var deleted = await ZooKeeperClient.DeleteProtectedAsync(new DeleteRequest($"{prefix}-{id:N}"), Log);
            deleted.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created1.NewPath).Exists.Should().BeFalse();
            ZooKeeperClient.Exists(created2.NewPath).Exists.Should().BeFalse();
        }

        [Test]
        public async Task DeleteProtectedAsync_should_return_no_network_errors()
        {
            var created = await ZooKeeperClient.DeleteProtectedAsync(new DeleteRequest("asdf"), Log);
            created.Status.Should().Be(ZooKeeperStatus.BadArguments);
        }

        [Test]
        public async Task DeleteProtectedAsync_should_retry_until_connected()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync(new CreateRequest(prefix, CreateMode.Persistent), Log);
            created.IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeTrue();

            Ensemble.Stop();

            var task = ZooKeeperClient.DeleteProtectedAsync(new DeleteRequest(created.NewPath), Log);

            await Task.Delay(1.Seconds());

            task.ShouldNotCompleteIn(1.Seconds());

            Ensemble.Start();

            task.ShouldCompleteIn(DefaultTimeout).IsSuccessful.Should().BeTrue();
            ZooKeeperClient.Exists(created.NewPath).Exists.Should().BeFalse();
        }

        [Test]
        public void DeleteProtectedAsync_should_return_error_if_client_disposed()
        {
            var client = CreateZooKeeperClient();
            client.Dispose();

            client.DeleteProtectedAsync(new DeleteRequest(prefix), Log)
                .ShouldCompleteIn(DefaultTimeout)
                .Status
                .Should()
                .Be(ZooKeeperStatus.Died);
        }
    }
}