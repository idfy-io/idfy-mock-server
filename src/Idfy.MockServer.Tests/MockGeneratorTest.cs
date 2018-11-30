using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Idfy.MockServer.Tests
{
    [TestFixture]
    public class MockGeneratorTest
    {
        private SwaggerDocument _swaggerDocument;
        
        [OneTimeSetUp]
        public void Init()
        {
            _swaggerDocument = JsonConvert.DeserializeObject<SwaggerDocument>(
                File.ReadAllText("resources/idfy-swagger.json"), new JsonSerializerSettings()
                {
                    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
                });
        }

        [Test]
        public void ReturnsCorrectStatusCodeAndBody()
        {
            var request = MockRequest("/notification/webhooks/123", "GET");
            var mockResponse = MockGenerator.Generate(request, _swaggerDocument);
            
            Assert.IsNotNull(mockResponse);
            Assert.IsNotNull(mockResponse.ResponseBody);
            Assert.AreEqual(200, mockResponse.StatusCode);

            var jObj = JObject.FromObject(mockResponse.ResponseBody);
            
            Assert.AreEqual(1, jObj["id"].Value<int>());
            Assert.AreEqual("My webhook", jObj["name"].Value<string>());
        }
        
        [Test]
        public void ReturnsNotFoundOnInvalidPath()
        {
            var request = MockRequest("/foo/bar", "GET");
            var mockResponse = MockGenerator.Generate(request, _swaggerDocument);
            
            Assert.IsNotNull(mockResponse);
            Assert.AreEqual(404, mockResponse.StatusCode);
        }

        [Test]
        public void PrioritizesExactRouteMatch()
        {
            // Sometimes, the request path will match multiple paths in our Swagger definition.
            // For example, the request path "/documents/summary" matches both the "/documents/summary"
            // and "/documents/{documentId}" paths (we don't have type constraints on path params, so "summary" could technically be
            // a document ID. Therefore we should exact matches must have priority over paths with params.
            var request = MockRequest("/signature/documents/summary", "GET");
            var mockResponse = MockGenerator.Generate(request, _swaggerDocument);
            
            Assert.IsNotNull(mockResponse);
            Assert.AreEqual(200, mockResponse.StatusCode);

            // Ensure that the response contains the list of document summaries
            var jObj = JObject.FromObject(mockResponse.ResponseBody);
            Assert.IsNotEmpty(jObj["data"].Value<IEnumerable<object>>());
        }

        public HttpRequest MockRequest(string path, string method)
        {
            var context = new DefaultHttpContext();
            return new DefaultHttpRequest(context)
            {
                Path = new PathString(path),
                Method = method
            };
        } 
    }
}