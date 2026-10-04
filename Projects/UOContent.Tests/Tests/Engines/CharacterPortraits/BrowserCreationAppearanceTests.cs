using System.Buffers;
using Server;
using Server.Engines.CharacterCreation;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Tests.Engines.CharacterPortraits;

[Collection("Sequential UOContent Tests")]
public class BrowserCreationAppearanceTests
{
    [Fact]
    public void ReadsVersionedHuesAndPreservesFollowingPacketFields()
    {
        var reader = new SpanReader(new byte[] { 65, 66, 1, 0, 0, 0, 88, 2, 30, 3, 233, 0, 0, 0, 0, 3 });
        Assert.Equal(new[] { 0, 0x58, 0x21e, 0x3e9 }, BrowserCreationAppearance.ReadHues(ref reader));
        Assert.Equal(3, reader.ReadByte());
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(65, 66, 2)]
    [InlineData(65, 66, 1)]
    public void IgnoresLegacyUnknownOrInvalidAppearance(int first, int second, int version)
    {
        var reader = new SpanReader(new byte[] { (byte)first, (byte)second, (byte)version, 255, 255, 0, 88, 2, 30, 3, 233, 0, 0, 0, 0 });
        Assert.Null(BrowserCreationAppearance.ReadHues(ref reader));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void EquippedClothingKeepsEachSelectedHueIncludingUndyed()
    {
        var mobile = new Mobile(World.NewMobile);
        mobile.DefaultMobileInit();
        try
        {
            CharacterCreation.ApplyBrowserClothing(mobile, new[] { 0x1713, 0x1517, 0x1539, 0x170b }, 0x44, new[] { 0, 0x58, 0x21e, 0x3e9 });
            Assert.Equal(0, mobile.FindItemOnLayer(Layer.Helm).Hue);
            Assert.Equal(0x58, mobile.FindItemOnLayer(Layer.Shirt).Hue);
            Assert.Equal(0x21e, mobile.FindItemOnLayer(Layer.Pants).Hue);
            Assert.Equal(0x3e9, mobile.FindItemOnLayer(Layer.Shoes).Hue);
            CharacterCreation.ApplyBrowserClothing(mobile, new[] { 0, 0x1517, 0x1539, 0x170b }, 0x44);
            Assert.Null(mobile.FindItemOnLayer(Layer.Helm));
            Assert.Equal(0x44, mobile.FindItemOnLayer(Layer.Shirt).Hue);
            Assert.Equal(0x44, mobile.FindItemOnLayer(Layer.Shoes).Hue);
        }
        finally
        {
            mobile.Delete();
        }
    }
}
