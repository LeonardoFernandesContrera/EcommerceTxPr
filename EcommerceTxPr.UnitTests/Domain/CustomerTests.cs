using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.Domain;

public sealed class CustomerTests
{
    [Fact]
    public void Constructor_initializes_identity_state_and_details()
    {
        var birthDate = new DateTime(1990, 5, 12);
        var beforeCreation = DateTime.UtcNow;

        var customer = new Customer("Ada Lovelace", birthDate);

        var afterCreation = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.True(customer.IsActive);
        Assert.Equal(DateTimeKind.Utc, customer.CreationDate.Kind);
        Assert.InRange(customer.CreationDate, beforeCreation, afterCreation);
        Assert.Equal("Ada Lovelace", customer.Name);
        Assert.Equal(birthDate, customer.BirthDate);
    }

    [Fact]
    public void UpdateDetails_changes_editable_details_only()
    {
        var customer = new Customer("Original Name", new DateTime(1985, 3, 10));
        var originalId = customer.Id;
        var originalCreationDate = customer.CreationDate;

        customer.UpdateDetails("Updated Name", new DateTime(1986, 4, 11));

        Assert.Equal("Updated Name", customer.Name);
        Assert.Equal(new DateTime(1986, 4, 11), customer.BirthDate);
        Assert.Equal(originalId, customer.Id);
        Assert.Equal(originalCreationDate, customer.CreationDate);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public void Deactivate_marks_customer_inactive_without_changing_identity()
    {
        var customer = new Customer("Grace Hopper", new DateTime(1906, 12, 9));
        var originalId = customer.Id;
        var originalCreationDate = customer.CreationDate;

        customer.Deactivate();

        Assert.False(customer.IsActive);
        Assert.Equal(originalId, customer.Id);
        Assert.Equal(originalCreationDate, customer.CreationDate);
    }
}
