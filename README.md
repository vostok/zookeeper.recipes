# Vostok.ZooKeeper.Recipes

[![Build & Test & Publish](https://github.com/vostok/zookeeper.recipes/actions/workflows/ci.yml/badge.svg)](https://github.com/vostok/zookeeper.recipes/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Vostok.ZooKeeper.Recipes.svg)](https://www.nuget.org/packages/Vostok.ZooKeeper.Recipes)

ZooKeeper recipes, such as distributed lock, based on Vostok.ZooKeeper.Client.


**Build guide**: https://github.com/vostok/devtools/blob/master/library-dev-conventions/how-to-build-a-library.md

## Quick start

	var @lock = new DistributedLock(zooKeeperClient, new DistributedLockSettings(lockPath), log);

    using (var token = await @lock.AcquireAsync())
    {
        ...
    }
