using Dapper;
using System.Data;
using TaskFive.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(30);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(builder =>
	{
		builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
	});
});
builder.Services.AddScoped<IDbConnection>(DB => DatabaseContext.GetConnection());

var app = builder.Build();

app.UseCors();

app.UseStaticFiles();

app.UseSession();

DatabaseContext.CreateTables();

app.MapPost("/register", async (HttpRequest request, IDbConnection db) =>
{
	var form = await request.ReadFormAsync();
	var email = form["email"].ToString();
	var password = form["password"].ToString();

	var sql = "INSERT INTO USERS (Email, Password) VALUES (@Email, @Password)";
	try
	{
		User user = new User(email, password);
		await db.ExecuteAsync(sql, user);
		return Results.Text(@"<script>window.location.href = '/login.html';</script>", "text/html");
	}
	catch (Exception ex)
	{
		return Results.Text($"Registration failed: {ex.Message}");
	}
});

app.MapPost("/login", async (HttpRequest request, IDbConnection db) =>
{
	var form = await request.ReadFormAsync();
	var email = form["email"].ToString();
	var password = form["password"].ToString();

	var sql = "SELECT * FROM Users WHERE Email = @Email AND Password = @Password";
	var user = await db.QuerySingleOrDefaultAsync<User>(sql, new { Email = email, Password = password }) ;
	
	if (user != null)
	{
		try
		{
			request.HttpContext.Session.SetInt32("userId", (int)user.ID);
			string redirectScript = "<script>window.location.href = '/addFeed.html';</script>";
			return Results.Text(redirectScript, "text/html");
		}
		catch (Exception ex)
		{
			return Results.Text($"Login failed: {ex.Message}");
		}
	}
	return Results.Text("Invalid email or password.");
});

app.MapPost("/feeds", async (HttpRequest request, IDbConnection db) =>
{
	var form = await request.ReadFormAsync();
	var url = form["url"].ToString();
	var userId = request.HttpContext.Session.GetInt32("userId");
	if (userId == null)
	{
		return Results.Text("Unauthorized", statusCode: 401);
	}

	var sqlInsert = "INSERT INTO Feeds (UserId, Url) VALUES (@UserId, @Url)";
	await db.ExecuteAsync(sqlInsert, new { UserId = userId, Url = url });

	var sqlSelect = "SELECT * FROM Feeds WHERE UserId = @UserId";
	var feeds = await db.QueryAsync<Feed>(sqlSelect, new { UserId = userId });

	var feedsHtml = "";
	foreach (var feed in feeds)
	{
		feedsHtml += $"<li class='list-group-item d-flex justify-content-between align-items-center'>{feed.Url} " +
					 $"<button class='btn btn-danger btn-sm' onclick='removeFeed({feed.Id})'>Remove</button></li>";
	}
	return Results.Text(feedsHtml, "text/html");
});


app.MapPost("/getFeeds", async (HttpRequest request, IDbConnection db) =>
{
	var userId = request.HttpContext.Session.GetInt32("userId");
	if (userId == null)
	{
		return Results.Text("Unauthorized", statusCode: 401);
	}

	var sql = "SELECT * FROM Feeds WHERE UserId = @UserId";
	var feeds = await db.QueryAsync<Feed>(sql, new { UserId = userId });

	return Results.Json(feeds);
});

app.MapPost("/removeFeed", async (HttpRequest request, IDbConnection db) =>
{
	var form = await request.ReadFormAsync();
	var feedId = int.Parse(form["feedId"]);
	var sql = "DELETE FROM Feeds WHERE Id = @Id";
	await db.ExecuteAsync(sql, new { Id = feedId });
	return Results.Ok();
});

app.MapFallbackToFile("index.html");
app.Run();

public record User
{
	public long ID { get; init; }
	public string Email { get; init; }
	public string Password { get; init; }

	public User() { }

	public User(string email, string password)
	{
		Email = email;
		Password = password;
	}
}
public record Feed {
	public long Id { get; init; }
	public long UserId { get; init; }
	public string Url { get; init; }
    public Feed(){}
    public Feed(long id , long userId , string url)
    {
		Id = id;
		UserId = userId;
		Url = url;
    }
}

