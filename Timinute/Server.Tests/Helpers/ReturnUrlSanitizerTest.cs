using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using Timinute.Server.Helpers;
using Xunit;

namespace Timinute.Server.Tests.Helpers
{
    // We trust the framework's IUrlHelper.IsLocalUrl to classify URLs; what we are
    // testing here is that our code calls it and branches on it correctly.
    public class ReturnUrlSanitizerTest
    {
        private static IUrlHelper UrlHelper(params string[] localUrls)
        {
            var mock = new Mock<IUrlHelper>();
            mock.Setup(u => u.Content("~/")).Returns("/");
            mock.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
                .Returns((string candidate) => Array.IndexOf(localUrls, candidate) >= 0);
            return mock.Object;
        }

        [Fact]
        public void Sanitize_Keeps_A_Local_Url()
        {
            var url = UrlHelper("/projects");

            Assert.Equal("/projects", ReturnUrlSanitizer.Sanitize(url, "/projects"));
        }

        [Theory]
        [InlineData("https://evil.example.com/steal")]
        [InlineData("//evil.example.com")]
        [InlineData("http://evil.example.com")]
        public void Sanitize_Rejects_A_Foreign_Url(string hostile)
        {
            // Only "/projects" is local; the hostile value is not.
            var url = UrlHelper("/projects");

            Assert.Equal("/", ReturnUrlSanitizer.Sanitize(url, hostile));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Sanitize_Falls_Back_To_Root_When_Absent(string? returnUrl)
        {
            var url = UrlHelper();

            Assert.Equal("/", ReturnUrlSanitizer.Sanitize(url, returnUrl));
        }
    }
}
