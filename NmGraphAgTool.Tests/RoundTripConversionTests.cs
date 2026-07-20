using System.Text;
using System.Xml.Linq;
using NmGraphAgTool.Converters;
using ValveKeyValue;
using Xunit;

namespace NmGraphAgTool.Tests;

public sealed class RoundTripConversionTests
{
    private const string SourceDirectoryEnvironmentVariable = "NMGRAPH_TEST_SOURCE_DIR";
    private const string DefaultSourceDirectory = @"D:\Work\CS_MODS\CS2\ag2\decompiled\animation\graphs";

    [Fact]
    public void AllVnmGraphFiles_RoundTripThroughAg_PreserveNormalizedKv3()
    {
        var sourceDirectory = ResolveSourceDirectory();
        var files = Directory.EnumerateFiles(sourceDirectory, "*.vnmgraph", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(files);

        var failures = new List<string>();

        foreach (var file in files)
        {
            var original = File.ReadAllText(file);

            string ag;
            try
            {
                ag = EsoAgConverter.ConvertVnmGraphToAg(original);
            }
            catch (InvalidDataException)
            {
                // Uses a Valve node class with no Esoterica equivalent; nothing to round-trip.
                continue;
            }

            var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);

            var normalizedOriginal = NormalizeKv3(original);
            var normalizedRoundTripped = NormalizeKv3(roundTripped);

            if (string.Equals(normalizedOriginal, normalizedRoundTripped, StringComparison.Ordinal))
            {
                continue;
            }

            var diff = DescribeFirstDifference(normalizedOriginal, normalizedRoundTripped);
            failures.Add($"{file}{Environment.NewLine}{diff}");
        }

        Assert.True(failures.Count == 0, string.Join($"{Environment.NewLine}{Environment.NewLine}", failures));
    }

    [Fact]
    public void AllVnmGraphFiles_DoNotSerializeUnknownToolsTypes()
    {
        var sourceDirectory = ResolveSourceDirectory();
        var files = Directory.EnumerateFiles(sourceDirectory, "*.vnmgraph", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(files);

        var failures = new List<string>();

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);

            string ag;
            try
            {
                ag = EsoAgConverter.ConvertVnmGraphToAg(source);
            }
            catch (InvalidDataException)
            {
                // Uses a Valve node class with no Esoterica equivalent; nothing to inspect.
                continue;
            }

            if (ag.Contains("UnknownToolsType", StringComparison.Ordinal))
            {
                failures.Add(file);
            }
        }

        Assert.True(failures.Count == 0,
            "UnknownToolsType was emitted for:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ValveRangeProperties_AreSerializedWithConcreteAgTypes()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocAnimationPoseNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Pose"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_pDefaultVariationData =
                              				{
                              					_class = "CNmGraphDocAnimationPoseNode::CData"
                              					m_clip = ""
                              					m_variationTimeValue = -1.0
                              				}
                              				m_overrides = [  ]
                              				m_defaultResourceName = ""
                              				m_inputTimeRemapRange =
                              				{
                              					m_flMin = 1.0
                              					m_flMax = 2.0
                              				}
                              				m_fixedTimeValue = 0.0
                              				m_useFramesAsInput = false
                              			},
                              			{
                              				_class = "CNmGraphDocFloatRemapNode"
                              				m_ID = "00000000-0000-0000-0000-000000000003"
                              				m_name = "Remap"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_inputRange =
                              				{
                              					m_flMin = 3.0
                              					m_flMax = 4.0
                              				}
                              				m_outputRange =
                              				{
                              					m_flMin = 5.0
                              					m_flMax = 6.0
                              				}
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        var rangeProperty = document.Descendants("Property")
            .FirstOrDefault(x => (string?) x.Attribute("ID") == "m_inputTimeRemapRange");

        Assert.NotNull(rangeProperty);
        Assert.Equal("1.0,2.0", (string?) rangeProperty!.Attribute("Value"));
        Assert.Empty(rangeProperty.Elements());
        Assert.DoesNotContain(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::UnknownToolsType");

        var remapRangeTypes = document.Descendants("Type")
            .Where(x => (string?) x.Attribute("TypeID") == "EE::Animation::FloatRemapNode::RemapRange")
            .ToArray();

        Assert.Equal(2, remapRangeTypes.Length);
        Assert.All(remapRangeTypes, type =>
        {
            Assert.DoesNotContain(type.Elements("Property"),
                x => (string?) x.Attribute("ID") is "m_flMin" or "m_flMax");
            Assert.Contains(type.Elements("Property"), x => (string?) x.Attribute("ID") == "m_begin");
            Assert.Contains(type.Elements("Property"), x => (string?) x.Attribute("ID") == "m_end");
        });
    }

    [Fact]
    public void ReferencedGraphVariationPaths_AreConvertedToEsotericaGraphResourceIds_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocReferencedGraphNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Child Graph"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_pDefaultVariationData =
                              				{
                              					_class = "CNmGraphDocReferencedGraphNode::CData"
                              					m_variation = "animation/graphs/viewmodel/viewmodel_inspects.vnmgraph+p90.vnmgraph"
                              				}
                              				m_overrides = [  ]
                              				m_defaultResourceName = ""
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        var graphDefinitionProperty = document.Descendants("Property")
            .FirstOrDefault(x => (string?) x.Attribute("ID") == "m_graphDefinition");

        Assert.NotNull(graphDefinitionProperty);
        Assert.Equal("data://animation/graphs/viewmodel/viewmodel_inspects.ag/p90.ag", (string?) graphDefinitionProperty!.Attribute("Value"));

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal(
            "animation/graphs/viewmodel/viewmodel_inspects.vnmgraph+p90.vnmgraph",
            reparsed["m_pRootGraph"]["m_nodes"][0]["m_pDefaultVariationData"]["m_variation"].ToString());
    }

