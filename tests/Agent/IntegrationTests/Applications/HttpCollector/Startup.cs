﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(HttpCollector.Startup))]
namespace HttpCollector
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
        }
    }
}
