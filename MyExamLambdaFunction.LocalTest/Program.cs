using Amazon.Lambda.TestUtilities;
using MyExamLambdaFunction;

Console.WriteLine("Dime tu pregunta sobre los conciertos:");
string pregunta = Console.ReadLine() ?? string.Empty;

Function function = new();
TestLambdaContext context = new();
ChatResponse respuesta = await function.FunctionHandler(new ChatRequest { Question = pregunta }, context);

Console.WriteLine();
Console.WriteLine("Respuesta:");
Console.WriteLine(respuesta.Answer);
