using System;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes.Tests.Helpers
{
    [TestFixture]
    internal class ZooKeeperPathHelper_Tests
    {
        [Test]
        public void Should_work_correctly()
        {
            ZooKeeperPathHelper.BuildProtectedNodePath(
                    new CreateRequest("/vostok/test/lock-test/lock", CreateMode.EphemeralSequential),
                    new Guid("e2646dc9-afcc-44e1-b7af-a20b46006fe1"))
                .Should()
                .Be("/vostok/test/lock-test/_c_e2646dc9-afcc-44e1-b7af-a20b46006fe1-lock");
        }
    }
}