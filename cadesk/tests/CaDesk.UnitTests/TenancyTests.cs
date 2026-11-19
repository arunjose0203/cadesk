using CaDesk.Application.Abstractions;
using CaDesk.Infrastructure.Persistence;
using CaDesk.Infrastructure.Tenancy;

namespace CaDesk.UnitTests;

public class TenantContextTests
{
    [Fact]
    public void Reading_firm_before_it_is_set_throws()
    {
        var t = new TenantContext();
        Assert.False(t.HasTenant);
        Assert.Throws<TenantNotSetException>(() => t.FirmId);
    }

    [Fact]
    public void Firm_can_be_set_once()
    {
        var t = new TenantContext();
        var firm = Guid.NewGuid();
        t.Set(firm, Guid.NewGuid(), "Partner");
        Assert.True(t.HasTenant);
        Assert.Equal(firm, t.FirmId);
        Assert.Equal("Partner", t.Role);
    }

    [Fact]
    public void Changing_firm_in_the_same_scope_is_refused()
    {
        var t = new TenantContext();
        t.Set(Guid.NewGuid(), null, null);
        Assert.Throws<InvalidOperationException>(() => t.Set(Guid.NewGuid(), null, null));
    }

    [Fact]
    public void Empty_firm_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new TenantContext().Set(Guid.Empty, null, null));
    }
}

public class SnakeCaseTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("FirmId", "firm_id")]
    [InlineData("TaxIds", "tax_ids")]
    [InlineData("AssignedUserId", "assigned_user_id")]
    [InlineData("IsActive", "is_active")]
    [InlineData("XMLFile", "xml_file")]
    [InlineData("Address2Line", "address2_line")]
    public void Converts_property_names(string input, string expected) =>
        Assert.Equal(expected, input.ToSnakeCase());
}
