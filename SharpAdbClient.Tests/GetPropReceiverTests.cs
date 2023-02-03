﻿using AndroCtrl.Protocols.AndroidDebugBridge.DeviceCommands;

using Xunit;

namespace AndroCtrl.Protocols.AndroidDebugBridge.Tests;

public class GetPropReceiverTests
{
    [Fact]
    public void ListPropertiesTest()
    {
        DeviceData device = new()
        {
            State = DeviceState.Online
        };

        DummyAdbClient client = new();
        client.Commands.Add("/system/bin/getprop", @"[init.svc.BGW]: [running]
[init.svc.MtkCodecService]: [running]
[init.svc.bootanim]: [stopped]");

        System.Collections.Generic.Dictionary<string, string> properties = client.GetProperties(device);
        Assert.NotNull(properties);
        Assert.Equal(3, properties.Count);
        Assert.True(properties.ContainsKey("init.svc.BGW"));
        Assert.True(properties.ContainsKey("init.svc.MtkCodecService"));
        Assert.True(properties.ContainsKey("init.svc.bootanim"));

        Assert.Equal("running", properties["init.svc.BGW"]);
        Assert.Equal("running", properties["init.svc.MtkCodecService"]);
        Assert.Equal("stopped", properties["init.svc.bootanim"]);
    }
}
