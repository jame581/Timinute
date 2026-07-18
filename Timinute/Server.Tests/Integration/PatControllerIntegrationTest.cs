using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Services.Pat;
using Timinute.Shared.Dtos.Pat;
using Xunit;

namespace Timinute.Server.Tests.Integration
{
    // Exercises the real pipeline (routing -> auth -> model binding -> controller)
    // for the PAT management surface. TestAuthHandler authenticates every request
    // as the seeded ApplicationUser1 (see TestAuthHandler.HandleAuthenticateAsync),
    // whose "sub" claim maps to Constants.Claims.UserId.
    [Collection("Integration")]
    public class PatControllerIntegrationTest : IClassFixture<TiminuteApiFactory>
    {
        private readonly TiminuteApiFactory factory;
        private readonly HttpClient client;

        public PatControllerIntegrationTest(TiminuteApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }

        [Fact]
        public async Task Create_Returns_Plaintext_Once_Then_List_Hides_It()
        {
            var create = await client.PostAsJsonAsync("/Pat", new CreatePatDto { Name = "cli", Scope = "read_write" });
            Assert.Equal(HttpStatusCode.OK, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<CreatedPatDto>();
            Assert.StartsWith("tmn_pat_", created!.Token);

            var list = await client.GetFromJsonAsync<PersonalAccessTokenDto[]>("/Pat");
            var row = list!.Single(t => t.Id == created.Id);
            Assert.Equal("cli", row.Name);
            Assert.Equal(created.Prefix, row.Prefix);
            Assert.Equal("read_write", row.Scope);
            // The DTO type has no Token/Hash member at all - secret never leaves after creation.
        }

        [Fact]
        public async Task Revoke_Removes_From_List_And_Second_Delete_Returns_404()
        {
            var created = await (await client.PostAsJsonAsync("/Pat", new CreatePatDto { Name = "temp", Scope = "read" }))
                .Content.ReadFromJsonAsync<CreatedPatDto>();

            var del = await client.DeleteAsync($"/Pat/{created!.Id}");
            Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

            var list = await client.GetFromJsonAsync<PersonalAccessTokenDto[]>("/Pat");
            Assert.DoesNotContain(list!, t => t.Id == created.Id);

            // Revoked != retrievable: a second revoke of the same id must not find it again.
            var secondDelete = await client.DeleteAsync($"/Pat/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
        }

        [Fact]
        public async Task List_Raw_Json_Contains_No_Secret_Material()
        {
            var created = await (await client.PostAsJsonAsync("/Pat", new CreatePatDto { Name = "secret-check", Scope = "read" }))
                .Content.ReadFromJsonAsync<CreatedPatDto>();

            // Compute the same hash the server stores, using the real IPatTokenService,
            // so we can assert the raw response body never contains it.
            var tokenService = factory.Services.GetRequiredService<IPatTokenService>();
            var expectedHash = tokenService.Hash(created!.Token);

            var rawBody = await client.GetStringAsync("/Pat");

            Assert.DoesNotContain(created.Token, rawBody);
            Assert.DoesNotContain(expectedHash, rawBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tokenhash", rawBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", rawBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Create_Normalizes_NonUtc_ExpiresAt_To_Utc()
        {
            var nonUtcExpiry = new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

            var created = await (await client.PostAsJsonAsync("/Pat", new CreatePatDto
            {
                Name = "expiring",
                Scope = "read",
                ExpiresAt = nonUtcExpiry
            })).Content.ReadFromJsonAsync<CreatedPatDto>();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.PersonalAccessTokens.SingleAsync(t => t.Id == created!.Id);

            Assert.Equal(TimeSpan.Zero, persisted.ExpiresAt!.Value.Offset);
            Assert.Equal(nonUtcExpiry, persisted.ExpiresAt.Value); // same instant, regardless of offset
        }

        [Fact]
        public async Task Revoke_Another_Users_Token_Returns_404()
        {
            // Seed a token owned by a different user directly through the DbContext -
            // TestAuthHandler only ever authenticates as ApplicationUser1, so the only
            // way to get a token owned by someone else into the store is to bypass the API.
            string otherUsersTokenId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var otherUsersToken = new PersonalAccessToken
                {
                    UserId = "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e", // seeded test2@email.com - not the authenticated user
                    Name = "someone-elses-token",
                    TokenHash = "irrelevant-hash-value",
                    Prefix = "irrelvnt",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.PersonalAccessTokens.Add(otherUsersToken);
                await db.SaveChangesAsync();
                otherUsersTokenId = otherUsersToken.Id;
            }

            var response = await client.DeleteAsync($"/Pat/{otherUsersTokenId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
