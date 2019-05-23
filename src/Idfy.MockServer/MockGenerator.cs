using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace Idfy.MockServer
{
    public static class MockGenerator
    {
        /// <summary>
        /// Generates a <see cref="MockResponse"/> based on a Swagger document.
        /// </summary>
        /// <param name="request">The incoming HTTP request.</param>
        /// <param name="swaggerDocument">The Swagger document.</param>
        /// <returns></returns>
        public static MockResponse Generate(HttpRequest request, SwaggerDocument swaggerDocument)
        {
            var op = GetOperation(request, swaggerDocument);
            
            // No operation matches the requested path, so we just return a 404.
            if (op?.responses == null) return NotFound();

            return GetMockResponse(op, swaggerDocument);
        }

        private static Operation GetOperation(HttpRequest request, SwaggerDocument swaggerDocument)
        {
            PathItem pathItem = null;
            
            // First try to match the exact path
            var exactPath = swaggerDocument.paths.FirstOrDefault(x => x.Key.Equals(request.Path));
            if (exactPath.Value != null)
            {
                pathItem = exactPath.Value;
            }
            else
            {
                // When there is no exact match, use RouteMatcher to find matching path
                foreach (var path in swaggerDocument.paths)
                {
                    try
                    {
                        if (!RouteMatcher.Match(path.Key, request.Path, request.Query)) continue;
                        
                        pathItem = path.Value;
                        break;
                    }
                    // The Swagger document might contain invalid paths that the RouteMatcher can't process, so we ignore those errors.
                    catch (ArgumentException)
                    {
                    }
                }   
            }
            
            if (pathItem == null) return null;

            switch (request.Method)
            {
                case "GET":
                    return pathItem.get;
                case "POST":
                    return pathItem.post;
                case "PATCH":
                    return pathItem.patch;
                case "PUT":
                    return pathItem.put;
                case "DELETE":
                    return pathItem.delete;
                default:
                    return null;
            }
        }

        private static MockResponse GetMockResponse(Operation operation, SwaggerDocument swaggerDocument)
        {
            // We don't really know which status code is expected from a successful operation,
            // so we just find the first response with a success status code and use that.
            var successCodes = new[] {"200", "201", "204"};
            foreach (var statusCode in successCodes)
            {
                if (!operation.responses.ContainsKey(statusCode)) continue;

                var response = operation.responses[statusCode];
                
                // Successful response found, so this is the status code we'll return in the mock response.
                var mockResponse = new MockResponse() { StatusCode = int.Parse(statusCode) };
                
                // Example responses can be defined inline or in an external definition referenced via $ref.
                // First we try to find an inline example response.
                if (response.examples != null)
                {
                    var examplesJObj = JObject.FromObject(response.examples);
                    if (examplesJObj.TryGetValue("application/json", out var example))
                    {
                        mockResponse.ResponseBody = example;
                        return mockResponse;
                    }
                }
                
                if (response.schema == null) return mockResponse;

                // If no inline example is found, try to find a referenced definition
                if (response.schema.@ref != null)
                {
                    mockResponse.ResponseBody = CreateResponseFromRef(response.schema.@ref, swaggerDocument);
                }
                
                // Operations that return a list of objects will instead have an "items" property with the ref
                else if (response.schema.items?.@ref != null)
                {
                    mockResponse.ResponseBody = new List<object>()
                    {
                        CreateResponseFromRef(response.schema.items.@ref, swaggerDocument)
                    };
                }
                
                // Enums can also be used to define a list of possible string values
                else if (response.schema.items?.@enum != null)
                {
                    mockResponse.ResponseBody = response.schema.items.@enum;
                }

                return mockResponse;
            }

            // We'll return 404 if we can't find any success responses for the given operation.
            return NotFound();
        }

        private static object CreateResponseFromRef(string @ref, SwaggerDocument swaggerDocument)
        {
            var modelName = @ref.Replace("#/definitions/", "");

            if (!swaggerDocument.definitions.TryGetValue(modelName, out var definition))
                return null;
                
            if (definition.example != null)
            {
                return definition.example;
            }

            // When we have a definition with no example response, the last resort is to create an object where
            // all properties are set to the types' default value.
            return CreateDefaultResponseObject(definition);
        }

        private static object CreateDefaultResponseObject(Schema definition)
        {
            var jObj = new JObject();
            foreach (var prop in definition.properties)
            {
                JToken value = null;
                
                // Set the value based on the property type. This checks for all the supported OpenAPI data types.
                switch (prop.Value.type)
                {
                    case "string":
                        var dateFormats = new[] { "date-time", "date" };
                        value = dateFormats.Contains(prop.Value.format)
                            ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                            : "string";
                        break;
                    case "number":
                    case "integer":
                        value = 0;
                        break;
                    case "boolean":
                        value = true;
                        break;
                    case "array":
                        value = new JArray();
                        break;
                    case "object":
                        value = null;
                        break;
                }
                
                jObj.Add(prop.Key, value);
            }

            return jObj.ToObject<object>();
        }

        private static MockResponse NotFound() => new MockResponse() { StatusCode = 404 };
    }

    public class MockResponse
    {
        public int StatusCode { get; set; }
        
        public object ResponseBody { get; set; }
    }
}