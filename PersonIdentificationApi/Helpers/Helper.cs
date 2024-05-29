namespace PersonIdentificationApi.Helpers
{
    internal static class Helper
    {
        private static IConfiguration configuration
            = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("local.settings.json").Build();

        public static string GetEnvironmentVariable(string variableName)
        {
            return configuration[variableName] ?? "";
        }
    }
}
