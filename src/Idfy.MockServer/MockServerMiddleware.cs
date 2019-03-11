using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Idfy.MockServer
{
    public class MockServerMiddleware
    {
        private readonly RequestDelegate _next;
        private SwaggerDocument _swaggerDocument;

        public MockServerMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_swaggerDocument == null)
                _swaggerDocument = await GetSwaggerDoc();
            
            // For requests toward the OAuth token endpoint we simply return a mocked token response
            if (IsTokenRequest(context.Request))
            {
                await CreateResponse(context, 200, OAuthToken);
                return;
            }
            
            if (!ValidateAuth(context.Request))
            {
                await CreateResponse(context, 401);
                return;
            }

            var mockResponse = MockGenerator.Generate(context.Request, _swaggerDocument);
            await CreateResponse(context, mockResponse.StatusCode, mockResponse.ResponseBody);
        }

        private bool IsTokenRequest(HttpRequest request)
        {
            return request.Path.Equals("/oauth/connect/token") && request.Method == "POST";
        }
        
        private bool ValidateAuth(HttpRequest request)
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                return false;

            var value = authHeader.FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Expect any JWT Bearer token
            var parts = value.Split(" ");
            if (parts.Length != 2) return false;

            if (!parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase)) return false;
            if (!parts[1].StartsWith("ey", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }
        
        private async Task<SwaggerDocument> GetSwaggerDoc()
        {
            using (var client = new HttpClient())
            {
                var url = Environment.GetEnvironmentVariable("IDFY_SWAGGER_URL") ??
                          "https://idfyapimanagement.blob.core.windows.net/swagger-prod/swagger.json";

                HttpResponseMessage response;

                try
                {
                    response = await client.GetAsync(url);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to retrieve Swagger document from {url}", e);;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                
                return JsonConvert.DeserializeObject<SwaggerDocument>(content,
                    new JsonSerializerSettings {MetadataPropertyHandling = MetadataPropertyHandling.Ignore});
            }
        }

        private async Task CreateResponse(HttpContext context, int statusCode, object responseBody = null)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            
            if (responseBody != null)
                await context.Response.WriteAsync(JsonConvert.SerializeObject(responseBody));
        }

        private object OAuthToken => new
        {
            access_token =
                "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJmb28iOiJiYXIifQ.UIZchxQD36xuhacrJF9HQ5SIUxH5HBiv9noESAacsxU",
            expires_in = 3600,
            token_type = "Bearer"
        };
    }

    public static class MockServerMiddlewareExtensions
    {
        public static IApplicationBuilder UseIdfyMockServer(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MockServerMiddleware>();
        }
    }
}