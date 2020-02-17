using System;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.Context;
using Vostok.ZooKeeper.Client;
using Vostok.ZooKeeper.LocalEnsemble;
using Vostok.ZooKeeper.Testing;

namespace Vostok.ZooKeeper.Recipes.Tests
{
    internal abstract class TestsBase
    {
        protected static readonly ILog Log = new SynchronousConsoleLog().WithOperationContext();
        protected static TimeSpan DefaultTimeout = 10.Seconds();

        protected ZooKeeperEnsemble Ensemble;
        protected ZooKeeperClient ZooKeeperClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
            ZooKeeperClient = CreateZooKeeperClient();
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

        protected async Task KillSessionAsync(ZooKeeperClient client) =>
            await ZooKeeperClientTestsHelper.KillSession(client.SessionId, client.SessionPassword, client.OnConnectionStateChanged, Ensemble.ConnectionString, DefaultTimeout);

        protected async Task RestartEnsembleAsync()
        {
            Ensemble.Stop();

            await Task.Delay(100.Milliseconds());

            Ensemble.Start();
        }

        protected ZooKeeperClient CreateZooKeeperClient()
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString) { Timeout = DefaultTimeout };
            return new ZooKeeperClient(settings, Log);
        }
    }
}