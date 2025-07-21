using EconomySim.Protocols;
using Xunit;

namespace SchemaRegistryTests
{
    public class SchemaRegistryTests
    {
        [Fact]
        public void SchemasAreUpToDate()
        {
            SchemaRegistry.ValidateCurrentSchemas();
        }
    }
}
