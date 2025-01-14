// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Labels.Tests
{
    [TestFixture]
    public class LabelsServiceTests
    {
        [TestCase(null)]
        public void empty_collection(string labelsConfigurationString)
        {
            // arrange
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration.Labels).Returns(labelsConfigurationString);

            // act
            var labelsService = new LabelsService(configurationService);

            // assert
            CollectionAssert.IsEmpty(labelsService.Labels);
        }

        public class TestCase
        {
            [JsonProperty(PropertyName = "name")]
            public readonly string Name;
            [JsonProperty(PropertyName = "labelString")]
            public readonly string LabelConfigurationString;
            [JsonProperty(PropertyName = "warning")]
            public readonly bool Warning;
            [JsonProperty(PropertyName = "expected")]
            public readonly IEnumerable<Label> Expected;

            public class Label
            {
                [JsonProperty(PropertyName = "label_type")]
                public readonly string LabelType;
                [JsonProperty(PropertyName = "label_value")]
                public readonly string LabelValue;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public static IEnumerable<TestCase> CrossAgentTestCases
        {
            get
            {
                #region testCasesJson

                const string testCasesJson = @"[
  {
    ""name"":        ""empty"",
    ""labelString"": """",
    ""warning"":     false,
    ""expected"":    []
  },
  {
    ""name"":        ""multiple_values"",
    ""labelString"": ""Data Center: East;Data Center :West; Server : North;Server:South; "",
    ""warning"":     false,
    ""expected"":    [
        { ""label_type"": ""Data Center"", ""label_value"": ""West"" },
        { ""label_type"": ""Server"", ""label_value"": ""South"" }
    ]
  },
  {
    ""name"":        ""multiple_labels_with_leading_and_trailing_whitespaces"",
    ""labelString"": ""   Data Center   : East Coast  ;   Deployment Flavor    :  Integration Environment   "",
    ""warning"":     false,
    ""expected"":    [
        { ""label_type"": ""Data Center"", ""label_value"": ""East Coast"" },
        { ""label_type"": ""Deployment Flavor"", ""label_value"": ""Integration Environment"" }
    ]
  },
  {
    ""name"":        ""single"",
    ""labelString"": ""Server:East"",
    ""warning"":     false,
    ""expected"":    [ { ""label_type"": ""Server"", ""label_value"": ""East"" } ]
  },
  {
    ""name"":        ""single_label_with_leading_and_trailing_whitespaces"",
    ""labelString"": ""   Data Center   : East Coast "",
    ""warning"":     false,
    ""expected"":    [ { ""label_type"": ""Data Center"", ""label_value"": ""East Coast"" } ]
  },
  {
    ""name"":        ""single_trailing_semicolon"",
    ""labelString"": ""Server:East;"",
    ""warning"":     false,
    ""expected"":    [ { ""label_type"": ""Server"", ""label_value"": ""East"" } ]
  },
  {
    ""name"":        ""pair"",
    ""labelString"": ""Data Center:Primary;Server:East"",
    ""warning"":     false,
    ""expected"":    [
      { ""label_type"": ""Data Center"", ""label_value"": ""Primary"" },
      { ""label_type"": ""Server"",      ""label_value"": ""East"" }
    ]
  },
  {
    ""name"":        ""truncation"",
    ""labelString"": ""KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK:VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV"",
    ""warning"":     true,
    ""expected"":    [ {
      ""label_type"":  ""KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK"",
      ""label_value"": ""VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV""
    } ]
  },
  {
    ""name"":        ""single_label_key_to_be_truncated_with_leading_and_trailing_whitespaces"",
    ""labelString"": ""           123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345TTTTT       :value"",
    ""warning"":     true,
    ""expected"":    [
        { ""label_type"": ""123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345"", ""label_value"": ""value"" }
    ]
  },
  {
    ""name"":        ""single_label_value_to_be_truncated_with_leading_and_trailing_whitespaces"",
    ""labelString"": ""key:           123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345TTTTT       "",
    ""warning"":     true,
    ""expected"":    [
        { ""label_type"": ""key"", ""label_value"": ""123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345"" }
    ]
  },
  {
    ""name"":        ""utf8"",
    ""labelString"": ""kéÿ:vãlüê"",
    ""warning"":     false,
    ""expected"":    [
        { ""label_type"": ""kéÿ"", ""label_value"": ""vãlüê"" }
    ]
  },
  {
    ""name"":        ""failed_no_delimiters"",
    ""labelString"": ""Server"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""failed_no_delimiter"",
    ""labelString"": ""ServerNorth;"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""failed_too_many_delimiters"",
    ""labelString"": ""Server:North:South;"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""failed_no_value"",
    ""labelString"": ""Server:   "",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""failed_no_key"",
    ""labelString"": "":North"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""failed_no_delimiter_in_later_pair"",
    ""labelString"": ""Server:North;South;"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""so_many_labels"",
    ""labelString"": ""0:0;1:1;2:2;3:3;4:4;5:5;6:6;7:7;8:8;9:9;10:10;11:11;12:12;13:13;14:14;15:15;16:16;17:17;18:18;19:19;20:20;21:21;22:22;23:23;24:24;25:25;26:26;27:27;28:28;29:29;30:30;31:31;32:32;33:33;34:34;35:35;36:36;37:37;38:38;39:39;40:40;41:41;42:42;43:43;44:44;45:45;46:46;47:47;48:48;49:49;50:50;51:51;52:52;53:53;54:54;55:55;56:56;57:57;58:58;59:59;60:60;61:61;62:62;63:63;64:64;65:65;66:66;67:67;68:68;69:69;70:70;71:71;72:72;73:73;74:74;75:75;76:76;77:77;78:78;79:79;80:80;81:81;82:82;83:83;84:84;85:85;86:86;87:87;88:88;89:89;90:90;91:91;92:92;93:93;94:94;95:95;96:96;97:97;98:98;99:99;"",
    ""warning"":     true,
    ""expected"":    [
      { ""label_type"": ""0"", ""label_value"": ""0"" },   { ""label_type"": ""1"", ""label_value"": ""1"" },   { ""label_type"": ""2"", ""label_value"": ""2"" },   { ""label_type"": ""3"", ""label_value"": ""3"" },   { ""label_type"": ""4"", ""label_value"": ""4"" },
      { ""label_type"": ""5"", ""label_value"": ""5"" },   { ""label_type"": ""6"", ""label_value"": ""6"" },   { ""label_type"": ""7"", ""label_value"": ""7"" },   { ""label_type"": ""8"", ""label_value"": ""8"" },   { ""label_type"": ""9"", ""label_value"": ""9"" },
      { ""label_type"": ""10"", ""label_value"": ""10"" }, { ""label_type"": ""11"", ""label_value"": ""11"" }, { ""label_type"": ""12"", ""label_value"": ""12"" }, { ""label_type"": ""13"", ""label_value"": ""13"" }, { ""label_type"": ""14"", ""label_value"": ""14"" },
      { ""label_type"": ""15"", ""label_value"": ""15"" }, { ""label_type"": ""16"", ""label_value"": ""16"" }, { ""label_type"": ""17"", ""label_value"": ""17"" }, { ""label_type"": ""18"", ""label_value"": ""18"" }, { ""label_type"": ""19"", ""label_value"": ""19"" },
      { ""label_type"": ""20"", ""label_value"": ""20"" }, { ""label_type"": ""21"", ""label_value"": ""21"" }, { ""label_type"": ""22"", ""label_value"": ""22"" }, { ""label_type"": ""23"", ""label_value"": ""23"" }, { ""label_type"": ""24"", ""label_value"": ""24"" },
      { ""label_type"": ""25"", ""label_value"": ""25"" }, { ""label_type"": ""26"", ""label_value"": ""26"" }, { ""label_type"": ""27"", ""label_value"": ""27"" }, { ""label_type"": ""28"", ""label_value"": ""28"" }, { ""label_type"": ""29"", ""label_value"": ""29"" },
      { ""label_type"": ""30"", ""label_value"": ""30"" }, { ""label_type"": ""31"", ""label_value"": ""31"" }, { ""label_type"": ""32"", ""label_value"": ""32"" }, { ""label_type"": ""33"", ""label_value"": ""33"" }, { ""label_type"": ""34"", ""label_value"": ""34"" },
      { ""label_type"": ""35"", ""label_value"": ""35"" }, { ""label_type"": ""36"", ""label_value"": ""36"" }, { ""label_type"": ""37"", ""label_value"": ""37"" }, { ""label_type"": ""38"", ""label_value"": ""38"" }, { ""label_type"": ""39"", ""label_value"": ""39"" },
      { ""label_type"": ""40"", ""label_value"": ""40"" }, { ""label_type"": ""41"", ""label_value"": ""41"" }, { ""label_type"": ""42"", ""label_value"": ""42"" }, { ""label_type"": ""43"", ""label_value"": ""43"" }, { ""label_type"": ""44"", ""label_value"": ""44"" },
      { ""label_type"": ""45"", ""label_value"": ""45"" }, { ""label_type"": ""46"", ""label_value"": ""46"" }, { ""label_type"": ""47"", ""label_value"": ""47"" }, { ""label_type"": ""48"", ""label_value"": ""48"" }, { ""label_type"": ""49"", ""label_value"": ""49"" },
      { ""label_type"": ""50"", ""label_value"": ""50"" }, { ""label_type"": ""51"", ""label_value"": ""51"" }, { ""label_type"": ""52"", ""label_value"": ""52"" }, { ""label_type"": ""53"", ""label_value"": ""53"" }, { ""label_type"": ""54"", ""label_value"": ""54"" },
      { ""label_type"": ""55"", ""label_value"": ""55"" }, { ""label_type"": ""56"", ""label_value"": ""56"" }, { ""label_type"": ""57"", ""label_value"": ""57"" }, { ""label_type"": ""58"", ""label_value"": ""58"" }, { ""label_type"": ""59"", ""label_value"": ""59"" },
      { ""label_type"": ""60"", ""label_value"": ""60"" }, { ""label_type"": ""61"", ""label_value"": ""61"" }, { ""label_type"": ""62"", ""label_value"": ""62"" }, { ""label_type"": ""63"", ""label_value"": ""63"" } ]
  },
  {
    ""name"":        ""trailing_semicolons"",
    ""labelString"": ""foo:bar;;"",
    ""warning"":     false,
    ""expected"":    [ { ""label_type"": ""foo"", ""label_value"": ""bar"" } ]
  },
  {
    ""name"":        ""leading_semicolons"",
    ""labelString"": "";;foo:bar"",
    ""warning"":     false,
    ""expected"":    [ { ""label_type"": ""foo"", ""label_value"": ""bar"" } ]
  },
  {
    ""name"":        ""empty_label"",
    ""labelString"": ""foo:bar;;zip:zap"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""trailing_colons"",
    ""labelString"": ""foo:bar;:"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""leading_colons"",
    ""labelString"": "":;foo:bar"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""empty_pair"",
    ""labelString"": "" : "",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"":        ""empty_pair_in_middle_of_string"",
    ""labelString"": ""foo:bar; : ;zip:zap"",
    ""warning"":     true,
    ""expected"":    []
  },
  {
    ""name"": ""long_multibyte_utf8"",
    ""labelString"": ""foo:€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€"",
    ""warning"": true,
    ""expected"": [ { ""label_type"": ""foo"", ""label_value"": ""€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€€"" } ]
  },
  {
    ""name"": ""long_4byte_utf8"",
    ""labelString"": ""foo:𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆"",
    ""warning"": true,
    ""expected"": [ { ""label_type"": ""foo"", ""label_value"": ""𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆𝌆""}]
  }
]";

                #endregion

                var testCases = JsonConvert.DeserializeObject<IEnumerable<TestCase>>(testCasesJson);
                Assert.NotNull(testCases);
                return testCases
                    .Where(testCase => testCase != null)
                    .ToArray();
            }
        }

       [TestCaseSource(nameof(CrossAgentTestCases))]
        public void cross_agent_tests(TestCase testCase)
        {
            using (var logger = new TestUtilities.Logging())
            {

                // arrange
                var configurationService = Mock.Create<IConfigurationService>();
                Mock.Arrange(() => configurationService.Configuration.Labels)
                    .Returns(testCase.LabelConfigurationString);

                // act
                var labelsService = new LabelsService(configurationService);
                var actualResults = JsonConvert.SerializeObject(labelsService.Labels);
                var expectedResults = JsonConvert.SerializeObject(testCase.Expected);

                // assert
                Assert.AreEqual(expectedResults, actualResults);
                if (testCase.Warning)
                    Assert.AreNotEqual(0, logger.WarnCount);
                else
                    Assert.AreEqual(0, logger.MessageCount);
            }
        }
    }
}
