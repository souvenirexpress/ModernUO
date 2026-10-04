using System;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class CharacterPortraitPacketTests
{
    [Fact]
    public void TestCharacterPortraitResponse()
    {
        var originalAlgorithm = AccountSecurity.CurrentAlgorithm;
        AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.SHA1;
        var account = new Account($"portrait-{Guid.NewGuid():N}", "test-password");
        var mobile = new Mobile(World.NewMobile);
        mobile.DefaultMobileInit();
        mobile.Body = 0x0190;
        mobile.Hue = 0x03EA;
        var shirt = new Item(World.NewItem)
        {
            ItemID = 0x1517,
            Hue = 0x0455,
            Layer = Layer.Shirt
        };
        mobile.AddItem(shirt);
        account[2] = mobile;

        try
        {
            using var ns = PacketTestUtilities.CreateTestNetState();
            ns.Account = account;

            IncomingExtendedCommandPackets.CharacterPortraitRequest(ns, default);

            var result = ns.SendBuffer.GetReadSpan();
            AssertThat.Equal(
                result,
                stackalloc byte[]
                {
                    0xBF, 0x00, 0x12, 0x00, 0x7F, 0x01, 0x01,
                    0x02, 0x01, 0x90, 0x03, 0xEA, 0x01,
                    0x15, 0x17, 0x04, 0x55, (byte)Layer.Shirt
                }
            );
        }
        finally
        {
            account.Delete();
            AccountSecurity.CurrentAlgorithm = originalAlgorithm;
        }
    }
}
