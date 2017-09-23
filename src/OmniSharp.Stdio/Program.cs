using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using OmniSharp.Stdio.Eventing;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.Eventing;

namespace OmniSharp.Stdio
{
    class Program
    {
        static int Main(string[] args) => HostHelpers.Start(() =>
        {
            var application = new StdioCommandLineApplication();
            application.OnExecute(() =>
            {
                // If an encoding was specified, be sure to set the Console with it before we access the input/output streams.
                // Otherwise, the streams will be created with the default encoding.
                if (application.Encoding != null)
                {
                    var encoding = Encoding.GetEncoding(application.Encoding);
                    Console.InputEncoding = encoding;
                    Console.OutputEncoding = encoding;
                }

                var environment = application.CreateEnvironment();
                var plugins = application.CreatePluginAssemblies();
                var configuration = new ConfigurationBuilder(environment).Build();
                var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(configuration);
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var cancellation = new CancellationTokenSource();

                if (application.Lsp)
                {
                    // TODO: Figure out how to change this around to use the lsp log messages
                    var writer = new SharedTextWriter(Console.Out);
                    var compositionHostBuilder = new CompositionHostBuilder(serviceProvider, environment, writer, NullEventEmitter.Instance)
                        .WithOmniSharpAssemblies()
                        .WithAssemblies(typeof(LanguageServerHost).Assembly);
                    using (var host = new LanguageServerHost(Console.OpenStandardInput(), Console.OpenStandardOutput(), environment, configuration, serviceProvider, compositionHostBuilder, loggerFactory, cancellation))
                    {
                        host.Start().Wait();
                        cancellation.Token.WaitHandle.WaitOne();
                    }
                }
                else
                {
                    var input = Console.In;
                    var output = Console.Out;

                    var writer = new SharedTextWriter(output);
                    var compositionHostBuilder = new CompositionHostBuilder(serviceProvider, environment, writer, new StdioEventEmitter(writer)).WithOmniSharpAssemblies();
                    using (var host = new Host(input, writer, environment, configuration, serviceProvider, compositionHostBuilder, loggerFactory, cancellation))
                    {
                        host.Start();
                        cancellation.Token.WaitHandle.WaitOne();
                    }
                }

                return 0;
            });

            return application.Execute(args);
        });
    }
}
