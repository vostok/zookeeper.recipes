using System;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.LocalEnsemble;
using Vostok.ZooKeeper.Recipes.Helpers;
using Vostok.ZooKeeper.Testing;

namespace Vostok.ZooKeeper.Recipes.Tests
{
    internal abstract class TestsBase
    {
        protected static readonly ILog Log = new SynchronousConsoleLog();
        protected static TimeSpan DefaultTimeout = 10.Seconds();

        protected ZooKeeperEnsemble Ensemble;
        protected ZooKeeperClient ZooKeeperClient;
        protected ExtendedZooKeeperClient ExtendedZooKeeperClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
            ZooKeeperClient = CreateZooKeeperClient();
            ExtendedZooKeeperClient = new ExtendedZooKeeperClient(ZooKeeperClient, Log);
        }

        [SetUp]
        public void SetUp()
        {
            if (!Ensemble.IsRunning)
                Ensemble.Start();

        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ZooKeeperClient.Dispose();
            Ensemble.Dispose();
        }

        protected Task KillSession(ZooKeeperClient client) =>
            ZooKeeperClientTestsHelper.KillSession(client.SessionId, client.SessionPassword, client.OnConnectionStateChanged, Ensemble.ConnectionString, DefaultTimeout);

        protected ZooKeeperClient CreateZooKeeperClient()
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString) { Timeout = DefaultTimeout };
            return new ZooKeeperClient(settings, Log);
        }
    }
}