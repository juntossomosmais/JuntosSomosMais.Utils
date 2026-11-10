using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb.Tests;

public class MongoRequestAuditRegistrationTests
{
    [Fact]
    public void AddMongoRequestAudit_WithConfiguration_RegistersTheStoreAndItsOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var database = new Mock<IMongoDatabase>();
        var resolutions = 0;

        // Act
        services.AddMongoRequestAudit(
            _ =>
            {
                resolutions++;
                return database.Object;
            },
            options => options.CollectionName = "customAudit");
        using var provider = services.BuildServiceProvider();
        var resolutionsBeforeUse = resolutions;
        var store = provider.GetRequiredService<IRequestAuditStore>();
        var purger = provider.GetRequiredService<IRequestAuditPurger>();
        var options = provider.GetRequiredService<MongoRequestAuditOptions>();

        // Assert
        Assert.Equal(0, resolutionsBeforeUse);
        Assert.Equal(2, resolutions);
        Assert.IsType<MongoRequestAuditStore>(store);
        Assert.Same(store, provider.GetRequiredService<IRequestAuditStore>());
        Assert.IsType<MongoRequestAuditPurger>(purger);
        Assert.Same(purger, provider.GetRequiredService<IRequestAuditPurger>());
        Assert.Equal("customAudit", options.CollectionName);
    }

    [Fact]
    public void AddMongoRequestAudit_WithoutConfiguration_UsesTheDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMongoRequestAudit(_ => new Mock<IMongoDatabase>().Object);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MongoRequestAuditOptions>();

        // Assert
        Assert.Equal("requestAuditLogs", options.CollectionName);
    }

    [Fact]
    public void AddMongoRequestAudit_StoreAlreadyRegistered_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMongoRequestAudit(_ => new Mock<IMongoDatabase>().Object);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddMongoRequestAudit(_ => new Mock<IMongoDatabase>().Object));

        // Assert
        Assert.Equal("A request audit store is already registered.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddMongoRequestAudit_InvalidOptions_Throws(string collectionName)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddMongoRequestAudit(
            _ => new Mock<IMongoDatabase>().Object, options => options.CollectionName = collectionName));

        // Assert
        Assert.Equal("CollectionName is required.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddMongoRequestAudit_NullDatabaseFactory_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => services.AddMongoRequestAudit(null!));

        // Assert
        Assert.Equal("database", exception.ParamName);
    }
}
