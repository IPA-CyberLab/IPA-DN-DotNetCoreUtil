using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IPA.DN.CoreUtil.Basic;
using IPA.DN.CoreUtil.Helper.Basic;

namespace IPA.DN.CoreUtil.WebApi
{
    public class HttpServer
    {
        public class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            { 
            }

            public void Configure(IApplicationBuilder app)
            {
            }
        }

        public HttpServer()
        {
            var webhost = new WebHostBuilder()
                .UseKestrel(opt =>
                {
                    opt.ListenAnyIP(80);
                })
                .UseContentRoot(@"c:\tmp")
                .ConfigureAppConfiguration((hostingContext, config) =>
                {

                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .UseStartup<Startup>()
                .Build();

            webhost.Run();
        }
    }
}
