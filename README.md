# Vostok.ZooKeeper.Recipes

[![Build status](https://ci.appveyor.com/api/projects/status/github/vostok/zookeeper.recipes?svg=true&branch=master)](https://ci.appveyor.com/project/vostok/zookeeper.recipes/branch/master)
[![NuGet](https://img.shields.io/nuget/v/Vostok.ZooKeeper.Recipes.svg)](https://www.nuget.org/packages/Vostok.ZooKeeper.Recipes)

ZooKeeper recipes, such as distributed lock, based on Vostok.ZooKeeper.Client.


**Build guide**: https://github.com/vostok/devtools/blob/master/library-dev-conventions/how-to-build-a-library.md

## Quick start

	var @lock = new DistributedLock(zooKeeperClient, new DistributedLockSettings(lockPath), log);

    using (var token = await @lock.AcquireAsync())
    {
        ...
    }