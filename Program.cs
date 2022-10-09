// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace DiscordAudioTests;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var startup = new Startup(hostContext.HostingEnvironment, hostContext.Configuration);

                startup.ConfigureServices(services);
            });
    }
}
