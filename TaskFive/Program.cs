using Dapper;
using System.Data;
using System.Xml.Linq;
using TaskFive.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient();
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

app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(self)");
    await next();
});

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
		return Results.Text(@"<script>window.location.href = '#login-section';</script>", "text/html");
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
	var user = await db.QuerySingleOrDefaultAsync<User>(sql, new { Email = email, Password = password });

	if (user != null)
	{
		try
		{
			request.HttpContext.Session.SetInt32("userId", (int)user.ID);
			Console.WriteLine($"Session userId set to {(int)user.ID}"); //new
			return Results.Text(@"<script>window.location.href = '#addFeed-section';</script>", "text/html");
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

    Console.WriteLine($"UserId from session: {userId}"); 
    var sqlInsert = "INSERT INTO Feeds (UserId, Url) VALUES (@UserId, @Url)";
	await db.ExecuteAsync(sqlInsert, new { UserId = userId, Url = url });

	var sqlSelect = "SELECT * FROM Feeds WHERE UserId = @UserId";
	var feeds = await db.QueryAsync<Feed>(sqlSelect, new { UserId = userId });

	var feedsHtml = "";
	foreach (var feed in feeds)
	{
		feedsHtml += $"<li class='list-group-item d-flex justify-content-between align-items-center'>{feed.Url} " +
					 $"<button class='btn btn-danger btn-sm' onclick='removeFeed({feed.Id})'>Remove</button>" +
                     $"<button class='btn btn-primary btn-sm' onclick='toggleFeedRender({feed.Id} ,\"{feed.Url}\")'>Render</button></li>";
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

app.MapGet("/fetchFeedContent", async (HttpContext context) =>
{
    try
    {
        if (!context.Request.Query.TryGetValue("url", out var url))
            throw new ArgumentException("URL parameter is missing");

        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();

        HttpResponseMessage response = await httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            string xmlContent = await response.Content.ReadAsStringAsync();
            string formattedContent = ParseXmlToHtml(xmlContent); 
            return Results.Text(formattedContent, "text/html");
        }
        else
        {
            return Results.Text($"Failed to fetch RSS feed content. Status code: {response.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        return Results.Text($"Error fetching RSS feed content: {ex.Message}");
    }
});

static string ParseXmlToHtml(string xmlContent)
{
    try
    {
        XDocument doc = XDocument.Parse(xmlContent);

        var items = doc.Root.Descendants("item")
                            .Select(item => new
                            {
                                Title = item.Element("title")?.Value ?? "",
                                Description = item.Element("description")?.Value ?? "",
                                Link = item.Element("link")?.Value ?? ""
                            });

        if (items.Any())
        {
            var html = "<div>";
            foreach (var item in items)
            {
                html += $"<h3><a href='{item.Link}' target='_blank'>{item.Title}</a></h3>";
                html += $"<p>{item.Description}</p>";
                html += "<hr />";
            }
            html += "</div>";
            return html;
        }
        else
        {
            return "<p>No items found in the RSS feed.</p>";
        }
    }
    catch (Exception ex)
    {
		return $"<p style=\"color: red;\">Incorrect feed url </p>";
	}
}

app.MapPost("/removeFeed", async (HttpRequest request, IDbConnection db) =>
{
	var form = await request.ReadFormAsync();
	var feedId = int.Parse(form["feedId"]);
	var sql = "DELETE FROM Feeds WHERE Id = @Id";
	await db.ExecuteAsync(sql, new { Id = feedId });
	Console.WriteLine("feed removed successfully !!");
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

