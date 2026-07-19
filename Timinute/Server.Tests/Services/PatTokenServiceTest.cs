using System;
using Timinute.Server.Services.Pat;
using Xunit;

namespace Timinute.Server.Tests.Services
{
    public class PatTokenServiceTest
    {
        private readonly IPatTokenService svc = new PatTokenService();

        [Fact]
        public void Generate_Produces_Prefixed_Token_And_Matching_Hash()
        {
            var (plaintext, hash, prefix) = svc.Generate();

            Assert.StartsWith("tmn_pat_", plaintext);
            Assert.Equal(8, prefix.Length);
            Assert.False(prefix.StartsWith("tmn_pat_"));
            Assert.Equal(hash, svc.Hash(plaintext));           // hash is deterministic over the plaintext
            Assert.True(svc.FixedTimeEquals(hash, svc.Hash(plaintext)));
        }

        [Fact]
        public void Generate_Is_Unique()
        {
            var a = svc.Generate();
            var b = svc.Generate();
            Assert.NotEqual(a.plaintext, b.plaintext);
            Assert.NotEqual(a.hash, b.hash);
        }

        [Fact]
        public void FixedTimeEquals_Rejects_Different_Hashes()
        {
            Assert.False(svc.FixedTimeEquals(svc.Hash("tmn_pat_aaa"), svc.Hash("tmn_pat_bbb")));
        }
    }
}
