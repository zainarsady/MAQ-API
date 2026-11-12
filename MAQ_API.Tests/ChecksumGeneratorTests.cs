using MAQ_API.Helpers;
using Xunit;

namespace MAQ_API.Tests;

public class ChecksumGeneratorTests
{
    private const string Key = "test-private-key";

    private static Dictionary<string, string> InitiateSample() => new()
    {
        ["name"] = "Ali bin Abu",
        ["email"] = "ali@example.com",
        ["phone"] = "0123456789",
        ["amount"] = "10.00",
        ["description"] = "Order #1001",
        ["payment_code"] = "ORDER-1001",
        ["payment_method"] = "Online Banking",
        ["bank_prefix"] = "ABB0233",
    };

    [Fact]
    public void Payload_MatchesDocsInitiateExample() =>
        Assert.Equal("10.00,ABB0233,Order #1001,ali@example.com,Ali bin Abu,ORDER-1001,Online Banking,0123456789",
            ChecksumGenerator.BuildPayload(InitiateSample()));

    [Fact]
    public void Payload_MatchesDocsCallbackExample()
    {
        var cb = new Dictionary<string, string>
        {
            ["payment_code"] = "ORDER-1001", ["reference_code"] = "HP-INVAPI-6720F1A2B3C4D", ["status"] = "Success",
            ["status_code"] = "00", ["amount"] = "10.00", ["currency"] = "MYR", ["bank_name"] = "Affin Bank",
            ["transaction_id"] = "2410281530450123", ["message"] = "Approved", ["payment_method"] = "FPX",
            ["fpx_type"] = "FPX", ["checksum"] = "ignored",
        };
        Assert.Equal("10.00,Affin Bank,MYR,FPX,Approved,ORDER-1001,FPX,HP-INVAPI-6720F1A2B3C4D,Success,00,2410281530450123",
            ChecksumGenerator.BuildPayload(cb));
    }

    [Fact]
    public void Checksum_FieldIsExcluded()
    {
        var d = InitiateSample();
        var expected = ChecksumGenerator.Generate(d, Key);
        d["checksum"] = "anything";
        Assert.Equal(expected, ChecksumGenerator.Generate(d, Key));
    }

    [Fact]
    public void Generate_IsLowercaseHex64()
    {
        var h = ChecksumGenerator.Generate(InitiateSample(), Key);
        Assert.Matches("^[0-9a-f]{64}$", h);
    }

    [Fact]
    public void AmountFormat_Matters()
    {
        var a = InitiateSample();
        var b = InitiateSample(); b["amount"] = "10";
        Assert.NotEqual(ChecksumGenerator.Generate(a, Key), ChecksumGenerator.Generate(b, Key));
    }

    [Fact]
    public void Verify_AcceptsValidAndRejectsTampered()
    {
        var d = InitiateSample();
        d["checksum"] = ChecksumGenerator.Generate(d, Key);
        Assert.True(ChecksumGenerator.Verify(d, d["checksum"], Key));

        d["amount"] = "99.00";
        Assert.False(ChecksumGenerator.Verify(d, d["checksum"], Key));
        Assert.False(ChecksumGenerator.Verify(d, null, Key));
        Assert.False(ChecksumGenerator.Verify(d, "", Key));
    }
}
