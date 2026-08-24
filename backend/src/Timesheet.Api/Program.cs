using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Application;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Infrastructure;
using Timesheet.Api.Middleware;

// Конвенции и сериализаторы Mongo должны быть зарегистрированы до первой сериализации.
MongoSetup.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));

// MongoClient потокобезопасен и держит пул соединений — он обязан быть синглтоном.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<MongoIndexes>();
builder.Services.AddSingleton<DataSeeder>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<PeriodService>();
builder.Services.AddScoped<TimeEntryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DictionaryService>();

builder.Services.AddControllers();

// Ошибки привязки модели тоже должны приходить в общем формате, а не в ProblemDetails.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiValidationError
            {
                Field = entry.Key,
                Message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Некорректное значение"
                    : error.ErrorMessage
            }))
            .ToList();

        return new BadRequestObjectResult(new ApiErrorResponse
        {
            Code = ErrorCodes.ValidationFailed,
            Message = "Проверьте заполненные поля",
            Errors = errors,
            TraceId = context.HttpContext.TraceIdentifier
        });
    };
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Учёт трудозатрат",
        Version = "v1",
        Description = "Табель и стоимость работ по проектам"
    });

    // DateOnly в схеме — обычная строка с датой.
    options.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    options.MapType<DateOnly?>(() => new OpenApiSchema { Type = "string", Format = "date", Nullable = true });
});

var app = builder.Build();

// Один и тот же образ умеет и обслуживать API, и наполнять базу: `dotnet Timesheet.Api.dll --seed`.
var seedOnly = args.Any(argument => argument is "--seed" or "seed");

await WaitForMongoAsync(app.Services, app.Logger);
await app.Services.GetRequiredService<MongoIndexes>().EnsureAsync();

if (seedOnly || app.Configuration.GetValue<bool>("SeedOnStart"))
{
    await app.Services.GetRequiredService<DataSeeder>().SeedAsync();

    if (seedOnly)
    {
        return;
    }
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Учёт трудозатрат v1"));

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Контейнер с API стартует быстрее, чем MongoDB успевает принять соединение,
// поэтому ждём базу явно, а не падаем на первом запросе.
static async Task WaitForMongoAsync(IServiceProvider services, ILogger logger)
{
    const int maxAttempts = 30;
    var context = services.GetRequiredService<MongoContext>();

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            logger.LogInformation("MongoDB доступна");
            return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                "MongoDB пока недоступна (попытка {Attempt} из {MaxAttempts}): {Message}",
                attempt,
                maxAttempts,
                exception.Message);

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
