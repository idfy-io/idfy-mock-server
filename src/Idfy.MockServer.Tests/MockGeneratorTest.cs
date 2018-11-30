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