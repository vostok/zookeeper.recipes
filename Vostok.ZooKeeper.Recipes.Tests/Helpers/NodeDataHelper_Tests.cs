using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Environment;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes.Tests.Helpers
{
    [TestFixture]
    internal class NodeDataHelper_Tests
    {
        [Test]
        public void Should_serialize_data()
        {
            var data = NodeDataHelper.GetNodeData();
            var desserialized = NodeDataHelper.Deserialize(data);
            desserialized["hostName"].Should().Be(EnvironmentInfo.Host);
        }
    }
}