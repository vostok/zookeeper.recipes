using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Recipes.Helpers;
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 4014

namespace Vostok.ZooKeeper.Recipes.Tests.Helpers
{
    [TestFixture]
    internal class IZooKeeperClientExtension_Tests : TestsBase
    {
        [Test]
        public async Task CreateWithProtectionAsync_should_create_node()
        {
            var times = 100;
            var prefix = "/protected-1/prefix";

            for (var i = 0; i < times; i++)
            {
                var created = await ZooKeeperClient.CreateProtectedAsync(prefix, new byte[i], Log, CancellationToken.None);
                created.IsSuccessful.Should().BeTrue();

                var newPath = created.NewPath;
                newPath.Should().StartWith(prefix);
                ZooKeeperPath.GetSequentialNodeIndex(newPath).Should().Be(i);
            }

            var children = await ZooKeeperClient.GetChildrenAsync("/protected-1");
            children.ChildrenNames.Count.Should().Be(times);
        }

        [Test]
        public async Task CreateWithProtectionAsync_should_return_no_network_errors()
        {
            var created = await ZooKeeperClient.CreateProtectedAsync("asdf", new byte[0], Log, CancellationToken.None);
            created.Status.Should().Be(ZooKeeperStatus.BadArguments);
        }

        [Test]
        public async Task CreateWithProtectionAsync_should_create_no_more_than_one_node()
        {
            var times = 10;
            var prefix = "/protected-2/prefix";

            for (var i = 0; i < times; i++)
            {
                Task.Run(() => KillSession(ZooKeeperClient));

                var created = await ZooKeeperClient.CreateProtectedAsync(prefix, new byte[i], Log, CancellationToken.None);
                created.IsSuccessful.Should().BeTrue();
                
                var own = 0;
                var children = await ZooKeeperClient.GetChildrenAsync("/protected-2");
                foreach (var child in children.ChildrenNames)
                {
                    var data = await ZooKeeperClient.GetDataAsync(ZooKeeperPath.Combine("/protected-2/", child));
                    if (data.IsSuccessful && data.Data?.Length == i)
                        own++;
                }
                own.Should().BeLessOrEqualTo(1);
            }
        }

        [Test]
        public async Task EnsureNodeDeletedAsync_should_delete_node_eventually()
        {
            var times = 10;
            var prefix = "/protected-3/prefix";

            using (var flappingClient = CreateZooKeeperClient())
            {
                for (var i = 0; i < times; i++)
                {
                    var created = await ZooKeeperClient.CreateProtectedAsync(prefix, new byte[i], Log, CancellationToken.None);

                    (await ZooKeeperClient.ExistsAsync(created.NewPath)).Exists.Should().BeTrue();

                    Task.Run(() => KillSession(flappingClient));
                    await flappingClient.DeleteProtectedAsync(created.NewPath, Log, CancellationToken.None);

                    (await ZooKeeperClient.ExistsAsync(created.NewPath)).Exists.Should().BeFalse();
                }
            }
        }

        [Test]
        public async Task EnsureNodeDeletedAsync_should_not_delete_other_nodes()
        {
            var prefix = "/protected-4/prefix";

            var created1 = await ZooKeeperClient.CreateProtectedAsync(prefix, new byte[1], Log, CancellationToken.None);
            var created2 = await ZooKeeperClient.CreateProtectedAsync(prefix, new byte[2], Log, CancellationToken.None);

            await ZooKeeperClient.DeleteProtectedAsync(created2.NewPath, Log, CancellationToken.None);

            (await ZooKeeperClient.ExistsAsync(created1.NewPath)).Exists.Should().BeTrue();
            (await ZooKeeperClient.ExistsAsync(created2.NewPath)).Exists.Should().BeFalse();
        }
    }
}