    [Fact]
    public void ReferencedGraphPaths_WithoutVariation_AreConvertedToAgReferences_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocReferencedGraphNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Child Graph"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_pDefaultVariationData =
                              				{
                              					_class = "CNmGraphDocReferencedGraphNode::CData"
                              					m_variation = "animation/graphs/ui/uimodel_walkup.vnmgraph"
                              				}
                              				m_overrides = [  ]
                              				m_defaultResourceName = ""
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        var graphDefinitionProperty = document.Descendants("Property")
            .FirstOrDefault(x => (string?) x.Attribute("ID") == "m_graphDefinition");

        Assert.NotNull(graphDefinitionProperty);
        Assert.Equal("data://animation/graphs/ui/uimodel_walkup.ag", (string?) graphDefinitionProperty!.Attribute("Value"));

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal(
            "animation/graphs/ui/uimodel_walkup.vnmgraph",
            reparsed["m_pRootGraph"]["m_nodes"][0]["m_pDefaultVariationData"]["m_variation"].ToString());
    }

    [Fact]
    public void TwoBoneIKEffectorBoneName_IsConvertedToEffectorBoneID_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CnmGraphDocTwoBoneIKNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Two Bone IK"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_pDefaultVariationData =
                              				{
                              					_class = "CnmGraphDocTwoBoneIKNode::CData"
                              					m_effectorBoneName = "hand_r"
                              					m_flBlendTimeSeconds = 0.0
                              				}
                              				m_overrides = [  ]
                              				m_defaultResourceName = ""
                              				m_isTargetInWorldSpace = false
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        var effectorBoneProperty = document.Descendants("Property")
            .FirstOrDefault(x => (string?) x.Attribute("ID") == "m_effectorBoneID");

        Assert.NotNull(effectorBoneProperty);
        Assert.Equal("hand_r", (string?) effectorBoneProperty!.Attribute("Value"));

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal(
            "hand_r",
            reparsed["m_pRootGraph"]["m_nodes"][0]["m_pDefaultVariationData"]["m_effectorBoneName"].ToString());
    }

    [Fact]
    public void FollowBoneNode_IsConvertedToCompatibilityStub_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CnmGraphDocFollowBoneNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Follow Bone"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_pDefaultVariationData =
                              				{
                              					_class = "CnmGraphDocFollowBoneNode::CData"
                              					m_boneName = "weapon"
                              					m_followTargetBoneName = "hand_r"
                              				}
                              				m_overrides = [  ]
                              				m_defaultResourceName = ""
                              				m_mode = "RotationAndTranslation"
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::FollowBoneToolsNode");

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::FollowBoneToolsNode::Data");

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal("CnmGraphDocFollowBoneNode", reparsed["m_pRootGraph"]["m_nodes"][0]["_class"].ToString());
        Assert.Equal("CnmGraphDocFollowBoneNode::CData", reparsed["m_pRootGraph"]["m_nodes"][0]["m_pDefaultVariationData"]["_class"].ToString());
        Assert.Equal("weapon", reparsed["m_pRootGraph"]["m_nodes"][0]["m_pDefaultVariationData"]["m_boneName"].ToString());
        Assert.Equal("hand_r", reparsed["m_pRootGraph"]["m_nodes"][0]["m_pDefaultVariationData"]["m_followTargetBoneName"].ToString());
        Assert.Equal("RotationAndTranslation", reparsed["m_pRootGraph"]["m_nodes"][0]["m_mode"].ToString());
    }

    [Fact]
    public void FloatSpringNode_IsConvertedToCompatibilityStub_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocFloatSpringNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Float Spring"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_flHertz = 6.5
                              				m_flDampingRatio = 0.35
                              				m_bUseStartValue = false
                              				m_flStartValue = 12.0
                              			},
                              		]
                              		m_graphType = "ValueTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::FloatSpringToolsNode");

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal("CNmGraphDocFloatSpringNode", reparsed["m_pRootGraph"]["m_nodes"][0]["_class"].ToString());
        Assert.Equal("6.5", reparsed["m_pRootGraph"]["m_nodes"][0]["m_flHertz"].ToString());
        Assert.Equal("0.35", reparsed["m_pRootGraph"]["m_nodes"][0]["m_flDampingRatio"].ToString());
        Assert.Equal("0", reparsed["m_pRootGraph"]["m_nodes"][0]["m_bUseStartValue"].ToString());
        Assert.Equal("12", reparsed["m_pRootGraph"]["m_nodes"][0]["m_flStartValue"].ToString());
    }

    [Fact]
    public void IDBasedSelectorNode_IsConvertedToCompatibilityStub_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocIDBasedSelectorNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "ID Selector"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_optionLabels = [ "Idle", "Run" ]
                              				m_bIgnoreInvalidOptions = true
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::IDBasedSelectorToolsNode");

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal("CNmGraphDocIDBasedSelectorNode", reparsed["m_pRootGraph"]["m_nodes"][0]["_class"].ToString());
        Assert.Equal("Idle", reparsed["m_pRootGraph"]["m_nodes"][0]["m_optionLabels"][0].ToString());
        Assert.Equal("Run", reparsed["m_pRootGraph"]["m_nodes"][0]["m_optionLabels"][1].ToString());
        Assert.Equal("1", reparsed["m_pRootGraph"]["m_nodes"][0]["m_bIgnoreInvalidOptions"].ToString());
    }

    [Fact]
    public void IDBasedClipSelectorNode_IsConvertedToCompatibilityStub_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocIDBasedClipSelectorNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "ID Clip Selector"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_optionLabels = [ "Pistol", "Rifle" ]
                              				m_bIgnoreInvalidOptions = false
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::IDBasedClipSelectorToolsNode");

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal("CNmGraphDocIDBasedClipSelectorNode", reparsed["m_pRootGraph"]["m_nodes"][0]["_class"].ToString());
        Assert.Equal("Pistol", reparsed["m_pRootGraph"]["m_nodes"][0]["m_optionLabels"][0].ToString());
        Assert.Equal("Rifle", reparsed["m_pRootGraph"]["m_nodes"][0]["m_optionLabels"][1].ToString());
        Assert.Equal("0", reparsed["m_pRootGraph"]["m_nodes"][0]["m_bIgnoreInvalidOptions"].ToString());
    }

    [Fact]
    public void CommentNode_TextSizeAndColor_AreConvertedToEsotericaCommentNode_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocCommentNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = ""
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_size = [ 100.0, 100.0 ]
                              				m_comment = "TODO: rework this blend"
                              				m_nodeColor = [ 255, 76, 76, 76 ]
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        var commentType = document.Descendants("Type")
            .FirstOrDefault(x => (string?) x.Attribute("TypeID") == "EE::NodeGraph::CommentNode");

        Assert.NotNull(commentType);
        Assert.Equal("TODO: rework this blend",
            (string?) commentType!.Elements("Property").FirstOrDefault(x => (string?) x.Attribute("ID") == "m_name")?.Attribute("Value"));
        Assert.Equal("100.0,100.0",
            (string?) commentType.Elements("Property").FirstOrDefault(x => (string?) x.Attribute("ID") == "m_commentBoxSize")?.Attribute("Value"));
        Assert.Equal("FF4C4C4C",
            (string?) commentType.Elements("Property").FirstOrDefault(x => (string?) x.Attribute("ID") == "m_nodeColor")?.Attribute("Value"));

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);
        var node = reparsed["m_pRootGraph"]["m_nodes"][0];

        Assert.Equal("CNmGraphDocCommentNode", node["_class"].ToString());
        Assert.Equal("", node["m_name"].ToString());
        Assert.Equal("TODO: rework this blend", node["m_comment"].ToString());
        Assert.Equal("100", node["m_size"][0].ToString());
        Assert.Equal("100", node["m_size"][1].ToString());
        Assert.Equal("255", node["m_nodeColor"][0].ToString());
        Assert.Equal("76", node["m_nodeColor"][1].ToString());
        Assert.Equal("76", node["m_nodeColor"][2].ToString());
        Assert.Equal("76", node["m_nodeColor"][3].ToString());
    }

    [Fact]
    public void ExternalGraphNode_IsConvertedToExternalReferencedGraphToolsNode_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CNmGraphDocExternalGraphNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "External Graph"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::ExternalReferencedGraphToolsNode");

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);

        Assert.Equal("CNmGraphDocExternalGraphNode", reparsed["m_pRootGraph"]["m_nodes"][0]["_class"].ToString());
    }

    [Fact]
    public void VariationConstFloatNode_IsConvertedToVariationFloatToolsNode_AndBack()
    {
        const string source = """
                              <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                              {
                              	_class = "CNmGraphDocument"
                              	m_nVersion = 0
                              	m_pRootGraph =
                              	{
                              		_class = "CNmGraphDocFlowGraph"
                              		m_ID = "00000000-0000-0000-0000-000000000001"
                              		m_nodes =
                              		[
                              			{
                              				_class = "CnmGraphDocVariationConstFloatNode"
                              				m_ID = "00000000-0000-0000-0000-000000000002"
                              				m_name = "Variation Float"
                              				m_floatingComment = ""
                              				m_position = [ 0.0, 0.0 ]
                              				m_inputPins = [  ]
                              				m_outputPins = [  ]
                              				m_pDefaultVariationData =
                              				{
                              					_class = "CnmGraphDocVariationConstFloatNode::CData"
                              					m_flValue = 3.5
                              				}
                              				m_overrides = [  ]
                              				m_defaultResourceName = ""
                              			},
                              		]
                              		m_graphType = "BlendTree"
                              		m_viewOffset = [ 0.0, 0.0 ]
                              		m_connections = [  ]
                              	}
                              	m_variationHierarchy =
                              	{
                              		m_variations = [  ]
                              	}
                              }
                              """;

        var ag = EsoAgConverter.ConvertVnmGraphToAg(source);
        var document = XDocument.Parse(ag);

        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::VariationFloatToolsNode");
        Assert.Contains(document.Descendants("Type"),
            x => (string?) x.Attribute("TypeID") == "EE::Animation::VariationFloatToolsNode::Data");

        var roundTripped = EsoAgConverter.ConvertAgToVnmGraph(ag);
        var reparsed = ParseKv3(roundTripped);
        var node = reparsed["m_pRootGraph"]["m_nodes"][0];

        Assert.Equal("CnmGraphDocVariationConstFloatNode", node["_class"].ToString());
        Assert.Equal("CnmGraphDocVariationConstFloatNode::CData", node["m_pDefaultVariationData"]["_class"].ToString());
        Assert.Equal("3.5", node["m_pDefaultVariationData"]["m_flValue"].ToString());
    }

    [Theory]
    [InlineData("CNmGraphDocIsInactiveBranchConditionNode")]
    [InlineData("CnmGraphDocChainLookatNode")]
    [InlineData("CNmGraphDocEntryOverrideNode")]
    [InlineData("CNmGraphDocAimCSNode")]
    [InlineData("CnmGraphDocSnapWeaponNode")]
    public void UnsupportedValveClasses_ThrowInsteadOfSilentlyProducingInvalidAg(string unsupportedClass)
    {
        var source = $$"""
                       <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
                       {
                       	_class = "CNmGraphDocument"
                       	m_nVersion = 0
                       	m_pRootGraph =
                       	{
                       		_class = "CNmGraphDocFlowGraph"
                       		m_ID = "00000000-0000-0000-0000-000000000001"
                       		m_nodes =
                       		[
                       			{
                       				_class = "{{unsupportedClass}}"
                       				m_ID = "00000000-0000-0000-0000-000000000002"
                       				m_name = ""
                       				m_floatingComment = ""
                       				m_position = [ 0.0, 0.0 ]
                       				m_inputPins = [  ]
                       				m_outputPins = [  ]
                       			},
                       		]
                       		m_graphType = "BlendTree"
                       		m_viewOffset = [ 0.0, 0.0 ]
                       		m_connections = [  ]
                       	}
                       	m_variationHierarchy =
                       	{
                       		m_variations = [  ]
                       	}
                       }
                       """;

        var exception = Assert.Throws<InvalidDataException>(() => EsoAgConverter.ConvertVnmGraphToAg(source));
        Assert.Contains(unsupportedClass, exception.Message, StringComparison.Ordinal);
    }

    private static string ResolveSourceDirectory()
    {
        var configuredPath = Environment.GetEnvironmentVariable(SourceDirectoryEnvironmentVariable);
        var sourceDirectory = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultSourceDirectory
            : configuredPath;

        Assert.True(Directory.Exists(sourceDirectory),
            $"Source directory does not exist: {sourceDirectory}. Override it with {SourceDirectoryEnvironmentVariable}.");

        return sourceDirectory;
    }

    private static string NormalizeKv3(string text)
        => ParseKv3(text).ToString();

    private static KVObject ParseKv3(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues3Text);
        return serializer.Deserialize(stream).Root;
    }

    private static string DescribeFirstDifference(string expectedKv3, string actualKv3)
    {
        var expected = ParseKv3(expectedKv3);
        var actual = ParseKv3(actualKv3);

        return FindDifference(expected, actual, "$")
            ?? "Normalized KV3 text differs, but no structural difference was identified.";
    }

    private static string? FindDifference(KVObject expected, KVObject actual, string path)
    {
        if (expected.ValueType != actual.ValueType)
        {
            return $"{path}: value type differs. Expected {expected.ValueType}, actual {actual.ValueType}.";
        }

        if (expected.IsArray)
        {
            if (expected.Count != actual.Count)
            {
                return $"{path}: array length differs. Expected {expected.Count}, actual {actual.Count}.";
            }

            for (var i = 0; i < expected.Count; i++)
            {
                var difference = FindDifference(expected[i], actual[i], $"{path}[{i}]");
                if (difference is not null)
                {
                    return difference;
                }
            }

            return null;
        }

        if (expected.ValueType == KVValueType.Collection)
        {
            var expectedKeys = expected.Select(property => property.Key).ToArray();
            var actualKeys = actual.Select(property => property.Key).ToArray();

            if (!expectedKeys.SequenceEqual(actualKeys, StringComparer.Ordinal))
            {
                return $"{path}: object keys differ. Expected [{string.Join(", ", expectedKeys)}], actual [{string.Join(", ", actualKeys)}].";
            }

            foreach (var key in expectedKeys)
            {
                var difference = FindDifference(expected[key], actual[key], $"{path}.{key}");
                if (difference is not null)
                {
                    return difference;
                }
            }

            return null;
        }

        var expectedValue = expected.ToString();
        var actualValue = actual.ToString();
        return string.Equals(expectedValue, actualValue, StringComparison.Ordinal)
            ? null
            : $"{path}: value differs. Expected '{expectedValue}', actual '{actualValue}'.";
    }
}
