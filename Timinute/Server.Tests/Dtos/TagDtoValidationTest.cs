using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Timinute.Shared.Dtos.Tag;
using Xunit;

namespace Timinute.Server.Tests.Dtos
{
    public class TagDtoValidationTest
    {
        [Fact]
        public void CreateTagDto_Whitespace_Name_Fails_Validation()
        {
            var dto = new CreateTagDto
            {
                Name = "   ",
                Color = "#123456"
            };

            var results = Validate(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTagDto.Name)));
        }

        [Fact]
        public void UpdateTagDto_Whitespace_Name_Fails_Validation()
        {
            var dto = new UpdateTagDto
            {
                TagId = "tag-1",
                Name = "   ",
                Color = "#123456"
            };

            var results = Validate(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTagDto.Name)));
        }

        [Fact]
        public void CreateTagDto_Valid_Name_Passes_Validation()
        {
            var dto = new CreateTagDto
            {
                Name = "Work",
                Color = "#123456"
            };

            var results = Validate(dto);

            Assert.Empty(results);
        }

        private static List<ValidationResult> Validate(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }
    }
}
