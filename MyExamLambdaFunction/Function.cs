using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyExamLambdaFunction;

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
}

public class Function
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Punto de entrada para API Gateway (integración proxy): recibe la petición HTTP completa
    /// y devuelve una respuesta HTTP con el JSON de ChatResponse en el body.
    /// </summary>
    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        ChatResponse result;

        try
        {
            ChatRequest? input = JsonSerializer.Deserialize<ChatRequest>(request.Body ?? string.Empty, jsonOptions);
            result = await ProcessQuestionAsync(input?.Question ?? string.Empty, context);
        }
        catch (Exception ex)
        {
            result = new ChatResponse { Answer = $"Petición inválida: {ex.Message}" };
        }

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(result),
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            }
        };
    }

    private static async Task<ChatResponse> ProcessQuestionAsync(string question, ILambdaContext context)
    {
        // --- CONFIGURACIÓN DE SECRETS MANAGER ---
        string secretName = "eventos-azure-openai";
        string region = "us-east-1";

        IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

        GetSecretValueRequest secretRequest = new GetSecretValueRequest
        {
            SecretId = secretName,
            VersionStage = "AWSCURRENT",
        };

        GetSecretValueResponse response;
        string secretString;

        try
        {
            response = await client.GetSecretValueAsync(secretRequest);
            secretString = response.SecretString;
        }
        catch (Exception e)
        {
            context.Logger.LogError($"Error recuperando el secreto de AWS: {e.Message}");
            return new ChatResponse { Answer = $"Error de infraestructura (Secrets Manager): {e.Message}" };
        }
        // -------------------------------------------

        try
        {
            // Parseamos el JSON del secreto recuperado
            using var doc = JsonDocument.Parse(secretString);
            string apiKey = doc.RootElement.GetProperty("ApiKey").GetString()!;
            string baseUrl = doc.RootElement.GetProperty("urlPacoIa").GetString()!;

            // Construimos la URL del modelo gpt-4.1 en Azure AI Foundry
            string requestUrl = $"{baseUrl.TrimEnd('/')}/chat/completions";

            // Estructura del Payload para OpenAI
            var payload = new
            {
                model = "gpt-4.1",
                messages = new[]
                {
                    new { role = "user", content = question }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            // Configuración de la petición hacia Azure OpenAI
            var aiRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            aiRequest.Headers.Add("api-key", apiKey);
            aiRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage aiResponse = await httpClient.SendAsync(aiRequest);

            if (!aiResponse.IsSuccessStatusCode)
            {
                string errorDetails = await aiResponse.Content.ReadAsStringAsync();
                return new ChatResponse { Answer = $"Error en la llamada a Azure AI: {aiResponse.StatusCode} - {errorDetails}" };
            }

            // Extracción de la respuesta de la Inteligencia Artificial
            string jsonResponse = await aiResponse.Content.ReadAsStringAsync();
            using var responseDoc = JsonDocument.Parse(jsonResponse);

            string aiText = responseDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;

            return new ChatResponse { Answer = aiText };
        }
        catch (Exception ex)
        {
            return new ChatResponse { Answer = $"Fallo interno en el procesamiento de la Lambda: {ex.Message}" };
        }
    }
}
