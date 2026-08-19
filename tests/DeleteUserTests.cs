using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UsersWebAPI.Data;
using UsersWebAPI.Models;

namespace UsersWebAPI.Tests;

public class DeleteUserTests
{
    private WebApplicationFactory<Program>? _factory;

    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace DB with in-memory Sqlite
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(connection);
                });

                // Build the service provider and ensure DB created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    [Test]
    public async Task DeleteUser_RemovesUser()
    {
        var client = _factory!.CreateClient();

        // Login to get token
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { Username = "test", Password = "test" });
        loginResp.EnsureSuccessStatusCode();
        var loginObj = await loginResp.Content.ReadFromJsonAsync<Dictionary<string,string>>();
        var token = loginObj!["token"];

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a user
        var newUser = new User { Name = "DeleteMe", Age = 30, City = "X", State = "Y", Pincode = "12345" };
        var postResp = await client.PostAsJsonAsync("/api/users", newUser);
        postResp.EnsureSuccessStatusCode();
        var created = await postResp.Content.ReadFromJsonAsync<User>();
        Assert.IsNotNull(created);

        // Delete the user
        var delResp = await client.DeleteAsync($"/api/users/{created!.Id}");
        Assert.IsTrue(delResp.IsSuccessStatusCode);
        Assert.AreEqual(System.Net.HttpStatusCode.NoContent, delResp.StatusCode);

        // Check not found
        var getResp = await client.GetAsync($"/api/users/{created.Id}");
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, getResp.StatusCode);
    }
}
