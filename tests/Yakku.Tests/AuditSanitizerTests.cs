using Yakku.Application.Audit;

namespace Yakku.Tests;

public class AuditSanitizerTests
{
    private const string Jwt =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    [Fact]
    public void SanitizeText_RedactsJwtsBearerTokensAndKeyValueSecrets()
    {
        var text = $"failed for token {Jwt}; Authorization: Bearer abc.def-ghi; password=hunter2 refresh_token=\"xyz\"";

        var clean = AuditSanitizer.SanitizeText(text, 500)!;

        Assert.DoesNotContain(Jwt, clean);
        Assert.DoesNotContain("abc.def-ghi", clean);
        Assert.DoesNotContain("hunter2", clean);
        Assert.DoesNotContain("xyz", clean);
        Assert.Contains(AuditSanitizer.Redacted, clean);
    }

    [Fact]
    public void SanitizeText_RedactsEmailsPhoneNumbersOtpsAndOpaqueSecrets()
    {
        var clean = AuditSanitizer.SanitizeText(
            "rejected jane.doe@example.com / +919876543210 code 123456 key AKIAIOSFODNN7EXAMPLE1234567890ABCDEF",
            500)!;

        Assert.DoesNotContain("jane.doe@example.com", clean);
        Assert.DoesNotContain("9876543210", clean);
        Assert.DoesNotContain("123456", clean);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", clean);
    }

    [Fact]
    public void SanitizeText_StripsControlCharactersAndTruncates()
    {
        var clean = AuditSanitizer.SanitizeText("line one\r\nline two\u0000" + new string('x', 40), 20)!;

        Assert.DoesNotContain('\n', clean);
        Assert.DoesNotContain('\r', clean);
        Assert.DoesNotContain('\0', clean);
        Assert.Equal(20, clean.Length);
    }

    [Fact]
    public void SanitizeText_ReturnsNullForBlank()
    {
        Assert.Null(AuditSanitizer.SanitizeText("  ", 10));
        Assert.Null(AuditSanitizer.SanitizeText(null, 10));
    }

    [Theory]
    [InlineData("installation_owned_by_another_account")]
    [InlineData("OTP_ATTEMPTS_EXCEEDED")]
    [InlineData("Http503")]
    [InlineData("InvalidOperationException")]
    public void SanitizeCode_KeepsPlainReasonCodesReadable(string code)
    {
        Assert.Equal(code, AuditSanitizer.SanitizeCode(code, 128));
    }

    [Fact]
    public void SanitizeCode_StillRedactsAnythingThatIsNotACode()
    {
        Assert.DoesNotContain(Jwt, AuditSanitizer.SanitizeCode(Jwt, 500));
        Assert.DoesNotContain("jane@example.com", AuditSanitizer.SanitizeCode("bad jane@example.com", 128));
        Assert.Equal(10, AuditSanitizer.SanitizeCode("a_code_that_is_too_long", 10)!.Length);
    }

    [Theory]
    [InlineData("jane@example.com", "j***@example.com")]
    [InlineData("+919876543210", "***10")]
    public void MaskRecipient_HidesTheAddress(string input, string expected)
    {
        Assert.Equal(expected, AuditSanitizer.MaskRecipient(input));
    }

    [Fact]
    public void HashSubject_IsStableCaseInsensitiveAndNotReversible()
    {
        var a = AuditSanitizer.HashSubject("Jane@Example.com");
        var b = AuditSanitizer.HashSubject(" jane@example.com ");

        Assert.Equal(a, b);
        Assert.Equal(64, a!.Length);
        Assert.DoesNotContain("jane", a);
        Assert.NotEqual(a, AuditSanitizer.HashSubject("john@example.com"));
        Assert.Null(AuditSanitizer.HashSubject(null));
    }

    [Fact]
    public void PseudonymizeVoter_IsStablePerPollAndUnlinkableAcrossPolls()
    {
        var voter = Guid.NewGuid();
        var pollA = Guid.NewGuid();
        var pollB = Guid.NewGuid();

        Assert.Equal(AuditSanitizer.PseudonymizeVoter(voter, pollA), AuditSanitizer.PseudonymizeVoter(voter, pollA));
        Assert.NotEqual(AuditSanitizer.PseudonymizeVoter(voter, pollA), AuditSanitizer.PseudonymizeVoter(voter, pollB));
        Assert.DoesNotContain(voter.ToString("N"), AuditSanitizer.PseudonymizeVoter(voter, pollA));
    }

    [Fact]
    public void NormalizePath_ReplacesIdsAndDropsTheQueryString()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            "/api/v1/polls/{id}/votes",
            AuditSanitizer.NormalizePath($"/api/v1/polls/{id}/votes?token=secret&x=1"));
        Assert.Equal("/api/v1/users/{id}", AuditSanitizer.NormalizePath("/api/v1/users/42"));
    }

    [Fact]
    public void SerializeMetadata_DropsSensitiveKeysAndSanitizesValues()
    {
        var json = AuditSanitizer.SerializeMetadata(new Dictionary<string, string?>
        {
            ["refreshToken"] = "should-not-appear",
            ["Authorization"] = "Bearer nope",
            ["otp"] = "123456",
            ["guestId"] = "0b6f1c1e-2f3d-4a5b-8c9d-0e1f2a3b4c5d",
            ["count"] = "5000",
            ["note"] = "contact jane@example.com"
        })!;

        Assert.DoesNotContain("should-not-appear", json);
        Assert.DoesNotContain("nope", json);
        Assert.DoesNotContain("123456", json);
        Assert.DoesNotContain("jane@example.com", json);
        Assert.Contains("0b6f1c1e-2f3d-4a5b-8c9d-0e1f2a3b4c5d", json);
        Assert.Contains("5000", json);
    }

    [Fact]
    public void SerializeMetadata_ReturnsNullWhenNothingSafeRemains()
    {
        Assert.Null(AuditSanitizer.SerializeMetadata(null));
        Assert.Null(AuditSanitizer.SerializeMetadata(new Dictionary<string, string?> { ["password"] = "x" }));
    }
}
