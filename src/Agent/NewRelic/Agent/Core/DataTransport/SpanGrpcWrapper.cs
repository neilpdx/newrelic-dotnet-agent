// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Segments;
using Grpc.Core;
using System.Threading;
using Grpc.Net.Client;
using System;

namespace NewRelic.Agent.Core.DataTransport
{
    public class SpanGrpcWrapper : GrpcWrapper<Span, RecordStatus>, IGrpcWrapper<Span, RecordStatus>
    {
        protected override AsyncDuplexStreamingCall<Span, RecordStatus> CreateStreamsImpl(GrpcChannel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            if (channel == null)
            {
                throw new GrpcWrapperChannelNotAvailableException();
            }

            var client = new IngestService.IngestServiceClient(channel);
            var streams = client.RecordSpan(headers: headers, cancellationToken: cancellationToken, deadline: DateTime.UtcNow.AddMilliseconds(connectTimeoutMs));

            return streams;
        }
    }

    public class SpanBatchGrpcWrapper : GrpcWrapper<SpanBatch, RecordStatus>, IGrpcWrapper<SpanBatch, RecordStatus>
    {
        protected override AsyncDuplexStreamingCall<SpanBatch, RecordStatus> CreateStreamsImpl(GrpcChannel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            if (channel == null)
            {
                throw new GrpcWrapperChannelNotAvailableException();
            }

            var client = new IngestService.IngestServiceClient(channel);
            var streams = client.RecordSpanBatch(headers: headers, cancellationToken: cancellationToken, deadline: DateTime.UtcNow.AddMilliseconds(connectTimeoutMs));

            return streams;
        }
    }

}
