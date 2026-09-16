using GraphRoots.GraphApi;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGraphRootsStore(builder.Configuration);
builder.Services.AddGraphRootsGraphQL();

var app = builder.Build();
app.MapGraphQL();
app.Run();

public partial class Program;
