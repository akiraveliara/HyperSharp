using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace HyperSharp.SourceGeneration.Tasks
{
    public sealed class HyperHeaderCommonValuesGenerator : ITask
    {
        private const string CODE_TEMPLATE = """
// <auto-generated/>

namespace HyperSharp.Protocol
{
    /// <summary>
    /// Provides common values for HTTP header names.
    /// </summary>
    /// <remarks>
    /// This enum is NOT exhaustive. It only contains the most common header names.
    /// </remarks>
    public enum HyperHeaderName
    {
        {{HeaderName}}
    }
}

""";

        private const string ENUM_VALUE_TEMPLATE = """

        /// <summary>
        /// Represents the <c>{{HeaderValue}}</c> header.
        /// </summary>
        {{HeaderName}},
""";

        public bool Execute(IConfiguration configuration)
        {
            StringBuilder headerNames = new();
            Type headerNameType = typeof(HeaderNames);
            foreach (FieldInfo headerName in headerNameType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (headerName.GetCustomAttribute<ObsoleteAttribute>() is not null)
                {
                    continue;
                }

                headerNames.AppendLine(ENUM_VALUE_TEMPLATE
                    .Replace("{{HeaderName}}", headerName.Name)
                    .Replace("{{HeaderValue}}", headerName.GetRawConstantValue()!.ToString()
                ));
            }

            string code = CODE_TEMPLATE.Replace("{{HeaderName}}", headerNames.ToString().TrimStart().TrimEnd('\n', ','));
            string projectRoot = Directory.GetCurrentDirectory();
            File.WriteAllText(Path.Combine(projectRoot, "Protocol", "HyperHeaderName.g.cs"), code);

            return true;
        }
    }
}
