/*
Copyright (c) 2018, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace MatterHackers.MatterSlice.Tests
{
	[TestFixture, Category("MatterSlice.OutputTests")]
	public class OutputTestsGenerator
	{
		[Test]
		public void GenerateTests()
		{
			var sb = new StringBuilder();

			// Get default settings
			var config = new ConfigSettings();

			// Round trip through Json.net to drop readonly properties
			var configText = JsonConvert.SerializeObject(
				config,
				Formatting.Indented,
				new JsonSerializerSettings()
				{
					ContractResolver = new WritablePropertiesOnlyResolver()
				});

			var jObject = JsonConvert.DeserializeObject(configText) as JObject;

			foreach (var kvp in jObject)
			{
				string propertyName = kvp.Key;
				object propertyValue = kvp.Value;

				// Invert bools so generated tests evaluate non-default case
				switch (kvp.Value.Type)
				{
					case JTokenType.Boolean:
						propertyValue = !(bool)kvp.Value;
						break;

					case JTokenType.Integer:
						propertyValue = (int)kvp.Value + 3;
						break;

					case JTokenType.Float:
						propertyValue = string.Format("{0:#.###}", (float)kvp.Value + 0.1);
						break;

					case JTokenType.String:

						if (string.IsNullOrEmpty(kvp.Value.ToString()))
						{
							propertyValue = $"{propertyName} Text";
						}
						else
						{
							propertyValue = "XXXXXXXXX " + kvp.Value;
						}
						break;
				}

				switch (propertyName)
				{
					case "InfillType":
						propertyValue = ConfigConstants.INFILL_TYPE.HEXAGON;
						break;

					case "BooleanOperations":
					case "AdditionalArgsToProcess":
					case "ModelMatrix":
						propertyValue = "";
						break;

					case "SupportType":
						propertyValue = ConfigConstants.SUPPORT_TYPE.GRID;
						break;
				}

				if (kvp.Value.Type != JTokenType.Float)
				{
					propertyValue = JsonConvert.SerializeObject(propertyValue).Replace("\r\n", "\\n");
				}


				string outputLine = @"
		[Test]
		public void {0}Test()
		{{
			this.RunGCodeTest(""{0}"", stlPath, (settings) =>
			{{
				settings.{0} = {1}; {2}
			}});
		}}
";
				var requiresMoreEffort = new List<string>()
				{
					"BooleanOperations",
					// "BridgeFanSpeedPercent",
					"AdditionalArgsToProcess",
					"ModelMatrix"
				};

				if (requiresMoreEffort.Contains(propertyName))
				{
					outputLine = outputLine.Replace("\n\t\t\t", "\n\t\t\t//");
				}

				sb.AppendFormat(
					outputLine,
					propertyName,
					propertyValue,
					(kvp.Value.Type == JTokenType.String) ? "" : $"// Default({JsonConvert.SerializeObject(kvp.Value).Replace("\r\n", "")})");
			}

			// Copy sb value to clipboard, paste into test body
		}

		class WritablePropertiesOnlyResolver : DefaultContractResolver
		{
			protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
			{
				IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
				return props.Where(p => p.Writable).ToList();
			}
		}
	}
}