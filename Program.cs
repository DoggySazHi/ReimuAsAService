using ReimuAsAService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IInvokeAIController, InvokeAIController>();
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();