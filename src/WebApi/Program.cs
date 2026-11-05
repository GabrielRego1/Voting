using Application;
using Infrastructure;
using WebApi;
using WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddWebApi()
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler()
    .UseStatusCodePages();

app.MapPollEndpoints()
    .MapVoteEndpoints()
    .MapResultEndpoints();

app.Run();

/// <summary>
/// Entry point, public so integration tests can host the API with <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
