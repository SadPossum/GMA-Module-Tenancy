namespace Gma.Modules.Tenancy.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Modules.Tenancy.Api;
using Gma.Modules.Tenancy.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenancyModuleTests
{
    [Fact]
    public void Default_profile_declares_context_and_header_resolution()
    {
        Assert.Equal(TenancyModuleMetadata.Name, TenancyProfiles.Default.ModuleName);
        Assert.Equal(TenancyProfiles.DefaultName, TenancyProfiles.Default.ProfileName);
        Assert.Contains(
            TenancyProfiles.Default.Provides,
            feature => feature.Id == TenancyCompositionFeatures.Context);
        Assert.Contains(
            TenancyProfiles.Default.Provides,
            feature => feature.Id == TenancyCompositionFeatures.HeaderResolution);
    }

    [Fact]
    public async Task Registration_forces_enabled_scoped_context_and_isolates_state()
    {
        WebApplicationBuilder builder = CreateBuilder();
        builder.AddTenancyInfrastructure();
        builder.AddModule<TenancyModule>();
        builder.ValidateModuleComposition();

        await using WebApplication app = builder.Build();

        using (IServiceScope firstScope = app.Services.CreateScope())
        {
            ITenantContext context = firstScope.ServiceProvider.GetRequiredService<ITenantContext>();
            ITenantContextAccessor accessor = firstScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

            Assert.Same(context, accessor);
            Assert.True(context.IsEnabled);
            Assert.Null(context.TenantId);

            accessor.SetTenant(" tenant-a ");
            Assert.Equal("tenant-a", context.TenantId);
        }

        using IServiceScope secondScope = app.Services.CreateScope();
        ITenantContext secondContext = secondScope.ServiceProvider.GetRequiredService<ITenantContext>();
        Assert.True(secondContext.IsEnabled);
        Assert.Null(secondContext.TenantId);
    }

    [Fact]
    public async Task Current_endpoint_resolves_custom_header_and_rejects_invalid_input()
    {
        await using RunningApplication running = await StartApplicationAsync();

        using HttpResponseMessage missing = await running.Client.GetAsync("/api/tenants/current");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(TenantErrors.TenantRequired.Code, await ReadProblemTitleAsync(missing));

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/current");
        invalidRequest.Headers.TryAddWithoutValidation("X-Workspace-Id", "tenant alpha");
        using HttpResponseMessage invalid = await running.Client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(TenantErrors.TenantInvalid.Code, await ReadProblemTitleAsync(invalid));

        using var validRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/current");
        validRequest.Headers.Add("X-Workspace-Id", "tenant-a");
        using HttpResponseMessage valid = await running.Client.SendAsync(validRequest);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        CurrentTenantResponse? response = await valid.Content.ReadFromJsonAsync<CurrentTenantResponse>();
        Assert.Equal(new CurrentTenantResponse("tenant-a", IsEnabled: true), response);
    }

    [Fact]
    public async Task Current_endpoint_honors_registered_access_policy()
    {
        await using RunningApplication running = await StartApplicationAsync(
            services => services.AddSingleton<ITenantEndpointAccessPolicy, DenyTenantAccessPolicy>());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/current");
        request.Headers.Add("X-Workspace-Id", "tenant-a");
        using HttpResponseMessage response = await running.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(DenyTenantAccessPolicy.ErrorCode, await ReadProblemTitleAsync(response));
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new($"{TenantOptions.SectionName}:Enabled", "false"),
            new($"{TenantOptions.SectionName}:HeaderName", "X-Workspace-Id"),
            new($"{TenantOptions.SectionName}:LocalDefaultTenantId", "local-default")
        ]);
        return builder;
    }

    private static async Task<RunningApplication> StartApplicationAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        WebApplicationBuilder builder = CreateBuilder();
        builder.AddTenancyInfrastructure();
        builder.AddModule<TenancyModule>();
        configureServices?.Invoke(builder.Services);
        builder.ValidateModuleComposition();

        WebApplication app = builder.Build();
        app.MapModules();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        IServer server = app.Services.GetRequiredService<IServer>();
        string address = Assert.Single(server.Features.Get<IServerAddressesFeature>()!.Addresses);
        return new RunningApplication(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    private static async Task<string?> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        await using Stream content = await response.Content.ReadAsStreamAsync();
        using JsonDocument problem = await JsonDocument.ParseAsync(content);
        return problem.RootElement.GetProperty("title").GetString();
    }

    private sealed class DenyTenantAccessPolicy : ITenantEndpointAccessPolicy
    {
        public const string ErrorCode = "TenancyTests.AccessDenied";

        public ValueTask<TenantEndpointAccessDecision> AuthorizeAsync(
            HttpContext httpContext,
            string tenantId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(TenantEndpointAccessDecision.Denied(
                ErrorCode,
                "Tenant access was denied by the test policy.",
                StatusCodes.Status403Forbidden));
    }

    private sealed class RunningApplication(WebApplication application, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await application.DisposeAsync();
        }
    }
}
