using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

// ReSharper disable AccessToDisposedClosure
#pragma warning disable 4014

namespace Vostok.ZooKeeper.Recipes.Tests
{
    [TestFixture]
    internal class IZooKeeperClientExtension_Tests : TestsBase
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
        public async Task WaitForLeadershipAsync_should_works_correctly()
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node3 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task1 = ZooKeeperClient.WaitForLeadershipAsync(node1.NewPath, Log);
            var task2 = ZooKeeperClient.WaitForLeadershipAsync(node2.NewPath, Log);
            var task3 = ZooKeeperClient.WaitForLeadershipAsync(node3.NewPath, Log);

            task1.ShouldCompleteIn(DefaultTimeout).Should().BeTrue();
            task2.ShouldNotCompleteIn(1.Seconds());
            task3.ShouldNotCompleteIn(1.Seconds());

            await ZooKeeperClient.DeleteAsync(node1.NewPath);
            task2.ShouldCompleteIn(DefaultTimeout).Should().BeTrue();
            task3.ShouldNotCompleteIn(1.Seconds());

            await ZooKeeperClient.DeleteAsync(node2.NewPath);
            task3.ShouldCompleteIn(DefaultTimeout).Should().BeTrue();
        }

        [Test]
        public async Task WaitForLeadershipAsync_should_return_false_on_non_existing_node()
        {
            var node = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            await ZooKeeperClient.DeleteAsync(node.NewPath);

            ZooKeeperClient.WaitForLeadershipAsync(node.NewPath, Log).ShouldCompleteIn(DefaultTimeout).Should().BeFalse();
        }

        [Test]
        public async Task WaitForLeadershipAsync_should_retry_until_connected()
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            
            var task2 = ZooKeeperClient.WaitForLeadershipAsync(node2.NewPath, Log);

            task2.ShouldNotCompleteIn(1.Seconds());

            Ensemble.Stop();
            await Task.Delay(1.Seconds());
            task2.ShouldNotCompleteIn(1.Seconds());
            Ensemble.Start();
            await Task.Delay(1.Seconds());
            task2.ShouldNotCompleteIn(1.Seconds());

            await ZooKeeperClient.DeleteAsync(node1.NewPath);
            task2.ShouldCompleteIn(DefaultTimeout).Should().BeTrue();
        }

        [Test]
        public async Task WaitForLeadershipAsync_should_be_cancellable()
        {
            var source = new CancellationTokenSource();

            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task2 = ZooKeeperClient.WaitForLeadershipAsync(node2.NewPath, Log, source.Token);

            task2.ShouldNotCompleteIn(1.Seconds());

            source.Cancel();

            task2.ShouldCompleteIn(DefaultTimeout).Should().BeFalse();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task WaitForDisappearanceAsync_should_complete_when_any_node_deleted(bool first)
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task = ZooKeeperClient.WaitForDisappearanceAsync(new[] {node1.NewPath, node2.NewPath}, Log);

            task.ShouldNotCompleteIn(1.Seconds());

            await ZooKeeperClient.DeleteAsync(first ? node1.NewPath : node2.NewPath);

            task.ShouldCompleteIn(DefaultTimeout);
        }

        [Test]
        public async Task WaitForDisappearanceAsync_should_complete_when_disconnected()
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task = ZooKeeperClient.WaitForDisappearanceAsync(new[] { node1.NewPath, node2.NewPath }, Log);

            task.ShouldNotCompleteIn(1.Seconds());

            Ensemble.Stop();

            task.ShouldCompleteIn(DefaultTimeout);
        }

        [Test]
        public async Task WaitForDisappearanceAsync_should_be_cancellable()
        {
            var source = new CancellationTokenSource();

            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task = ZooKeeperClient.WaitForDisappearanceAsync(new[] { node1.NewPath, node2.NewPath }, Log, source.Token);

            task.ShouldNotCompleteIn(1.Seconds());

            source.Cancel();

            task.ShouldCompleteIn(DefaultTimeout);
        }
    }
}