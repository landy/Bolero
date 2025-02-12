// $begin{copyright}
//
// This file is part of Bolero
//
// Copyright (c) 2018 IntelliFactory and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace Bolero.Tests.Remoting

open System
open System.Text.Json.Serialization
open Bolero.Tests.Remoting.Client
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Bolero.Remoting.Server
open Bolero.Server
open FSharp.SystemTextJson.Swagger

module Page =
    open Bolero.Html
    open Bolero.Server.Html

    let index = doctypeHtml {
        head {
            title { "Bolero (remoting)" }
            meta { attr.charset "UTF-8" }
            ``base`` { attr.href "/" }
        }
        body {
            div { attr.id "main"; comp<Client.MyApp> }
            script { attr.src "_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js" }
            boleroScript
        }
    }

type MyApiHandler(log: ILogger<MyApiHandler>, ctx: IRemoteContext) =
    inherit RemoteHandler<Client.MyApi>()

    let mutable items = Map.empty

    override this.Handler =
        {
            getItems = fun () -> async {
                log.LogInformation("Getting items")
                return items
            }
            setItem = fun (k, v) -> async {
                log.LogInformation("Setting {0} => {1}", k, v)
                items <- Map.add k v items
            }
            removeItem = fun k -> async {
                log.LogInformation("Removing {0}", k)
                items <- Map.remove k items
            }
            login = fun login -> async {
                log.LogInformation("User logging in: {0}", login)
                return! ctx.HttpContext.AsyncSignIn(login, TimeSpan.FromDays(365. * 10.))
            }
            logout = fun () -> async {
                log.LogInformation("User logging out: {0}", ctx.HttpContext.User.Identity.Name)
                return! ctx.HttpContext.AsyncSignOut()
            }
            getLogin = ctx.Authorize <| fun () -> async {
                log.LogInformation("User getting their login: {0}", ctx.HttpContext.User.Identity.Name)
                return ctx.HttpContext.User.Identity.Name
            }
            authDouble = ctx.Authorize <| fun i -> async {
                log.LogInformation("User {0} doubling {1}", ctx.HttpContext.User.Identity.Name, i)
                return i * 2
            }
        }

type Startup() =

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddMvc() |> ignore
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            |> ignore
        services
            .AddBoleroRemoting<MyApiHandler>()
            .AddBoleroHost()
            .AddServerSideBlazor()
        |> ignore
        services.AddSwaggerForSystemTextJson(JsonFSharpOptions()) |> ignore
        services.AddEndpointsApiExplorer() |> ignore

    member this.Configure(app: IApplicationBuilder, env: IHostEnvironment, log: ILogger<Startup>) =
        app.UseAuthentication()
            .UseStaticFiles()
            .UseSwagger()
            .UseSwaggerUI()
            .UseRouting()
            .UseAuthorization()
            .UseBlazorFrameworkFiles()
            .UseEndpoints(fun endpoints ->
                endpoints.MapBlazorHub() |> ignore
                endpoints.MapBoleroRemoting()
                    .WithOpenApi()
                |> ignore
                endpoints.MapFallbackToBolero(Page.index) |> ignore)
        |> ignore

        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage()
                .UseWebAssemblyDebugging()

module Main =
    [<EntryPoint>]
    let Main args =
        WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build()
            .Run()
        0
