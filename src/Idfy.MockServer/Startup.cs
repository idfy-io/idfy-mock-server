using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Idfy.MockServer
{
    public class Startup
    {
        private SwaggerDocument _swaggerDocument;

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Run(async (context) =>
            {
                if (_swaggerDocument == null)
                    _swaggerDocument = await GetSwaggerDoc();

                context.Response.ContentType = "application/json";
                
                if (!ValidateAuth(context.Request))
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                var mockResponse = MockGenerator.Generate(context.Request, _swaggerDocument);

                context.Response.StatusCode = mockResponse.StatusCode;
                
                if (mockResponse.ResponseBody != null)
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(mockResponse.ResponseBody));
            });
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
            if (!parts[1].StartsWith("eY")) return false;

            return true;
        }
        
        private async Task<SwaggerDocument> GetSwaggerDoc()
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync("https://idfyapimanagement.blob.core.windows.net/swagger-test/swagger.json");

                var content = await response.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<SwaggerDocument>(content,
                    new JsonSerializerSettings {MetadataPropertyHandling = MetadataPropertyHandling.Ignore});
            }
        }
    }
}