using System;
using System.Buffers;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.CharacterPortraits;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Tests.Engines.CharacterPortraits;

[Collection("Sequential UOContent Tests")]
public class CharacterPortraitTests : IDisposable
{
    private readonly PasswordProtectionAlgorithm _originalAlgorithm = AccountSecurity.CurrentAlgorithm;

    public CharacterPortraitTests()
    {
        AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.SHA2;
    }

    public void Dispose() => AccountSecurity.CurrentAlgorithm = _originalAlgorithm;

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void CatalogBounds(int id, bool valid)
    {
        Assert.Equal(valid, CharacterPortraitSystem.IsValidPortrait(id));
    }

    [Fact]
    public void CharacterSelectionCanQueryPortraitsWithoutEnablingInGameCommands()
    {
        IncomingExtendedCommandPackets.Configure();
        CharacterPortraitSystem.Configure();
        Assert.False(IncomingPackets.GetHandler(0xbf).InGameOnly);
        Assert.False(IncomingExtendedCommandPackets.GetExtendedHandler(0x80).InGameOnly);
        Assert.True(IncomingExtendedCommandPackets.GetExtendedHandler(0x09).InGameOnly);

        var account = new Account($"portrait-query-{Guid.NewGuid():N}", "portrait-test-only");
        using var state = PacketTestUtilities.CreateTestNetState();
        try
        {
            CharacterPortraitSystem.Receive(state, new SpanReader(new byte[] { 0 }));
            Assert.Empty(state.SendBuffer.GetReadSpan().ToArray());
            state.Account = account;
            CharacterPortraitSystem.Receive(state, new SpanReader(new byte[] { 0 }));
            Assert.Equal(new byte[] { 0xbf, 0, 7, 0, 0x80, 1, 0 }, state.SendBuffer.GetReadSpan().ToArray());
        }
        finally
        {
            account.Delete();
        }
    }

    [Fact]
    public void PersistsPerCharacterAndRejectsForeignAccount()
    {
        var account = new Account($"portraits-{Guid.NewGuid():N}", "portrait-test-only");
        var other = new Account($"portraits-{Guid.NewGuid():N}", "portrait-test-only");
        var first = new PlayerMobile();
        var second = new PlayerMobile();
        account[0] = first;
        account[1] = second;
        try
        {
            Assert.Equal(0, CharacterPortraitSystem.GetPortrait(account, first));
            Assert.True(CharacterPortraitSystem.SetPortrait(account, first, 50));
            Assert.True(CharacterPortraitSystem.SetPortrait(account, second, 100));
            Assert.False(CharacterPortraitSystem.SetPortrait(other, first, 1));
            Assert.False(CharacterPortraitSystem.SetPortrait(account, first, 101));
            Assert.Equal(50, CharacterPortraitSystem.GetPortrait(account, first));
            Assert.Equal(100, CharacterPortraitSystem.GetPortrait(account, second));
            Assert.Equal("50", account.GetTag($"Abadoria.Portrait.{first.Serial.Value}"));
            CharacterPortraitSystem.OnPlayerDeleted(first);
            Assert.Equal(0, CharacterPortraitSystem.GetPortrait(account, first));
            Assert.Equal(100, CharacterPortraitSystem.GetPortrait(account, second));
        }
        finally
        {
            first.Delete();
            second.Delete();
            account.Delete();
            other.Delete();
        }
    }

    [Fact]
    public void NetworkSaveRequiresTheLoggedInCharacter()
    {
        var account = new Account($"portraits-{Guid.NewGuid():N}", "portrait-test-only");
        var player = new PlayerMobile();
        var other = new PlayerMobile();
        account[0] = player;
        account[1] = other;
        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = account;
        state.Mobile = player;
        try
        {
            var writer = new SpanWriter(stackalloc byte[6]);
            writer.Write((byte)1);
            writer.Write(other.Serial.Value);
            writer.Write((byte)25);
            CharacterPortraitSystem.Receive(state, new SpanReader(writer.Span));
            Assert.Equal(0, CharacterPortraitSystem.GetPortrait(account, other));
            Assert.Empty(state.SendBuffer.GetReadSpan().ToArray());

            writer = new SpanWriter(stackalloc byte[6]);
            writer.Write((byte)1);
            writer.Write(player.Serial.Value);
            writer.Write((byte)25);
            CharacterPortraitSystem.Receive(state, new SpanReader(writer.Span));
            Assert.Equal(25, CharacterPortraitSystem.GetPortrait(account, player));
            var response = state.SendBuffer.GetReadSpan();
            Assert.Equal(19, response.Length);
            Assert.Equal(0xbf, response[0]);
            Assert.Equal(0x80, response[4]);
            Assert.Equal(25, response[12]);
        }
        finally
        {
            state.Mobile = null;
            player.Delete();
            other.Delete();
            account.Delete();
        }
    }
}
