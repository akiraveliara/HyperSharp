using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net;
using OoLunar.HyperSharp.Protocol;
using OoLunar.HyperSharp.Responders;
using OoLunar.HyperSharp.Tests.Responders;

namespace OoLunar.HyperSharp.Tests.Benchmarks
{
    [JsonExporterAttribute.Brief]
    public class AsyncResponderBenchmarks
    {
        private readonly HyperContext _context;

        public AsyncResponderBenchmarks()
        {
            ServiceProvider serviceProvider = Program.CreateServiceProvider();
            HyperConnection connection = new(new MemoryStream(), serviceProvider.GetRequiredService<HyperServer>());
            _context = new HyperContext(HttpMethod.Get, new Uri("http://localhost/"), HttpVersion.Version11, new(), connection);
        }

        [Benchmark]
        [ArgumentsSource(nameof(Data))]
        public async ValueTask DelegateExecutionTimeAsync(ValueTaskResponderDelegate<HyperContext, HyperStatus> responder) => await responder(_context, default);

        public static IEnumerable<ValueTaskResponderDelegate<HyperContext, HyperStatus>> Data()
        {
            yield return new HelloWorldValueTaskResponder().RespondAsync;

            ResponderCompiler compiler = new();
            compiler.Search(new[] { typeof(OkTaskResponder) });
            yield return compiler.CompileAsyncResponders<HyperContext, HyperStatus>(Program.CreateServiceProvider());

            compiler = new();
            compiler.Search(new[] { typeof(HelloWorldValueTaskResponder) });
            yield return compiler.CompileAsyncResponders<HyperContext, HyperStatus>(Program.CreateServiceProvider());

            compiler = new();
            compiler.Search(new[] { typeof(OkTaskResponder), typeof(HelloWorldValueTaskResponder) });
            yield return compiler.CompileAsyncResponders<HyperContext, HyperStatus>(Program.CreateServiceProvider());
        }
    }
}
