using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Idfy.MockServer.Tests
{
    [TestFixture]
    public class RouteMatcherTest
    {
        [Test]
        public void MatchesBasicRoute()
        {
            var routeTemplate = "/foo/bar";
            var requestPath = "/foo/bar";
            var query = new QueryCollection();
            
            var isMatch = RouteMatcher.Match(routeTemplate, requestPath, query);
            
            Assert.IsTrue(isMatch);
        }

        [Test]
        public void DoesNotMatchInvalidRoute()
        {
            var routeTemplate = "/foo/bar";
            var requestPath = "/bar/foo";
            var query = new QueryCollection();
            
            var isMatch = RouteMatcher.Match(routeTemplate, requestPath, query);
            
            Assert.IsFalse(isMatch);
        }

        [Test]
        public void MatchesRouteWithPathParams()
        {
            var routeTemplate = "/foo/{id}/bar/{id2}";
            var requestPath = "/foo/123/bar/456";
            var query = new QueryCollection();
            
            var isMatch = RouteMatcher.Match(routeTemplate, requestPath, query);
            
            Assert.IsTrue(isMatch);
        }
    }
}