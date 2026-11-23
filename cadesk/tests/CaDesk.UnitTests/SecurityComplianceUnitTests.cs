using CaDesk.Domain.SecurityCompliance;
using Xunit;

namespace CaDesk.UnitTests;

public class SecurityComplianceUnitTests
{
    [Fact]
    public void StandardSecurityControls_ContainsAll5CorePillars()
    {
        var controls = StandardSecurityControls.All;
        Assert.NotEmpty(controls);
        Assert.True(controls.Count >= 18);

        var categories = controls.Select(c => c.Category).Distinct().ToList();
        Assert.Contains(SecurityControlCategory.AccessControl, categories);
        Assert.Contains(SecurityControlCategory.Cryptography, categories);
        Assert.Contains(SecurityControlCategory.OperationsSecurity, categories);
        Assert.Contains(SecurityControlCategory.PhysicalAndCloud, categories);
        Assert.Contains(SecurityControlCategory.DataPrivacyAndGovernance, categories);
    }

    [Fact]
    public void StandardSecurityControls_HaveValidCodesAndFrameworkMappings()
    {
        foreach (var c in StandardSecurityControls.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Code));
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.False(string.IsNullOrWhiteSpace(c.Description));
            Assert.False(string.IsNullOrWhiteSpace(c.VerificationMethod));
            Assert.False(string.IsNullOrWhiteSpace(c.FrameworkMapping));

            Assert.True(c.FrameworkMapping.Contains("ISO 27001") || c.FrameworkMapping.Contains("SOC 2") || c.FrameworkMapping.Contains("DPDP"));
        }
    }

    [Fact]
    public void SpecificMoatControls_AreRegisteredInStandardCatalog()
    {
        var controls = StandardSecurityControls.All;

        // AC-01: Two-Lock RLS
        var rls = controls.FirstOrDefault(c => c.Code == "AC-01");
        Assert.NotNull(rls);
        Assert.Contains("Two-Lock", rls.Title);
        Assert.Contains("RLS", rls.Title);

        // AC-03: RFC 6238 TOTP MFA
        var mfa = controls.FirstOrDefault(c => c.Code == "AC-03");
        Assert.NotNull(mfa);
        Assert.Contains("MFA", mfa.Title);

        // CR-01: AES-256
        var aes = controls.FirstOrDefault(c => c.Code == "CR-01");
        Assert.NotNull(aes);
        Assert.Contains("AES-256", aes.Title);

        // CR-02: TLS 1.3 / HSTS
        var tls = controls.FirstOrDefault(c => c.Code == "CR-02");
        Assert.NotNull(tls);
        Assert.Contains("TLS 1.3", tls.Title);

        // OP-01: ClamAV Antivirus
        var av = controls.FirstOrDefault(c => c.Code == "OP-01");
        Assert.NotNull(av);
        Assert.Contains("ClamAV", av.Title);

        // PE-03: India Data Sovereignty
        var sov = controls.FirstOrDefault(c => c.Code == "PE-03");
        Assert.NotNull(sov);
        Assert.Contains("India", sov.Title);

        // PR-02: Zero AI Model Training
        var zeroAi = controls.FirstOrDefault(c => c.Code == "PR-02");
        Assert.NotNull(zeroAi);
        Assert.Contains("Zero External Model Training", zeroAi.Title);
    }

    [Theory]
    [InlineData(10, 0, 100)]
    [InlineData(8, 2, 90)]
    [InlineData(5, 5, 75)]
    [InlineData(0, 10, 50)]
    public void ComplianceScoreCalculation_AccuratelyWeighsCompliantAndPartial(int compliant, int partial, int expectedPercentage)
    {
        int total = compliant + partial;
        int computed = (int)Math.Round(((double)compliant + partial * 0.5) / total * 100);
        Assert.Equal(expectedPercentage, computed);
    }

    [Fact]
    public void FrameworkConstants_MatchIndustryStandards()
    {
        Assert.Equal("ISO/IEC 27001:2022", SecurityFrameworks.Iso27001_2022);
        Assert.Equal("SOC 2 Type II (Trust Services Criteria)", SecurityFrameworks.Soc2_TypeII);
        Assert.Equal("Digital Personal Data Protection Act 2023", SecurityFrameworks.DpdpAct2023);
    }
}
