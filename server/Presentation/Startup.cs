﻿using Aggregates;
using NServiceBus;
using NServiceBus.Features;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.Text;
using ServiceStack.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using ServiceStack.Redis;
using ServiceStack.Auth;
using System.Threading.Tasks;
using LogManager = ServiceStack.Logging.LogManager;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore;
using Serilog;
using App.Metrics.Health;
using StructureMap;
using App.Metrics.Health.Builder;
using ServiceStack.Api.OpenApi;

namespace Example
{
    class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }

    class Startup
    {
        IConfiguration Configuration { get; set; }
        public Startup(IConfiguration configuration) => Configuration = configuration;

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseServiceStack(new AppHost
            {
                AppSettings = new NetCoreAppSettings(Configuration)
            });
        }
    }

    class AppHost : AppHostBase
    {
        private static StructureMap.IContainer _container;
        private static IEndpointInstance _bus;

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal("<{EventId:l}> Unhandled exception {Exception}", "Unhandled", e.ExceptionObject);
            Console.WriteLine("");
            Console.WriteLine("FATAL ERROR - Press return to close...");
            Console.ReadLine();
            Environment.Exit(1);
        }

        //Tell Service Stack the name of your application and where to find your web services
        public AppHost()
            : base("Api", typeof(AppHost).Assembly)
        {
        }

        public override ServiceStackHost Init()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
               .WriteTo.Console(outputTemplate: "[{Level}] {Message}{NewLine}{Exception}")
               .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            _container = new Container(x =>
            {
                x.Scan(y =>
                {
                    y.TheCallingAssembly();

                    y.WithDefaultConventions();
                });
            });

            MutationManager.RegisterMutator("commands", typeof(Mutator));

            _bus = InitBus().Result;

            return base.Init();
        }
        private async Task<IEndpointInstance> InitBus()
        {
            var config = new EndpointConfiguration("domain");

            config.UsePersistence<InMemoryPersistence>();
            config.UseContainer<StructureMapBuilder>(c => c.ExistingContainer(_container));

            config.Pipeline.Remove("LogErrorOnInvalidLicense");

            await Aggregates.Configuration.Build(c => c
                .StructureMap(_container)
                .NewtonsoftJson()
                .NServiceBus(config)
                .SetUniqueAddress(Defaults.Instance.ToString())
                .SetPassive()
                .SetRetries(20)
            ).ConfigureAwait(false);

            return Aggregates.Bus.Instance;
        }

        public override void Configure(Funq.Container container)
        {
            JsConfig.IncludeNullValues = true;
            JsConfig.AlwaysUseUtc = true;
            JsConfig.AssumeUtc = true;
            JsConfig.TreatEnumAsInteger = true;
            JsConfig.DateHandler = DateHandler.ISO8601;

            ServiceExceptionHandlers.Add((httpReq, request, exception) =>
            {
                Log.Logger.Error(exception, "Service exception {ExceptionType} - {ExceptionMessage} on request {Request}", exception.GetType().Name, exception.Message, httpReq);
                return null;
            });

            UncaughtExceptionHandlers.Add((req, res, operationName, ex) =>
            {
                Log.Logger.Error(ex, "Unhandled exception {ExceptionType} - {ExceptionMessage}, operation {OperationName} on request {Request}", ex.GetType().Name, ex.Message, operationName, req);
            });

            // Todo: see: http://docs.servicestack.net/releases/v4.5.10#vulnerability-with-object-properties
            JsConfig.AllowRuntimeType = _ => true;

            SetConfig(new HostConfig { DebugMode = true, ApiVersion = "1", WebHostUrl = "http://127.0.0.1:8080" });

            container.Adapter = new StructureMapContainerAdapter(_container);
            
            Plugins.Add(new SessionFeature());
            Plugins.Add(new OpenApiFeature());
            //Plugins.Add(new NativeTypesFeature());

            // Todo
            //Plugins.Add(new RequestLogsFeature
            //{
            //    RequestLogger = container.Exists<IRedisClientsManager>() 
            //        ? new RedisNLogRequestLogger(container.Resolve<IRedisClientsManager>(), Logger, capacity: 1000) 
            //        : (IRequestLogger)new InMemoryNLogRequestLogger(Logger, capacity: 1000)
            //});

            Plugins.Add(new PostmanFeature
            {
                DefaultLabelFmt = new List<string> { "type: english", " ", "route" }
            });
            Plugins.Add(new CorsFeature(
                allowOriginWhitelist: new[]
                {
                    "http://localhost:9000"
                },
                allowCredentials: true,
                allowedHeaders: "Content-Type, Authorization",
                allowedMethods: "GET, POST, PUT, DELETE, OPTIONS"
            ));

            Plugins.Add(new ValidationFeature());
            // Todo
            //Plugins.Add(new Profiling.MetricsFeature());
            Plugins.Add(new ServerEventsFeature());
            
            var nativeTypes = this.GetPlugin<NativeTypesFeature>();
            nativeTypes.MetadataTypesConfig.GlobalNamespace = "DTOs";

        }
    }
}

