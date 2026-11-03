using System.Text.Json.Serialization;
using Voting.Api.Endpoints;
using Voting.Application;
using Voting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapPollEndpoints();

app.Run();

/// <summary>
/// Entry point, public so integration tests can host the API with <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
