using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Graph;

namespace MailMonitor.UnitTests.Domain;

public sealed class GraphSettingValidationTests
{
    [Fact]
    public void Validate_ShouldFail_WhenInstanceIsEmpty()
    {
        var graphSetting = BuildValidGraphSetting();
        graphSetting.Instance = string.Empty;

        var result = graphSetting.Validate();

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.GraphSetting.InstanceRequired.Code, result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldFail_WhenScopesJsonIsInvalid()
    {
        var graphSetting = BuildValidGraphSetting();
        graphSetting.GraphUserScopesJson = "{invalid-json}";

        var result = graphSetting.Validate();

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.GraphSetting.InvalidScopesJson.Code, result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAllRequiredFieldsArePresent()
    {
        var graphSetting = BuildValidGraphSetting();

        var result = graphSetting.Validate();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetScopes_ShouldNormalizeAndDeduplicateValues()
    {
        var graphSetting = BuildValidGraphSetting();

        graphSetting.SetScopes([" Mail.Read ", "mail.read", "Files.ReadWrite", " "]);

        var scopes = graphSetting.GetScopes();

        Assert.Equal(["Mail.Read", "Files.ReadWrite"], scopes);
    }

    private static GraphSetting BuildValidGraphSetting()
    {
        var graphSetting = new GraphSetting
        {
            Instance = "https://login.microsoftonline.com/",
            ClientId = "client-id",
            TenantId = "tenant-id",
            ClientSecret = "super-secret"
        };

        graphSetting.SetScopes(["https://graph.microsoft.com/.default"]);
        return graphSetting;
    }
}
