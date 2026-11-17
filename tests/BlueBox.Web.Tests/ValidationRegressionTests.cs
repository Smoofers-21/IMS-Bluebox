using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BlueBox.Web.Data;
using BlueBox.Web.Models;
using BlueBox.Web.Pages.Account;
using BlueBox.Web.Pages.Inventory;
using BlueBox.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace BlueBox.Web.Tests;

public sealed class ValidationRegressionTests
{
    [Theory]
    [InlineData("ItemId")]
    [InlineData("LocationId")]
    [InlineData("Direction")]
    [InlineData("Quantity")]
    public async Task MissingMovementFieldsAreRejectedByRealModelBinding(string missing)
    {
        await using var app = new TestApp();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var values = new Dictionary<string, StringValues>
        {
            ["Input.ItemId"] = "1",
            ["Input.LocationId"] = "1",
            ["Input.Direction"] = "CHECK IN",
            ["Input.Quantity"] = "5"
        };
        values.Remove("Input." + missing);
        var action = new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadata = services.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(typeof(MoveModel.MovementInput));
        var info = new BindingInfo { BindingSource = BindingSource.Form };
        var binder = services.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext
        { Metadata = metadata, BindingInfo = info, CacheToken = missing });
        var binding = DefaultModelBindingContext.CreateBindingContext(action,
            new FormValueProvider(BindingSource.Form, new FormCollection(values), CultureInfo.InvariantCulture), metadata, info, "Input");
        await binder.BindModelAsync(binding);
        Assert.True(binding.Result.IsModelSet);
        services.GetRequiredService<IObjectModelValidator>().Validate(action, null, "Input", binding.Result.Model!);
        Assert.False(action.ModelState.IsValid);
        Assert.NotEmpty(action.ModelState["Input." + missing]!.Errors);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(256)]
    public void SetupPasswordBoundariesCanBeHashedVerifiedAndAcceptedAtLogin(int length)
    {
        var password = new string('x', length);
        Assert.True(PasswordPolicy.IsValidForSetup(password));
        var employee = new Employee();
        var hasher = new PasswordHasher<Employee>();
        var hash = hasher.HashPassword(employee, password);
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(employee, hash, password));
        var login = new LoginModel(null!, hasher) { Username = "test.user", Password = password };
        Assert.True(Validator.TryValidateObject(login, new ValidationContext(login), [], true));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(257)]
    public async Task SetupRejectsInvalidPasswordLengthsBeforeOpeningADatabase(int length)
    {
        var database = new SqlDatabase(new ConfigurationBuilder().Build(), NullLogger<SqlDatabase>.Instance);
        var password = new string('x', length);
        var hasher = new PasswordHasher<Employee>();
        var managerError = await Assert.ThrowsAsync<InvalidOperationException>(() => ManagerBootstrap.CreateAsync(database, hasher, "first.manager", password));
        var seedError = await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeeder.SeedAsync(database, hasher, password));
        Assert.Contains("12–256", managerError.Message);
        Assert.Contains("12–256", seedError.Message);
        if (length > PasswordPolicy.MaximumLength)
        {
            var login = new LoginModel(null!, hasher) { Username = "test.user", Password = password };
            Assert.False(Validator.TryValidateObject(login, new ValidationContext(login), [], true));
        }
    }
}
