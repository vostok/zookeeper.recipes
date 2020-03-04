using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes.Tests.Helpers
{
    internal class ZooKeeperNodeHelper_Tests : TestsBase
    {
        private readonly string folder = "/helper";
        private readonly string prefix = "/helper/prefix";

        [SetUp]
        public new void SetUp()
        {
            ZooKeeperClient.Delete(folder);
        }

        [Test]
        public async Task WaitForLeadershipAsync_should_work_correctly()
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node3 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task1 = ZooKeeperNodeHelper.WaitForLeadershipAsync(ZooKeeperClient, node1.NewPath, Log);
            var task2 = ZooKeeperNodeHelper.WaitForLeadershipAsync(ZooKeeperClient, node2.NewPath, Log);
            var task3 = ZooKeeperNodeHelper.WaitForLeadershipAsync(ZooKeeperClient, node3.NewPath, Log);

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

            ZooKeeperNodeHelper.WaitForLeadershipAsync(ZooKeeperClient, node.NewPath, Log).ShouldCompleteIn(DefaultTimeout).Should().BeFalse();
        }

        [Test]
        public async Task WaitForLeadershipAsync_should_retry_until_connected()
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task2 = ZooKeeperNodeHelper.WaitForLeadershipAsync(ZooKeeperClient, node2.NewPath, Log);

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
        public async Task WaitForLeadershipAsync_should_be_cancelable()
        {
            var source = new CancellationTokenSource();

            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task2 = ZooKeeperNodeHelper.WaitForLeadershipAsync(ZooKeeperClient, node2.NewPath, Log, source.Token);

            task2.ShouldNotCompleteIn(1.Seconds());

            source.Cancel();

            task2.ShouldCompleteIn(DefaultTimeout).Should().BeFalse();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task WaitForDisappearanceAsync_should_complete_after_any_node_deletion(bool first)
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task = ZooKeeperNodeHelper.WaitForDisappearanceAsync(ZooKeeperClient, new[] {node1.NewPath, node2.NewPath}, Log);

            task.ShouldNotCompleteIn(1.Seconds());

            await ZooKeeperClient.DeleteAsync(first ? node1.NewPath : node2.NewPath);

            task.ShouldCompleteIn(DefaultTimeout);
        }

        [Test]
        public async Task WaitForDisappearanceAsync_should_complete_after_disconnect()
        {
            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task = ZooKeeperNodeHelper.WaitForDisappearanceAsync(ZooKeeperClient, new[] {node1.NewPath, node2.NewPath}, Log);

            task.ShouldNotCompleteIn(1.Seconds());

            Ensemble.Stop();

            task.ShouldCompleteIn(DefaultTimeout);
        }

        [Test]
        public async Task WaitForDisappearanceAsync_should_be_cancelable()
        {
            var source = new CancellationTokenSource();

            var node1 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);
            var node2 = await ZooKeeperClient.CreateAsync(prefix, CreateMode.PersistentSequential);

            var task = ZooKeeperNodeHelper.WaitForDisappearanceAsync(ZooKeeperClient, new[] {node1.NewPath, node2.NewPath}, Log, source.Token);

            task.ShouldNotCompleteIn(1.Seconds());

            source.Cancel();

            task.ShouldCompleteIn(DefaultTimeout);
        }
    }
}