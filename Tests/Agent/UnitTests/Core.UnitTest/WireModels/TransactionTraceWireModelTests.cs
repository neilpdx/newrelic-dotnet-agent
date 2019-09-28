﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
	[TestFixture]
	public class TransactionTraceWireModelTests
	{
		[Test]
		public void TransactionSampleDataSerializesCorrectly()
		{
			// Arrange
			const string expected = @"[1514768400000,1000.0,""Transaction Name"",""Transaction URI"",[1514768400000,{},{},[0.0,1000.0,""Segment Name"",{},[],""Segment Class Name"",""Segment Method Name""],{""agentAttributes"":{},""userAttributes"":{},""intrinsics"":{}}],""Transaction GUID"",null,false,null,null]";
			var timestamp = new DateTime(2018,1,1,1,0,0, DateTimeKind.Utc);
			var transactionTraceSegment = new TransactionTraceSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Segment Name", new Dictionary<String, Object>(), new List<TransactionTraceSegment>(), "Segment Class Name", "Segment Method Name");
			var agentAttributes = new Dictionary<String, Object>();
			var intrinsicAttributes = new Dictionary<String, Object>();
			var userAttributes = new Dictionary<String, Object>();
			var transactionTrace = new TransactionTraceData(timestamp, transactionTraceSegment, agentAttributes, intrinsicAttributes, userAttributes);
			var transactionSample = new TransactionTraceWireModel(timestamp, TimeSpan.FromSeconds(1), "Transaction Name", "Transaction URI", transactionTrace, "Transaction GUID", null, null, false);

			// Act
			var actual = JsonConvert.SerializeObject(transactionSample);

			// Assert
			Assert.AreEqual(expected, actual);
		}
	}
}