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

public class UsersControllerTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _token;

    [SetUp]
    public async Task Setup()
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

        _client = _factory.CreateClient();

        // Login to get token
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new { Username = "test", Password = "test" });
        loginResp.EnsureSuccessStatusCode();
        var loginObj = await loginResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        _token = loginObj!["token"];

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
        _client?.Dispose();
    }

    #region GetUsers Tests

    [Test]
    public async Task GetUsers_ReturnsEmptyList_WhenNoUsersExist()
    {
        var response = await _client!.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        Assert.IsNotNull(users);
        Assert.AreEqual(0, users.Count);
    }

    [Test]
    public async Task GetUsers_ReturnsAllUsers_WhenUsersExist()
    {
        // Arrange - Create multiple users
        var user1 = new User { Name = "John Doe", Age = 30, City = "New York", State = "NY", Pincode = "10001" };
        var user2 = new User { Name = "Jane Smith", Age = 28, City = "Los Angeles", State = "CA", Pincode = "90001" };
        var user3 = new User { Name = "Bob Johnson", Age = 35, City = "Chicago", State = "IL", Pincode = "60601" };

        await _client!.PostAsJsonAsync("/api/users", user1);
        await _client.PostAsJsonAsync("/api/users", user2);
        await _client.PostAsJsonAsync("/api/users", user3);

        // Act
        var response = await _client.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<User>>();

        // Assert
        Assert.IsNotNull(users);
        Assert.AreEqual(3, users.Count);
        Assert.IsTrue(users.Any(u => u.Name == "John Doe"));
        Assert.IsTrue(users.Any(u => u.Name == "Jane Smith"));
        Assert.IsTrue(users.Any(u => u.Name == "Bob Johnson"));
    }

    [Test]
    public async Task GetUsers_ReturnsUsers_WithCorrectProperties()
    {
        // Arrange
        var user = new User { Name = "Test User", Age = 25, City = "TestCity", State = "TS", Pincode = "12345" };
        var createResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await createResp.Content.ReadFromJsonAsync<User>();

        // Act
        var response = await _client.GetAsync("/api/users");
        var users = await response.Content.ReadFromJsonAsync<List<User>>();

        // Assert
        var retrievedUser = users!.FirstOrDefault(u => u.Id == created!.Id);
        Assert.IsNotNull(retrievedUser);
        Assert.AreEqual("Test User", retrievedUser!.Name);
        Assert.AreEqual(25, retrievedUser.Age);
        Assert.AreEqual("TestCity", retrievedUser.City);
        Assert.AreEqual("TS", retrievedUser.State);
        Assert.AreEqual("12345", retrievedUser.Pincode);
    }

    #endregion

    #region GetUser by ID Tests

    [Test]
    public async Task GetUser_ReturnsUser_WhenUserExists()
    {
        // Arrange
        var user = new User { Name = "Alice", Age = 29, City = "Boston", State = "MA", Pincode = "02101" };
        var postResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await postResp.Content.ReadFromJsonAsync<User>();

        // Act
        var response = await _client.GetAsync($"/api/users/{created!.Id}");
        response.EnsureSuccessStatusCode();
        var retrieved = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(created.Id, retrieved!.Id);
        Assert.AreEqual("Alice", retrieved.Name);
        Assert.AreEqual(29, retrieved.Age);
    }

    [Test]
    public async Task GetUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync("/api/users/9999");

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Test]
    public async Task GetUser_ReturnsCorrectUser_WhenMultipleUsersExist()
    {
        // Arrange
        var user1 = new User { Name = "User1", Age = 20, City = "City1", State = "S1", Pincode = "11111" };
        var user2 = new User { Name = "User2", Age = 21, City = "City2", State = "S2", Pincode = "22222" };
        var user3 = new User { Name = "User3", Age = 22, City = "City3", State = "S3", Pincode = "33333" };

        var resp1 = await _client!.PostAsJsonAsync("/api/users", user1);
        var resp2 = await _client.PostAsJsonAsync("/api/users", user2);
        var resp3 = await _client.PostAsJsonAsync("/api/users", user3);

        var created1 = await resp1.Content.ReadFromJsonAsync<User>();
        var created2 = await resp2.Content.ReadFromJsonAsync<User>();
        var created3 = await resp3.Content.ReadFromJsonAsync<User>();

        // Act & Assert - Get each user and verify correct data
        var get1 = await _client.GetAsync($"/api/users/{created1!.Id}");
        var retrieved1 = await get1.Content.ReadFromJsonAsync<User>();
        Assert.AreEqual("User1", retrieved1!.Name);

        var get2 = await _client.GetAsync($"/api/users/{created2!.Id}");
        var retrieved2 = await get2.Content.ReadFromJsonAsync<User>();
        Assert.AreEqual("User2", retrieved2!.Name);

        var get3 = await _client.GetAsync($"/api/users/{created3!.Id}");
        var retrieved3 = await get3.Content.ReadFromJsonAsync<User>();
        Assert.AreEqual("User3", retrieved3!.Name);
    }

    #endregion

    #region CreateUser Tests

    [Test]
    public async Task CreateUser_ReturnsCreatedAtAction_WithUser()
    {
        // Arrange
        var user = new User { Name = "New User", Age = 27, City = "Seattle", State = "WA", Pincode = "98101" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/users", user);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<User>();
        Assert.IsNotNull(created);
        Assert.AreNotEqual(0, created!.Id);
        Assert.AreEqual("New User", created.Name);
        Assert.AreEqual(27, created.Age);
    }

    [Test]
    public async Task CreateUser_StoresUserInDatabase()
    {
        // Arrange
        var user = new User { Name = "Stored User", Age = 31, City = "Miami", State = "FL", Pincode = "33101" };

        // Act
        var postResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await postResp.Content.ReadFromJsonAsync<User>();

        // Assert - Verify we can retrieve the user
        var getResp = await _client.GetAsync($"/api/users/{created!.Id}");
        getResp.EnsureSuccessStatusCode();
        var retrieved = await getResp.Content.ReadFromJsonAsync<User>();
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(created.Id, retrieved!.Id);
    }

    [Test]
    public async Task CreateUser_IncrementsId()
    {
        // Arrange & Act
        var user1 = new User { Name = "First", Age = 20, City = "C1", State = "S1", Pincode = "00001" };
        var user2 = new User { Name = "Second", Age = 21, City = "C2", State = "S2", Pincode = "00002" };

        var resp1 = await _client!.PostAsJsonAsync("/api/users", user1);
        var created1 = await resp1.Content.ReadFromJsonAsync<User>();

        var resp2 = await _client.PostAsJsonAsync("/api/users", user2);
        var created2 = await resp2.Content.ReadFromJsonAsync<User>();

        // Assert
        Assert.IsNotNull(created1);
        Assert.IsNotNull(created2);
        Assert.AreNotEqual(created1!.Id, created2!.Id);
        Assert.Greater(created2.Id, created1.Id);
    }

    [Test]
    public async Task CreateUser_PreservesAllUserProperties()
    {
        // Arrange
        var user = new User
        {
            Name = "Complete User",
            Age = 40,
            City = "Denver",
            State = "CO",
            Pincode = "80202"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        Assert.IsNotNull(created);
        Assert.AreEqual("Complete User", created!.Name);
        Assert.AreEqual(40, created.Age);
        Assert.AreEqual("Denver", created.City);
        Assert.AreEqual("CO", created.State);
        Assert.AreEqual("80202", created.Pincode);
    }

    #endregion

    #region UpdateUser Tests

    [Test]
    public async Task UpdateUser_ModifiesExistingUser()
    {
        // Arrange
        var user = new User { Name = "Original", Age = 25, City = "Original City", State = "OS", Pincode = "00000" };
        var postResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await postResp.Content.ReadFromJsonAsync<User>();

        var updated = new User { Name = "Updated", Age = 26, City = "Updated City", State = "US", Pincode = "11111" };

        // Act
        var putResp = await _client.PutAsJsonAsync($"/api/users/{created!.Id}", updated);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NoContent, putResp.StatusCode);

        // Verify update
        var getResp = await _client.GetAsync($"/api/users/{created.Id}");
        var retrieved = await getResp.Content.ReadFromJsonAsync<User>();
        Assert.AreEqual("Updated", retrieved!.Name);
        Assert.AreEqual(26, retrieved.Age);
        Assert.AreEqual("Updated City", retrieved.City);
        Assert.AreEqual("US", retrieved.State);
        Assert.AreEqual("11111", retrieved.Pincode);
    }

    [Test]
    public async Task UpdateUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var updated = new User { Name = "Updated", Age = 26, City = "City", State = "ST", Pincode = "12345" };

        // Act
        var response = await _client!.PutAsJsonAsync("/api/users/9999", updated);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Test]
    public async Task UpdateUser_UpdatesAllProperties()
    {
        // Arrange
        var user = new User { Name = "Old", Age = 20, City = "OldCity", State = "OC", Pincode = "00000" };
        var postResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await postResp.Content.ReadFromJsonAsync<User>();

        var updated = new User
        {
            Name = "Brand New",
            Age = 50,
            City = "New City",
            State = "NC",
            Pincode = "99999"
        };

        // Act
        await _client.PutAsJsonAsync($"/api/users/{created!.Id}", updated);
        var getResp = await _client.GetAsync($"/api/users/{created.Id}");
        var retrieved = await getResp.Content.ReadFromJsonAsync<User>();

        // Assert
        Assert.AreEqual("Brand New", retrieved!.Name);
        Assert.AreEqual(50, retrieved.Age);
        Assert.AreEqual("New City", retrieved.City);
        Assert.AreEqual("NC", retrieved.State);
        Assert.AreEqual("99999", retrieved.Pincode);
    }

    [Test]
    public async Task UpdateUser_PreservesId()
    {
        // Arrange
        var user = new User { Name = "Test", Age = 30, City = "Test", State = "TS", Pincode = "12345" };
        var postResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await postResp.Content.ReadFromJsonAsync<User>();
        var originalId = created!.Id;

        var updated = new User { Name = "Modified", Age = 31, City = "Test", State = "TS", Pincode = "12345" };

        // Act
        await _client.PutAsJsonAsync($"/api/users/{originalId}", updated);
        var getResp = await _client.GetAsync($"/api/users/{originalId}");
        var retrieved = await getResp.Content.ReadFromJsonAsync<User>();

        // Assert
        Assert.AreEqual(originalId, retrieved!.Id);
    }

    [Test]
    public async Task UpdateUser_DoesNotAffectOtherUsers()
    {
        // Arrange
        var user1 = new User { Name = "User1", Age = 25, City = "City1", State = "S1", Pincode = "11111" };
        var user2 = new User { Name = "User2", Age = 30, City = "City2", State = "S2", Pincode = "22222" };

        var resp1 = await _client!.PostAsJsonAsync("/api/users", user1);
        var resp2 = await _client.PostAsJsonAsync("/api/users", user2);
        var created1 = await resp1.Content.ReadFromJsonAsync<User>();
        var created2 = await resp2.Content.ReadFromJsonAsync<User>();

        var updated = new User { Name = "Modified", Age = 99, City = "Modified", State = "MO", Pincode = "99999" };

        // Act
        await _client.PutAsJsonAsync($"/api/users/{created1!.Id}", updated);

        // Assert - Verify user2 is unchanged
        var getResp = await _client.GetAsync($"/api/users/{created2!.Id}");
        var retrieved = await getResp.Content.ReadFromJsonAsync<User>();
        Assert.AreEqual("User2", retrieved!.Name);
        Assert.AreEqual(30, retrieved.Age);
        Assert.AreEqual("City2", retrieved.City);
    }

    #endregion

    #region DeleteUser Tests

    [Test]
    public async Task DeleteUser_RemovesUser()
    {
        // Arrange
        var user = new User { Name = "DeleteMe", Age = 30, City = "X", State = "Y", Pincode = "12345" };
        var postResp = await _client!.PostAsJsonAsync("/api/users", user);
        var created = await postResp.Content.ReadFromJsonAsync<User>();

        // Act
        var delResp = await _client.DeleteAsync($"/api/users/{created!.Id}");

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NoContent, delResp.StatusCode);

        // Verify deletion
        var getResp = await _client.GetAsync($"/api/users/{created.Id}");
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Test]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Act
        var response = await _client!.DeleteAsync("/api/users/9999");

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Test]
    public async Task DeleteUser_RemovesFromGetAllUsers()
    {
        // Arrange
        var user1 = new User { Name = "Keep", Age = 25, City = "C1", State = "S1", Pincode = "11111" };
        var user2 = new User { Name = "Delete", Age = 26, City = "C2", State = "S2", Pincode = "22222" };

        var resp1 = await _client!.PostAsJsonAsync("/api/users", user1);
        var resp2 = await _client.PostAsJsonAsync("/api/users", user2);
        var created1 = await resp1.Content.ReadFromJsonAsync<User>();
        var created2 = await resp2.Content.ReadFromJsonAsync<User>();

        // Act
        await _client.DeleteAsync($"/api/users/{created2!.Id}");
        var getAllResp = await _client.GetAsync("/api/users");
        var allUsers = await getAllResp.Content.ReadFromJsonAsync<List<User>>();

        // Assert
        Assert.AreEqual(1, allUsers!.Count);
        Assert.AreEqual(created1!.Id, allUsers[0].Id);
        Assert.AreEqual("Keep", allUsers[0].Name);
    }

    [Test]
    public async Task DeleteUser_DoesNotAffectOtherUsers()
    {
        // Arrange
        var user1 = new User { Name = "User1", Age = 25, City = "C1", State = "S1", Pincode = "11111" };
        var user2 = new User { Name = "User2", Age = 26, City = "C2", State = "S2", Pincode = "22222" };

        var resp1 = await _client!.PostAsJsonAsync("/api/users", user1);
        var resp2 = await _client.PostAsJsonAsync("/api/users", user2);
        var created1 = await resp1.Content.ReadFromJsonAsync<User>();
        var created2 = await resp2.Content.ReadFromJsonAsync<User>();

        // Act
        await _client.DeleteAsync($"/api/users/{created1!.Id}");

        // Assert - Verify user2 still exists
        var getResp = await _client.GetAsync($"/api/users/{created2!.Id}");
        getResp.EnsureSuccessStatusCode();
        var retrieved = await getResp.Content.ReadFromJsonAsync<User>();
        Assert.AreEqual("User2", retrieved!.Name);
    }

    #endregion

    #region Authorization Tests

    [Test]
    public async Task GetUsers_ReturnsUnauthorized_WithoutToken()
    {
        // Arrange
        var clientNoAuth = _factory!.CreateClient();

        // Act
        var response = await clientNoAuth.GetAsync("/api/users");

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Test]
    public async Task CreateUser_ReturnsUnauthorized_WithoutToken()
    {
        // Arrange
        var clientNoAuth = _factory!.CreateClient();
        var user = new User { Name = "Test", Age = 25, City = "City", State = "ST", Pincode = "12345" };

        // Act
        var response = await clientNoAuth.PostAsJsonAsync("/api/users", user);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Test]
    public async Task UpdateUser_ReturnsUnauthorized_WithoutToken()
    {
        // Arrange
        var clientNoAuth = _factory!.CreateClient();
        var user = new User { Name = "Test", Age = 25, City = "City", State = "ST", Pincode = "12345" };

        // Act
        var response = await clientNoAuth.PutAsJsonAsync("/api/users/1", user);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Test]
    public async Task DeleteUser_ReturnsUnauthorized_WithoutToken()
    {
        // Arrange
        var clientNoAuth = _factory!.CreateClient();

        // Act
        var response = await clientNoAuth.DeleteAsync("/api/users/1");

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
