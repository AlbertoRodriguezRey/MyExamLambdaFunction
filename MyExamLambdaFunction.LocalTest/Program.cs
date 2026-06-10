using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using MyExamLambdaFunction;

Console.WriteLine("Dime tu pregunta sobre los conciertos:");
string pregunta = Console.ReadLine() ?? string.Empty;

Function function = new();
TestLambdaContext context = new();

APIGatewayHttpApiV2ProxyRequest request = new()
{
    Body = JsonSerializer.Serialize(new ChatRequest { Question = pregunta })
};

APIGatewayHttpApiV2ProxyResponse response = await function.FunctionHandler(request, context);

Console.WriteLine();
Console.WriteLine($"StatusCode: {response.StatusCode}");
Console.WriteLine("Body:");
Console.WriteLine(response.Body);
