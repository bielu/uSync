using System;
using System.Xml.Linq;

using NUnit.Framework;
using NUnit.Framework.Internal;

using Umbraco.Web.Editors;

using uSync8.Core;
using uSync8.Core.Extensions;

namespace uSync8.Tests.Core.Extensions
{
    [TestFixture]
    public class XElementExtensionsTests
    {
        const string emptyGuid = "00000000-0000-0000-0000-000000000000";

        const string dataTypeXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <DataType Key = ""f0bc4bfb-b499-40d6-ba86-058885a5178c"" Alias=""Label"" Level=""1"">
                <Info>
                    <Name>Label</Name>
                        <EditorAlias>Umbraco.Label</EditorAlias>
                        <DatabaseType>Nvarchar</DatabaseType>
                </Info>
                <Config><![CDATA[{
                        ""ValueType"": ""STRING""
                }]]></Config>
            </DataType>";

        const string contentXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Content Key=""469b6e23-2ae0-4dcd-b4a2-6e857f75e1fb"" Alias=""ContentTemplate"" Level=""2"">
  <Info>
    <Parent Key=""fdfa7dd1-b45d-4a14-bc66-83f441df2d69"">Homepage</Parent>
    <Path>/Homepage/ContentTemplate</Path>
    <Trashed>false</Trashed>
    <ContentType>newDocTypeAlias</ContentType>
    <CreateDate>2019-04-30T15:41:36</CreateDate>
    <NodeName Default=""ContentTemplate"" />
    <SortOrder>1</SortOrder>
    <Published Default=""false"" />
    <Schedule />
    <Template />
  </Info>
  <Properties>
    <gridthing>
      <Value><![CDATA[{
  ""name"": ""1 column layout"",
  ""sections"": [
    {
      ""grid"": 12,
      ""rows"": [
        {
          ""label"": ""Headline"",
          ""name"": ""Headline"",
          ""areas"": [
            {
              ""grid"": 12,
              ""editors"": [
                ""headline""
              ],
              ""hasConfig"": false,
              ""controls"": [
                {
                  ""value"": ""<p>RTE in a grid</p>"",
                  ""editor"": {
                    ""name"": ""Rich text editor"",
                    ""alias"": ""rte"",
                    ""view"": ""rte"",
                    ""render"": null,
                    ""icon"": ""icon-article"",
                    ""config"": {}
                  },
                  ""active"": false
                },
                {
                  ""value"": {
                    ""id"": 1104,
                    ""udi"": ""umb://media/9ec1a8cff40841d4b560b21cfe637f9c"",
                    ""image"": ""/media/g0cdgwg0/curling.jpg"",
                    ""caption"": ""Image in a grid""
                  },
                  ""editor"": {
                    ""name"": ""Image"",
                    ""alias"": ""media"",
                    ""view"": ""media"",
                    ""render"": null,
                    ""icon"": ""icon-picture"",
                    ""config"": {}
                  },
                  ""active"": false
                }
              ]
            }
          ],
          ""hasConfig"": false,
          ""id"": ""6e9a49d6-0e4e-81cb-f9e7-51893a9fb27f""
        }
      ]
    }
  ]
}]]></Value>
    </gridthing>
    <test>
      <Value><![CDATA[]]></Value>
    </test>
  </Properties>
</Content>";

        const string blankXml = @"<?xml version=""1.0"" encoding=""utf-8""?><root/>";

        [Test]
        [TestCase(dataTypeXml, 1)]
        [TestCase(blankXml, 0)]
        public void GetLevel_Exists_Test(string xml, int expected)
        {
            var node = XElement.Parse(xml);
            var level = node.GetLevel();
            Assert.AreEqual(expected, level);
        }

        [Test]
        [TestCase(dataTypeXml, "Label")]
        [TestCase(blankXml, "")]
        public void GetAlias_Test(string xml, string expected)
        {
            var node = XElement.Parse(xml);
            var alias = node.GetAlias();
            Assert.AreEqual(expected, alias);
        }

        [Test]
        [TestCase(dataTypeXml, "f0bc4bfb-b499-40d6-ba86-058885a5178c")]
        [TestCase(blankXml, emptyGuid)]
        public void GetKey_Test(string xml, string expected)
        {
            var node = XElement.Parse(xml);
            var key = node.GetKey();
            Assert.AreEqual(Guid.Parse(expected), key);
        }

        [Test]
        [TestCase(dataTypeXml, "")]
        [TestCase(blankXml, "")]
        public void GetCultures_Test(string xml, string expected)
        {
            var node = XElement.Parse(xml);
            var key = node.GetCultures();
            Assert.AreEqual(expected, key);
        }

        [Test]
        [TestCase(dataTypeXml, "")]
        [TestCase(blankXml, "")]
        public void GetSegments_Test(string xml, string expected)
        {
            var node = XElement.Parse(xml);
            var key = node.GetSegments();
            Assert.AreEqual(expected, key);
        }

        [Test]
        [TestCase(dataTypeXml, emptyGuid)]
        [TestCase(blankXml, emptyGuid)]
        [TestCase(contentXml, "fdfa7dd1-b45d-4a14-bc66-83f441df2d69")]
        public void GetParentKey_Tests(string xml, string expected)
        {
            var node = XElement.Parse(xml);
            var key = node.GetParentKey();
            Assert.AreEqual(Guid.Parse(expected), key);
        }

        [Test]
        public void Element_ValueOrDefault_Test()
        {
            var node = XElement.Parse(contentXml);

            // int value 
            Assert.AreEqual(1, node.Element("Info").Element("SortOrder").ValueOrDefault(2));

            // convert to format in default (if possible)
            Assert.AreEqual("1", node.Element("Info").Element("SortOrder").ValueOrDefault("2"));

            // get default when value is missing 
            Assert.AreEqual(1, node.Element("Info").Element("NonExsistant").ValueOrDefault(1));

            // get default when value is blank
            Assert.AreEqual(string.Empty, node.Element("Info").Element("Schedule").ValueOrDefault(string.Empty));

            // get default when value cannot be converted to format
            Assert.AreEqual(Guid.Empty, node.Element("Info").Element("SortOrder").ValueOrDefault(Guid.Empty));
           
        }

    }
}
