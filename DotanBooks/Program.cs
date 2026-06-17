using Microsoft.EntityFrameworkCore;
using Repository;
using AutoMapper;
using DTOs;
using Microsoft.Extensions.DependencyInjection;
using Service;
using DotanBooks.Middlewares;
using NLog;
using NLog.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using Service.Caching;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();


    builder.Services.AddScoped<IGetByCategoriesService, GetByCategoriesService>();
    builder.Services.AddScoped<IGetByCategoriesRepository, GetByCategoriesRepository>();
    builder.Services.AddScoped<ISearchBookService, SearchBookService>();
    builder.Services.AddScoped<ISearchBookRepository, SearchBookRepository>();
    builder.Services.AddScoped<IBookByIdService, BookByIdService>();
    builder.Services.AddScoped<IBookByIdRepository, BookByIdRepository>();
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IPromotionService, PromotionService>();
    builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
    builder.Services.AddScoped<IAuthorService, AuthorService>();
    builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();
    builder.Services.AddScoped<IManagementBookService, ManagementBookService>();
    builder.Services.AddScoped<IManagementBookRepository, ManagementBookRepository>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IRatingService, RatingService>();
    builder.Services.AddScoped<IRatingRepository, RatingRepository>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IChatRepository, ChatRepository>();
    builder.Services.AddScoped<IChatService, ChatService>();


    builder.Services.AddDbContext<StoreContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    //builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("Redis"));
    //builder.Services.AddStackExchangeRedisCache(options =>
    //{
    //    var redisHost = builder.Configuration["Redis:Host"] ?? "localhost";
    //    var redisPort = builder.Configuration["Redis:Port"] ?? "6379";
    //    var redisPassword = builder.Configuration["Redis:Password"];

    //    var connectionString = string.IsNullOrWhiteSpace(redisPassword)
    //        ? $"{redisHost}:{redisPort},abortConnect=false"
    //        : $"{redisHost}:{redisPort},password={redisPassword},abortConnect=false";

    //    options.Configuration = connectionString;
    //    options.InstanceName = "DotanBooks:";
    //});
    builder.Services.AddDistributedMemoryCache();

    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();
    builder.Services.AddAutoMapper(_ => { }, typeof(MappingProfiles).Assembly);

    builder.Services.AddHttpClient();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:4200" };

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngular",
            policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
            });
    });

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSettings["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");
    var key = Encoding.ASCII.GetBytes(jwtKey);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            ClockSkew = TimeSpan.Zero // מבטל את הדיליי המובנה של 5 דקות פקיעת תוקף
        };
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"global:{clientIp}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        options.AddPolicy("LoginPolicy", httpContext =>
        {
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"login:{clientIp}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            var retryAfterSeconds = 10;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            }

            context.HttpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

            if (!context.HttpContext.Response.HasStarted)
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "יותר מדי בקשות. נסה שוב בעוד מספר שניות." },
                    cancellationToken: cancellationToken);
            }
        };
    });

    var app = builder.Build();

    // Retry migrations to handle SQL Server warm-up in containerized startup.
    const int maxMigrationRetries = 10;
    var migrationDelay = TimeSpan.FromSeconds(5);
    for (var attempt = 1; attempt <= maxMigrationRetries; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StoreContext>();
            dbContext.Database.Migrate();
            break;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            logger.Warn(ex, "Database migration attempt {Attempt} failed.", attempt);
            if (attempt == maxMigrationRetries)
            {
                throw;
            }

            await Task.Delay(migrationDelay);
        }
    }

    app.UseMiddleware<RatingMiddleware>();
    app.UseMiddleware<ExceptionMiddleware>();//ההזרקה של במידלוור של ה 404 
                                             // Configure the HTTP request pipeline.

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger(); 
        app.UseSwaggerUI(); 
    }

    var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", !app.Environment.IsDevelopment());
    if (enableHttpsRedirection)
    {
        app.UseExceptionHandler("/error");
app.UseHttpsRedirection();
    }


    app.UseStaticFiles(new StaticFileOptions
    {
        ServeUnknownFileTypes = true
    });

    app.UseCors("AllowAngular");
    app.UseRateLimiter();

    app.UseMiddleware<BlockedUserMiddleware>();
    app.UseMiddleware<CookieToAuthHeaderMiddleware>();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}