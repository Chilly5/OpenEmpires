using System;

namespace OpenEmpires
{
    public static class StrategicAIInterpreterFactory
    {
        public static IStrategicAIInterpreter Create(string preference = null,
            ICommanderHttpTransport transport = null)
        {
            preference = preference ?? Environment.GetEnvironmentVariable("OPENEMPIRES_COMMANDER_PROVIDER");
            if (string.Equals(preference, "mock", StringComparison.OrdinalIgnoreCase))
                return new MockStrategicAIProvider();
            if (!string.IsNullOrWhiteSpace(preference)
                && !string.Equals(preference, "gemini", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Unsupported strategic provider.", nameof(preference));
            string key = DotEnvLoader.Get(GeminiAIProvider.KeyEnvironmentVariable);
            if (string.Equals(preference, "gemini", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(key))
                return new GeminiStrategicAIProvider(key, transport);
            return new MockStrategicAIProvider();
        }
    }
}
