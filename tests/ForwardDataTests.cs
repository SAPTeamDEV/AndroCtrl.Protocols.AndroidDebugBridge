﻿using Xunit;

namespace SAPTeam.AndroCtrl.Adb.Tests
{
    public class ForwardDataTests
    {
        [Fact]
        public void SpecTests()
        {
            ForwardData data = new()
            {
                Local = "tcp:1234",
                Remote = "tcp:4321"
            };

            Assert.Equal("tcp:1234", data.LocalSpec.ToString());
            Assert.Equal("tcp:4321", data.RemoteSpec.ToString());
        }
    }
}