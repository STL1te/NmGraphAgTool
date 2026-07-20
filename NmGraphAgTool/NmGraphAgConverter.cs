using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NmGraphAgTool.KV3;
using ValveKeyValue;

namespace NmGraphAgTool;

/// <summary>
/// Converts between Valve animation graph editor KV3 documents and Esoterica graph XML files.
/// </summary>
public static class NmGraphAgConverter
{
    private const string PreservedValveClassCommentPrefix = "VRF:ValveClass:";
    private const string PreservedValvePropertyCommentPrefix = "VRF:ValveProperty:";

    private static readonly Dictionary<string, string> ValveToAgClassMap = new(StringComparer.Ordinal)
    {
        ["CNmGraphDocFlowGraph"] = "EE::Animation::FlowGraph",
        ["CNmGraphDocStateMachineGraph"] = "EE::Animation::StateMachineGraph",
        ["CNmGraphDocPoseResultNode"] = "EE::Animation::PoseResultToolsNode",
        ["CNmGraphDocBoolResultNode"] = "EE::Animation::BoolResultToolsNode",
        ["CNmGraphDocIDResultNode"] = "EE::Animation::IDResultToolsNode",
        ["CNmGraphDocFloatResultNode"] = "EE::Animation::FloatResultToolsNode",
        ["CNmGraphDocVectorResultNode"] = "EE::Animation::VectorResultToolsNode",
        ["CNmGraphDocTargetResultNode"] = "EE::Animation::TargetResultToolsNode",
        ["CNmGraphDocBoneMaskResultNode"] = "EE::Animation::BoneMaskResultToolsNode",
        ["CNmGraphDocStateMachineNode"] = "EE::Animation::StateMachineToolsNode",
        ["CNmGraphDocStateNode"] = "EE::Animation::StateToolsNode",
        ["CNmGraphDocStateLayerDataNode"] = "EE::Animation::StateLayerDataToolsNode",
        ["CNmGraphDocEntryStateOverrideConduitNode"] = "EE::Animation::EntryStateOverrideConduitToolsNode",
        ["CNmGraphDocEntryStateOverrideConditionsNode"] = "EE::Animation::EntryStateOverrideConditionsToolsNode",
        ["CNmGraphDocGlobalTransitionConduitNode"] = "EE::Animation::GlobalTransitionConduitToolsNode",
        ["CNmGraphDocGlobalTransitionNode"] = "EE::Animation::GlobalTransitionToolsNode",
        ["CNmGraphDocTransitionConduitNode"] = "EE::Animation::TransitionConduitToolsNode",
        ["CNmGraphDocTransitionNode"] = "EE::Animation::TransitionToolsNode",
        ["CNmGraphDocClipNode"] = "EE::Animation::AnimationClipToolsNode",
        ["CNmGraphDocClipNode::CData"] = "EE::Animation::AnimationClipToolsNode::Data",
        ["CNmGraphDocAnimationPoseNode"] = "EE::Animation::AnimationPoseToolsNode",
        ["CNmGraphDocAnimationPoseNode::CData"] = "EE::Animation::AnimationPoseToolsNode::Data",
        ["CNmGraphDocBlend1DNode"] = "EE::Animation::Blend1DToolsNode",
        ["CNmGraphDocBlend2DNode"] = "EE::Animation::Blend2DToolsNode",
        ["CNmGraphDocSelectorNode"] = "EE::Animation::SelectorToolsNode",
        ["CNmGraphDocClipSelectorNode"] = "EE::Animation::AnimationClipSelectorToolsNode",
        ["CNmGraphDocParameterizedSelectorNode"] = "EE::Animation::ParameterizedSelectorToolsNode",
        ["CNmGraphDocParameterizedClipSelectorNode"] = "EE::Animation::ParameterizedAnimationClipSelectorToolsNode",
        ["CNmGraphDocSelectorConditionNode"] = "EE::Animation::SelectorConditionToolsNode",
        ["CNmGraphDocBoolControlParameterNode"] = "EE::Animation::BoolControlParameterToolsNode",
        ["CNmGraphDocFloatControlParameterNode"] = "EE::Animation::FloatControlParameterToolsNode",
        ["CNmGraphDocIDControlParameterNode"] = "EE::Animation::IDControlParameterToolsNode",
        ["CNmGraphDocVectorControlParameterNode"] = "EE::Animation::VectorControlParameterToolsNode",
        ["CNmGraphDocTargetControlParameterNode"] = "EE::Animation::TargetControlParameterToolsNode",
        ["CNmGraphDocBoolVirtualParameterNode"] = "EE::Animation::BoolVirtualParameterToolsNode",
        ["CNmGraphDocFloatVirtualParameterNode"] = "EE::Animation::FloatVirtualParameterToolsNode",
        ["CNmGraphDocIDVirtualParameterNode"] = "EE::Animation::IDVirtualParameterToolsNode",
        ["CNmGraphDocVectorVirtualParameterNode"] = "EE::Animation::VectorVirtualParameterToolsNode",
        ["CNmGraphDocTargetVirtualParameterNode"] = "EE::Animation::TargetVirtualParameterToolsNode",
        ["CNmGraphDocBoneMaskVirtualParameterNode"] = "EE::Animation::BoneMaskVirtualParameterToolsNode",
        ["CNmGraphDocBoolParameterReferenceNode"] = "EE::Animation::BoolParameterReferenceToolsNode",
        ["CNmGraphDocFloatParameterReferenceNode"] = "EE::Animation::FloatParameterReferenceToolsNode",
        ["CNmGraphDocIDParameterReferenceNode"] = "EE::Animation::IDParameterReferenceToolsNode",
        ["CNmGraphDocVectorParameterReferenceNode"] = "EE::Animation::VectorParameterReferenceToolsNode",
        ["CNmGraphDocTargetParameterReferenceNode"] = "EE::Animation::TargetParameterReferenceToolsNode",
        ["CNmGraphDocFloatComparisonNode"] = "EE::Animation::FloatComparisonToolsNode",
        ["CNmGraphDocFloatRangeComparisonNode"] = "EE::Animation::FloatRangeComparisonToolsNode",
        ["CNmGraphDocFloatSpringNode"] = "EE::Animation::FloatSpringToolsNode",
        ["CNmGraphDocIDComparisonNode"] = "EE::Animation::IDComparisonToolsNode",
        ["CNmGraphDocIDBasedSelectorNode"] = "EE::Animation::IDBasedSelectorToolsNode",
        ["CNmGraphDocIDBasedClipSelectorNode"] = "EE::Animation::IDBasedClipSelectorToolsNode",
        ["CNmGraphDocOrNode"] = "EE::Animation::OrToolsNode",
        ["CNmGraphDocAndNode"] = "EE::Animation::AndToolsNode",
        ["CNmGraphDocNotNode"] = "EE::Animation::NotToolsNode",
        ["CNmGraphDocIDEventConditionNode"] = "EE::Animation::IDEventConditionToolsNode",
        ["CNmGraphDocGraphEventConditionNode"] = "EE::Animation::GraphEventConditionToolsNode",
        ["CNmGraphDocIDToFloatNode"] = "EE::Animation::IDToFloatToolsNode",
        ["CNmGraphDocStateCompletedConditionNode"] = "EE::Animation::StateCompletedConditionToolsNode",
        ["CnmGraphDocTwoBoneIKNode"] = "EE::Animation::TwoBoneIKToolsNode",
        ["CnmGraphDocFollowBoneNode"] = "EE::Animation::FollowBoneToolsNode",
        ["CnmGraphDocFollowBoneNode::CData"] = "EE::Animation::FollowBoneToolsNode::Data",
        ["CnmGraphDocConstBoneTargetNode"] = "EE::Animation::ConstBoneTargetToolsNode",
        ["CNmGraphDocConstTargetNode"] = "EE::Animation::ConstTargetToolsNode",
        ["CNmGraphDocBoneMaskNode"] = "EE::Animation::BoneMaskToolsNode",
        ["CnmGraphDocConstFloatNode"] = "EE::Animation::ConstFloatToolsNode",
        ["CNmGraphDocCachedFloatNode"] = "EE::Animation::CachedFloatToolsNode",
        ["CnmGraphDocDurationScaleNode"] = "EE::Animation::DurationScaleToolsNode",
        ["CNmGraphDocLayerBlendNode"] = "EE::Animation::LayerBlendToolsNode",
        ["CNmGraphDocReferencedGraphNode"] = "EE::Animation::InternalReferencedGraphToolsNode",
        ["CNmGraphDocReferencedGraphNode::CData"] = "EE::Animation::InternalReferencedGraphToolsNode::Data",
        ["CNmGraphDocExternalGraphNode"] = "EE::Animation::ExternalReferencedGraphToolsNode",
        ["CnmGraphDocVariationConstFloatNode"] = "EE::Animation::VariationFloatToolsNode",
        ["CnmGraphDocVariationConstFloatNode::CData"] = "EE::Animation::VariationFloatToolsNode::Data",
        ["CNmGraphDocCommentNode"] = "EE::NodeGraph::CommentNode",
    };

    private static readonly Dictionary<string, string> AgToValveClassMap = CreateReverseDictionary(ValveToAgClassMap);

    private static readonly Dictionary<string, string> ValveToAgPropertyMap = new(StringComparer.Ordinal)
    {
        ["m_pRootGraph"] = "m_rootGraph",
        ["m_pChildGraph"] = "m_childGraph",
        ["m_pSecondaryGraph"] = "m_secondaryGraph",
        ["m_pDefaultVariationData"] = "m_defaultVariationData",
        ["m_pData"] = "m_variationData",
        ["m_variation"] = "m_graphDefinition",
        ["m_effectorBoneName"] = "m_effectorBoneID",
        ["m_position"] = "m_canvasPosition",
        ["m_graphType"] = "m_type",
        ["m_bIsDynamicPin"] = "m_isDynamic",
        ["m_bAllowMultipleOutConnections"] = "m_allowMultipleOutConnections",
        ["m_bSampleRootMotion"] = "m_sampleRootMotion",
        ["m_bAllowLooping"] = "m_allowLooping",
        ["m_values"] = "m_IDs",
        ["m_flSpeedMultiplier"] = "m_speedMultiplier",
        ["m_flDesiredDuration"] = "m_desiredDuration",
        ["m_flValue"] = "m_value",
        ["m_nStartSyncEventOffset"] = "m_startSyncEventOffset",
        ["m_clip"] = "m_animClip",
        ["m_flBegin"] = "m_begin",
        ["m_flEnd"] = "m_end",
        ["m_flMin"] = "m_begin",
        ["m_flMax"] = "m_end",
        ["m_bLimitSearchToSourceState"] = "m_limitSearchToSourceState",
        ["m_bIgnoreInactiveBranchEvents"] = "m_ignoreInactiveBranchEvents",
        ["m_bSwitchDynamically"] = "m_switchDynamically",
        ["m_flBlendTimeSeconds"] = "m_blendTime",
        ["m_size"] = "m_commentBoxSize",
    };

    private static readonly Dictionary<string, string> AgToValvePropertyMap = CreateAgToValvePropertyMap();

    private static readonly HashSet<string> IgnoredValveProperties =
    [
        "m_nVersion",
        "m_debugParameterSets",
        "m_dictionaryIDSetIDs",
        "m_floatingComment",
        "m_flViewZoom",
        "m_defaultResourceName",
        "m_cloneSourceStateID",
        "m_stateEvents",
        "m_timedStateEvents",
        "m_bUseActualElapsedTimeInStateForTimedEvents",
        "m_pUserData",
        "m_graphEvents",
    ];

    private static readonly HashSet<string> UnsupportedValveClasses =
    [
        // CS2-specific additions with no equivalent ToolsNode in this Esoterica branch.
        "CNmGraphDocIsInactiveBranchConditionNode",
        "CnmGraphDocChainLookatNode",
        "CNmGraphDocEntryOverrideNode",
    ];

    private static readonly HashSet<string> IgnoredAgProperties = [];

    public static string ConvertVnmGraphToAg(string input)
    {
        var document = KV3Helpers.ParseKV3(new MemoryStream(Encoding.UTF8.GetBytes(input)));
        return ConvertVnmGraphToAg(document).ToString();
    }

    public static XDocument ConvertVnmGraphToAg(KVDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Root;
        if (!string.Equals(root.GetStringProperty("_class"), "CNmGraphDocument", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected a CNmGraphDocument root.");
        }

        ValidateNoUnsupportedValveClasses(root);

        var graphDescriptor = new XElement("Type",
            new XAttribute("TypeID", "EE::Animation::GraphResourceDescriptor"),
            new XAttribute("Version", "1"));

        var graphDefinition = new XElement("Type",
            new XAttribute("ID", "m_graphDefinition"),
            new XAttribute("TypeID", "EE::Animation::ToolsGraphDefinition"));

        foreach (var property in root)
        {
            if (property.Key is "_class" or "m_nVersion")
            {
                continue;
            }

            if (IgnoredValveProperties.Contains(property.Key))
            {
                graphDefinition.Add(CreatePreservedValvePropertyComment(property.Key, property.Value));
                continue;
            }

            var element = ConvertValvePropertyToAg(property.Key, property.Value);
            if (element is not null)
            {
                graphDefinition.Add(element);
            }
        }

        graphDescriptor.Add(graphDefinition);
        return new XDocument(graphDescriptor);
    }

    private static void ValidateNoUnsupportedValveClasses(KVObject value)
    {
        var unsupportedClasses = new HashSet<string>(StringComparer.Ordinal);
        CollectUnsupportedValveClasses(value, unsupportedClasses);

        if (unsupportedClasses.Count == 0)
        {
            return;
        }

        throw new InvalidDataException(
            "The source graph uses Valve node classes that do not exist in this Esoterica branch: "
            + string.Join(", ", unsupportedClasses.OrderBy(x => x, StringComparer.Ordinal)));
    }

    private static void CollectUnsupportedValveClasses(KVObject value, HashSet<string> unsupportedClasses)
    {
        if (value.ValueType == KVValueType.Collection)
        {
            var className = value.GetStringProperty("_class");
            if (!string.IsNullOrEmpty(className) && UnsupportedValveClasses.Contains(className))
            {
                unsupportedClasses.Add(className);
            }

            foreach (var property in value)
            {
                CollectUnsupportedValveClasses(property.Value, unsupportedClasses);
            }
        }
        else if (value.IsArray)
        {
            for (var i = 0; i < value.Count; i++)
            {
                CollectUnsupportedValveClasses(value[i], unsupportedClasses);
            }
        }
    }


    public static string ConvertAgToVnmGraph(string input)
    {
        var document = XDocument.Parse(input, LoadOptions.None);
        return ConvertAgToVnmGraph(document).ToKV3String();
    }

    public static KVDocument ConvertAgToVnmGraph(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var rootType = document.Root ?? throw new InvalidDataException("Missing XML root element.");
        if (!string.Equals(rootType.Name.LocalName, "Type", StringComparison.Ordinal) ||
            !string.Equals(rootType.Attribute("TypeID")?.Value, "EE::Animation::GraphResourceDescriptor", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected an EE::Animation::GraphResourceDescriptor root.");
        }

        var graphDefinition = rootType.Elements("Type")
            .FirstOrDefault(element => string.Equals(element.Attribute("ID")?.Value, "m_graphDefinition", StringComparison.Ordinal))
            ?? throw new InvalidDataException("Missing m_graphDefinition.");

        var root = KVObject.Collection();
        root.Add("_class", "CNmGraphDocument");
        root.Add("m_nVersion", 0L);

        foreach (var node in graphDefinition.Nodes())
        {
            if (TryReadPreservedValveProperty(node, out var preservedProperty))
            {
                var preserved = preservedProperty!.Value;
                root[preserved.Key] = preserved.Value;
                continue;
            }

            if (node is not XElement child)
            {
                continue;
            }

            var converted = ConvertAgElementToValveProperty(child);
            if (converted is not null)
            {
                root[converted.Value.Key] = converted.Value.Value;
            }
        }

        return root.ToKV3Document();
    }

    private static XElement? ConvertValvePropertyToAg(string propertyName, KVObject value)
    {
        if (IgnoredValveProperties.Contains(propertyName) || value.IsNull)
        {
            return null;
        }

        var mappedPropertyName = MapValvePropertyName(propertyName);

        if (TryConvertSpecialValvePropertyToAg(mappedPropertyName, propertyName, value, out var specialElement))
        {
            return specialElement;
        }

        if (TryConvertValveScalarToAgProperty(mappedPropertyName, propertyName, value, out var scalarElement))
        {
            return scalarElement;
        }

        if (value.IsArray)
        {
            return ConvertValveArrayToAgProperty(mappedPropertyName, propertyName, value);
        }

        return ConvertValveObjectToAgType(value, mappedPropertyName, GetAgTypeIdForProperty(mappedPropertyName, value));
    }

    private static bool TryConvertSpecialValvePropertyToAg(string mappedPropertyName, string originalPropertyName, KVObject value, out XElement element)
    {
        element = null!;

        if (originalPropertyName != "m_blendSpace" || value.ValueType != KVValueType.Collection)
        {
            return false;
        }

        if (IsValveBlend1DBlendSpace(value))
        {
            element = ConvertValveBlend1DSpaceToAg(mappedPropertyName, value);
            return true;
        }

        if (IsValveBlend2DBlendSpace(value))
        {
            element = ConvertValveObjectToAgType(value, mappedPropertyName, "EE::Animation::Blend2DToolsNode::BlendSpace");
            return true;
        }

        return false;
    }

    private static XElement ConvertValveBlend1DSpaceToAg(string propertyName, KVObject value)
    {
        var property = new XElement("Property", new XAttribute("ID", propertyName));
        var points = value.GetArray("m_points");

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var type = new XElement("Type",
                new XAttribute("TypeID", "EE::Animation::Blend1DToolsNode::BlendSpacePoint"),
                new XAttribute("Index", i));

            foreach (var pointProperty in point)
            {
                if (pointProperty.Key == "_class")
                {
                    continue;
                }

                var agPropertyName = pointProperty.Key == "m_flValue" ? "m_value" : MapValvePropertyName(pointProperty.Key);
                var childElement = ConvertValvePropertyToAg(agPropertyName, pointProperty.Value);
                if (childElement is not null)
                {
                    type.Add(childElement);
                }
            }

            property.Add(type);
        }

        return property;
    }

    private static XElement ConvertValveArrayToAgProperty(string mappedPropertyName, string originalPropertyName, KVObject arrayValue)
    {
        if (IsFloat2Property(originalPropertyName) && TryReadPrimitiveArray(arrayValue, out var primitiveValues))
        {
            return CreateAgProperty(mappedPropertyName, string.Join(",", primitiveValues.Select(item => FormatAgScalar(item))));
        }

        var property = new XElement("Property", new XAttribute("ID", mappedPropertyName));
        var index = 0;

        for (var i = 0; i < arrayValue.Count; i++)
        {
            var item = arrayValue[i];

            if (item.IsNull)
            {
                index++;
                continue;
            }

            if (item.IsArray && IsFloat2Property(originalPropertyName) && TryReadPrimitiveArray(item, out var nestedPrimitiveValues))
            {
                property.Add(new XElement("Property",
                    new XAttribute("Index", index),
                    new XAttribute("Value", string.Join(",", nestedPrimitiveValues.Select(child => FormatAgScalar(child))))));
            }
            else if (item.IsArray)
            {
                property.Add(new XElement("Property",
                    new XAttribute("Index", index),
                    new XAttribute("Value", FormatAgPrimitiveArray(item))));
            }
            else if (item.ValueType == KVValueType.Collection)
            {
                property.Add(ConvertValveObjectToAgType(item, index, GetAgTypeIdForArrayItem(mappedPropertyName, item)));
            }
            else
            {
                property.Add(new XElement("Property",
                    new XAttribute("Index", index),
                    new XAttribute("Value", FormatAgScalar(item))));
            }

            index++;
        }

        return property;
    }

    private static XElement ConvertValveObjectToAgType(KVObject objectValue, string? propertyId, string? forcedTypeId)
    {
        var originalClassName = objectValue.GetStringProperty("_class");
        var typeId = forcedTypeId ?? MapValveClassName(originalClassName);
        var type = new XElement("Type", new XAttribute("TypeID", typeId));

        if (!string.IsNullOrEmpty(propertyId))
        {
            type.Add(new XAttribute("ID", propertyId));
        }

        if (!string.IsNullOrEmpty(originalClassName) && ShouldPreserveOriginalValveClassName(originalClassName))
        {
            type.Add(new XComment($"{PreservedValveClassCommentPrefix}{originalClassName}"));
        }

        foreach (var property in objectValue)
        {
            if (property.Key == "_class")
            {
                continue;
            }

            if (IgnoredValveProperties.Contains(property.Key))
            {
                type.Add(CreatePreservedValvePropertyComment(property.Key, property.Value));
                continue;
            }

            if (ShouldPreserveValvePropertyAsCommentOnly(originalClassName, property.Key))
            {
                type.Add(CreatePreservedValvePropertyComment(property.Key, property.Value));
                continue;
            }

            if (property.Key == "m_graphType" && typeId == "EE::Animation::StateMachineGraph")
            {
                continue;
            }

            if (property.Key == "m_comment" && typeId == "EE::NodeGraph::CommentNode")
            {
                var commentElement = ConvertValvePropertyToAg("m_name", property.Value);
                if (commentElement is not null)
                {
                    type.Add(commentElement);
                }

                continue;
            }

            var childElement = ConvertValvePropertyToAg(property.Key, property.Value);
            if (childElement is not null)
            {
                type.Add(childElement);

                if (property.Key == "m_graphType" && NeedsOriginalGraphTypePreservation(property.Value.ToString()))
                {
                    type.Add(CreatePreservedValvePropertyComment(property.Key, property.Value));
                }
                else if (ShouldPreserveOriginalValvePropertyValue(property.Key))
                {
                    type.Add(CreatePreservedValvePropertyComment(property.Key, property.Value));
                }
            }
        }

        return type;
    }

    private static XElement ConvertValveObjectToAgType(KVObject objectValue, int index, string? forcedTypeId)
    {
        var type = ConvertValveObjectToAgType(objectValue, propertyId: null, forcedTypeId);
        type.Add(new XAttribute("Index", index));
        return type;
    }

    private static KeyValuePair<string, KVObject>? ConvertAgElementToValveProperty(XElement element)
    {
        if (element.Name.LocalName == "Type")
        {
            var propertyId = element.Attribute("ID")?.Value
                ?? throw new InvalidDataException("Nested Type element is missing ID.");

            return new KeyValuePair<string, KVObject>(MapAgPropertyName(propertyId), ConvertAgTypeToValveObject(element));
        }

        if (element.Name.LocalName != "Property")
        {
            return null;
        }

        var propertyNameAttribute = element.Attribute("ID");
        if (propertyNameAttribute is null)
        {
            return null;
        }

        var propertyName = MapAgPropertyName(propertyNameAttribute.Value);
        if (IgnoredAgProperties.Contains(propertyName))
        {
            return null;
        }

        if (TryConvertSpecialAgPropertyToValve(element, propertyName, out var specialProperty))
        {
            return specialProperty;
        }

        var valueAttribute = element.Attribute("Value");
        if (valueAttribute is not null)
        {
            return new KeyValuePair<string, KVObject>(propertyName, ParseAgScalarProperty(propertyName, valueAttribute.Value));
        }

        var array = KVObject.Array();

        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == "Type")
            {
                array.Add(ConvertAgTypeToValveObject(child));
            }
            else if (child.Name.LocalName == "Property")
            {
                var childValue = child.Attribute("Value")?.Value ?? string.Empty;
                array.Add(ParseAgArrayItem(propertyName, childValue));
            }
        }

        return new KeyValuePair<string, KVObject>(propertyName, array);
    }

    private static bool TryConvertSpecialAgPropertyToValve(XElement element, string propertyName, out KeyValuePair<string, KVObject>? property)
    {
        property = null;

        if (propertyName != "m_blendSpace")
        {
            return false;
        }

        var childTypes = element.Elements("Type").ToArray();
        if (childTypes.Length == 0 || childTypes.Any(x => x.Attribute("TypeID")?.Value != "EE::Animation::Blend1DToolsNode::BlendSpacePoint"))
        {
            return false;
        }

        var points = KVObject.Array();
        foreach (var childType in childTypes)
        {
            var point = ConvertAgTypeToValveObject(childType);
            if (point.ContainsKey("m_value"))
            {
                point["m_flValue"] = point["m_value"];
                point.Remove("m_value");
            }

            points.Add(point);
        }

        var blendSpace = KVObject.Collection();
        blendSpace.Add("m_points", points);
        property = new KeyValuePair<string, KVObject>(propertyName, blendSpace);
        return true;
    }

    private static KVObject ConvertAgTypeToValveObject(XElement typeElement)
    {
        var typeId = typeElement.Attribute("TypeID")?.Value
            ?? throw new InvalidDataException("Type element is missing TypeID.");

        var preservedClassName = TryReadPreservedValveClass(typeElement);
        var valveClassName = preservedClassName ?? MapAgClassName(typeId);
        if (string.IsNullOrEmpty(preservedClassName) && valveClassName == "CNmGraphDocUnknownToolsType")
        {
            valveClassName = string.Empty;
        }

        var objectValue = KVObject.Collection();

        if (!string.IsNullOrEmpty(valveClassName))
        {
            objectValue.Add("_class", valveClassName);
        }

        var addedStateMachineGraphType = false;

        foreach (var node in typeElement.Nodes())
        {
            if (typeId == "EE::Animation::StateMachineGraph"
                && !addedStateMachineGraphType
                && ShouldInsertStateMachineGraphTypeBeforeNode(node))
            {
                objectValue["m_graphType"] = "StateMachine";
                addedStateMachineGraphType = true;
            }

            if (TryReadPreservedValveProperty(node, out var preservedProperty))
            {
                var preserved = preservedProperty!.Value;
                objectValue[preserved.Key] = preserved.Value;
                continue;
            }

            if (node is not XElement child)
            {
                continue;
            }

            var converted = ConvertAgElementToValveProperty(child);
            if (converted is null)
            {
                continue;
            }

            if (typeId == "EE::Animation::FlowGraph" && converted.Value.Key == "m_type")
            {
                objectValue["m_graphType"] = converted.Value.Value;
            }
            else if (typeId == "EE::NodeGraph::CommentNode" && converted.Value.Key == "m_name")
            {
                objectValue["m_comment"] = converted.Value.Value;
            }
            else
            {
                objectValue[converted.Value.Key] = converted.Value.Value;
            }
        }

        if (typeId == "EE::Animation::StateMachineGraph" && !addedStateMachineGraphType)
        {
            objectValue["m_graphType"] = "StateMachine";
        }

        return objectValue;
    }

    private static XComment CreatePreservedValvePropertyComment(string propertyName, KVObject value)
    {
        var wrapper = KVObject.Collection();
        wrapper.Add("value", value);
        var serialized = wrapper.ToKV3Document().ToKV3String();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(serialized));
        return new XComment($"{PreservedValvePropertyCommentPrefix}{propertyName}:{payload}");
    }

    private static bool TryReadPreservedValveProperty(XNode node, out KeyValuePair<string, KVObject>? property)
    {
        property = null;

        if (node is not XComment comment)
        {
            return false;
        }

        var value = comment.Value;
        if (!value.StartsWith(PreservedValvePropertyCommentPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIdx = value.IndexOf(':', PreservedValvePropertyCommentPrefix.Length);
        if (separatorIdx < 0)
        {
            return false;
        }

        var propertyName = value[PreservedValvePropertyCommentPrefix.Length..separatorIdx];
        var payload = value[(separatorIdx + 1)..];
        var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
        var document = KV3Helpers.ParseKV3(stream);
        property = new KeyValuePair<string, KVObject>(propertyName, document.Root["value"]);
        return true;
    }

    private static string? TryReadPreservedValveClass(XElement typeElement)
        => typeElement.Nodes()
            .OfType<XComment>()
            .Select(comment => comment.Value)
            .FirstOrDefault(value => value.StartsWith(PreservedValveClassCommentPrefix, StringComparison.Ordinal))
            ?[PreservedValveClassCommentPrefix.Length..];

    private static bool ShouldPreserveOriginalValveClassName(string valveClassName)
        => !ValveToAgClassMap.ContainsKey(valveClassName);

    private static bool ShouldInsertStateMachineGraphTypeBeforeNode(XNode node)
        => node is XElement element
            && element.Name.LocalName == "Property"
            && element.Attribute("ID")?.Value is "m_viewOffset" or "m_entryStateID";

    private static bool TryConvertValveScalarToAgProperty(string mappedPropertyName, string originalPropertyName, KVObject value, out XElement scalarElement)
    {
        scalarElement = null!;

        if (TryConvertValveFloatRangeToAgProperty(mappedPropertyName, value, out scalarElement))
        {
            return true;
        }

        if (IsFloat2Property(originalPropertyName) && TryReadPrimitiveArray(value, out var float2Values))
        {
            scalarElement = CreateAgProperty(mappedPropertyName, string.Join(",", float2Values.Select(item => FormatAgScalar(item))));
            return true;
        }

        if (originalPropertyName == "m_curve" && TryConvertValveFloatCurveToAgProperty(mappedPropertyName, value, out scalarElement))
        {
            return true;
        }

        if (originalPropertyName == "m_nodeColor" && TryConvertValveColorToAgProperty(mappedPropertyName, value, out scalarElement))
        {
            return true;
        }

        if (value.ValueType is KVValueType.Collection or KVValueType.Array or KVValueType.Null)
        {
            return false;
        }

        var scalarValue = originalPropertyName switch
        {
            "m_graphType" => MapValveGraphTypeToAg(value.ToString()),
            "m_type" => MapValveValueTypeToAg(value.ToString()),
            // These are reflected GraphValueType enums in Esoterica, so they need the enum token, not the pin display name.
            "m_resultType" or "m_parameterValueType" => value.ToString(),
            _ => FormatAgScalar(value, mappedPropertyName),
        };

        scalarElement = CreateAgProperty(mappedPropertyName, scalarValue);
        return true;
    }

    private static bool TryConvertValveFloatRangeToAgProperty(string mappedPropertyName, KVObject value, out XElement scalarElement)
    {
        scalarElement = null!;

        if (!IsFloatRangeProperty(mappedPropertyName) || value.ValueType != KVValueType.Collection)
        {
            return false;
        }

        if (!TryReadValveRange(value, out var min, out var max))
        {
            return false;
        }

        scalarElement = CreateAgProperty(mappedPropertyName, $"{FormatAgFloatingPoint(min)}{','}{FormatAgFloatingPoint(max)}");
        return true;
    }

    private static bool TryConvertValveFloatCurveToAgProperty(string mappedPropertyName, KVObject value, out XElement scalarElement)
    {
        scalarElement = null!;

        if (mappedPropertyName != "m_curve" || value.ValueType != KVValueType.Collection)
        {
            return false;
        }

        if (!TryFormatValveFloatCurve(value, out var curveString))
        {
            return false;
        }

        scalarElement = CreateAgProperty(mappedPropertyName, curveString);
        return true;
    }

    private static bool TryConvertValveColorToAgProperty(string mappedPropertyName, KVObject value, out XElement scalarElement)
    {
        scalarElement = null!;

        if (!TryReadPrimitiveArray(value, out var components) || components.Count != 4)
        {
            return false;
        }

        static byte ToByteComponent(KVObject component)
            => (byte)Math.Clamp((int)double.Parse(component.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture), 0, 255);

        var a = ToByteComponent(components[0]);
        var r = ToByteComponent(components[1]);
        var g = ToByteComponent(components[2]);
        var b = ToByteComponent(components[3]);

        scalarElement = CreateAgProperty(mappedPropertyName, $"{a:X2}{b:X2}{g:X2}{r:X2}");
        return true;
    }

    private static KVObject ParseValveColor(string value)
    {
        if (value.Length != 8 || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return ParseAgScalar(value);
        }

        var a = Convert.ToByte(value[..2], 16);
        var b = Convert.ToByte(value[2..4], 16);
        var g = Convert.ToByte(value[4..6], 16);
        var r = Convert.ToByte(value[6..8], 16);

        return KVObject.Array([(long)a, (long)r, (long)g, (long)b]);
    }

    private static XElement CreateAgProperty(string propertyName, string value)
        => new("Property",
            new XAttribute("ID", propertyName),
            new XAttribute("Value", value));

    private static bool TryReadPrimitiveArray(KVObject value, out IReadOnlyList<KVObject> items)
    {
        items = [];
        if (!value.IsArray)
        {
            return false;
        }

        var list = new List<KVObject>(value.Count);
        for (var i = 0; i < value.Count; i++)
        {
            var item = value[i];

            if (item.ValueType is KVValueType.Collection or KVValueType.Array or KVValueType.Null)
            {
                return false;
            }

            list.Add(item);
        }

        items = list;
        return true;
    }

    private static bool IsFloat2Property(string propertyName)
        => propertyName is "m_position" or "m_viewOffset" or "m_canvasPosition" or "m_size" or "m_commentBoxSize";

    private static bool IsFloatRangeProperty(string propertyName)
        => propertyName is "m_inputTimeRemapRange" or "m_clampRange" or "m_range";

    private static bool TryReadValveRange(KVObject value, out string min, out string max)
    {
        min = string.Empty;
        max = string.Empty;

        if (!value.TryGetValue("m_flMin", out var minValue) || !value.TryGetValue("m_flMax", out var maxValue))
        {
            return false;
        }

        min = minValue.ToString();
        max = maxValue.ToString();
        return true;
    }

    private static KVObject ParseAgScalarProperty(string propertyName, string value)
    {
        if (IsFloat2Property(propertyName))
        {
            return ParseFloatArray(value);
        }

        if (IsFloatRangeProperty(propertyName))
        {
            return ParseValveFloatRange(value);
        }

        if (propertyName == "m_curve")
        {
            return ParseValveFloatCurve(value);
        }

        if (propertyName == "m_nodeColor")
        {
            return ParseValveColor(value);
        }

        if (propertyName == "m_type")
        {
            return MapAgTypeValueToValve(value);
        }

        if (propertyName is "m_resultType" or "m_parameterValueType")
        {
            return MapAgValueTypeToValve(value);
        }

        return ParseAgScalar(MapAgResourcePathToValve(value, propertyName));
    }

    private static KVObject ParseAgArrayItem(string propertyName, string value)
    {
        if (IsFloat2Property(propertyName))
        {
            return ParseFloatArray(value);
        }

        return ParseAgScalar(value);
    }

    private static KVObject ParseAgScalar(string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            && !value.Contains('.', StringComparison.Ordinal))
        {
            return integer;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
        {
            return single;
        }

        return value;
    }

    private static KVObject ParseFloatArray(string value)
    {
        var array = KVObject.Array();

        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
            {
                array.Add(single);
            }
            else
            {
                array.Add(part);
            }
        }

        return array;
    }

    private static KVObject ParseValveFloatRange(string value)
    {
        var values = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var range = KVObject.Collection();
        range.Add("m_flMin", values.Length > 0 ? ParseAgScalar(values[0]) : 0.0f);
        range.Add("m_flMax", values.Length > 1 ? ParseAgScalar(values[1]) : 0.0f);
        return range;
    }

    private static KVObject ParseValveFloatCurve(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var numPoints) || numPoints < 0)
        {
            return KVObject.Collection();
        }

        var spline = KVObject.Array();
        var tangents = KVObject.Array();
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        var cursor = 1;
        for (var i = 0; i < numPoints; i++)
        {
            var x = cursor < parts.Length ? ParseAgScalar(parts[cursor++]) : 0.0f;
            var y = cursor < parts.Length ? ParseAgScalar(parts[cursor++]) : 0.0f;
            var inTangent = cursor < parts.Length ? ParseAgScalar(parts[cursor++]) : 1.0f;
            var outTangent = cursor < parts.Length ? ParseAgScalar(parts[cursor++]) : 1.0f;
            var tangentMode = cursor < parts.Length ? parts[cursor++] : "0";

            spline.Add(KVObject.Collection([
                new KeyValuePair<string, KVObject>("x", x),
                new KeyValuePair<string, KVObject>("y", y),
                new KeyValuePair<string, KVObject>("m_flSlopeIncoming", inTangent),
                new KeyValuePair<string, KVObject>("m_flSlopeOutgoing", outTangent),
            ]));

            var valveTangentMode = MapAgCurveTangentModeToValve(tangentMode);
            tangents.Add(KVObject.Collection([
                new KeyValuePair<string, KVObject>("m_nIncomingTangent", valveTangentMode),
                new KeyValuePair<string, KVObject>("m_nOutgoingTangent", valveTangentMode),
            ]));

            if (float.TryParse(x.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var xFloat))
            {
                minX = Math.Min(minX, xFloat);
                maxX = Math.Max(maxX, xFloat);
            }

            if (float.TryParse(y.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var yFloat))
            {
                minY = Math.Min(minY, yFloat);
                maxY = Math.Max(maxY, yFloat);
            }
        }

        if (float.IsPositiveInfinity(minX))
        {
            minX = minY = maxX = maxY = 0.0f;
        }

        return KVObject.Collection([
            new KeyValuePair<string, KVObject>("m_spline", spline),
            new KeyValuePair<string, KVObject>("m_tangents", tangents),
            new KeyValuePair<string, KVObject>("m_vDomainMins", KVObject.Array([minX, minY])),
            new KeyValuePair<string, KVObject>("m_vDomainMaxs", KVObject.Array([maxX, maxY])),
        ]);
    }

    private static bool TryFormatValveFloatCurve(KVObject value, out string curveString)
    {
        curveString = string.Empty;

        if (!value.TryGetValue("m_spline", out var spline) || !spline.IsArray)
        {
            return false;
        }

        value.TryGetValue("m_tangents", out var tangents);

        var segments = new List<string>(1 + spline.Count * 5) { spline.Count.ToString(CultureInfo.InvariantCulture) };

        for (var i = 0; i < spline.Count; i++)
        {
            if (spline[i].ValueType != KVValueType.Collection)
            {
                return false;
            }

            var point = spline[i];
            segments.Add(FormatAgScalar(point["x"]));
            segments.Add(FormatAgScalar(point["y"]));
            segments.Add(FormatAgScalar(point["m_flSlopeIncoming"]));
            segments.Add(FormatAgScalar(point["m_flSlopeOutgoing"]));

            var tangentMode = "0";
            if (tangents is not null && tangents.IsArray && i < tangents.Count && tangents[i].ValueType == KVValueType.Collection)
            {
                tangentMode = MapValveCurveTangentModeToAg(tangents[i]);
            }

            segments.Add(tangentMode);
        }

        curveString = string.Join(",", segments);
        return true;
    }

    private static string MapValveCurveTangentModeToAg(KVObject tangentValue)
    {
        var incoming = tangentValue.TryGetValue("m_nIncomingTangent", out var incomingValue) ? incomingValue.ToString() : string.Empty;
        var outgoing = tangentValue.TryGetValue("m_nOutgoingTangent", out var outgoingValue) ? outgoingValue.ToString() : string.Empty;

        return incoming == "CURVE_TANGENT_FREE" || outgoing == "CURVE_TANGENT_FREE" ? "0" : "1";
    }

    private static string MapAgCurveTangentModeToValve(string tangentMode)
        => tangentMode == "0" ? "CURVE_TANGENT_FREE" : "CURVE_TANGENT_SPLINE";

    private static bool ShouldPreserveValvePropertyAsCommentOnly(string? originalClassName, string propertyName)
        => (string.Equals(originalClassName, "CNmGraphDocBoneMaskNode", StringComparison.Ordinal)
                && propertyName is "m_pDefaultVariationData" or "m_overrides" or "m_defaultResourceName")
            || (string.Equals(originalClassName, "CNmGraphDocCommentNode", StringComparison.Ordinal)
                && propertyName == "m_name");

    private static bool ShouldPreserveOriginalValvePropertyValue(string propertyName)
        => propertyName == "m_curve";

    private static string FormatAgScalar(KVObject value, string? propertyName = null)
    {
        var formatted = value.ValueType switch
        {
            KVValueType.Boolean => ((bool)value) ? "True" : "False",
            KVValueType.FloatingPoint or KVValueType.FloatingPoint64 => FormatAgFloatingPoint(value.ToString()),
            _ => value.ToString(),
        };

        return MapValveResourcePathToAg(formatted, propertyName);
    }

    private static string FormatAgFloatingPoint(string value)
        => value.Contains('.', StringComparison.Ordinal) || value.Contains('e', StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{value}.0";

    private static string FormatAgPrimitiveArray(KVObject arrayValue)
    {
        var values = new string[arrayValue.Count];

        for (var i = 0; i < arrayValue.Count; i++)
        {
            values[i] = FormatAgScalar(arrayValue[i]);
        }

        return string.Join(",", values);
    }

    private static string MapValveGraphTypeToAg(string graphType)
        => graphType switch
        {
            "EntryOverrideTree" => "ValueTree",
            "GlobalTransitionConduit" => "ValueTree",
            "VirtualParameterValueTree" => "ValueTree",
            _ => graphType,
        };

    private static KVObject MapAgGraphTypeToValve(string graphType)
        => graphType switch
        {
            "BlendTree" => "BlendTree",
            "ValueTree" => "ValueTree",
            "TransitionConduit" => "TransitionConduit",
            _ => ParseAgScalar(graphType),
        };

    private static string MapValveValueTypeToAg(string valueType)
        => valueType switch
        {
            "BoneMask" => "Bone Mask",
            _ => valueType,
        };

    private static KVObject MapAgValueTypeToValve(string valueType)
        => valueType switch
        {
            "Bone Mask" => "BoneMask",
            _ => ParseAgScalar(valueType),
        };

    private static KVObject MapAgTypeValueToValve(string value)
    {
        var graphType = MapAgGraphTypeToValve(value);
        if (graphType.ValueType != KVValueType.String || graphType.ToString() != value)
        {
            return graphType;
        }

        return MapAgValueTypeToValve(value);
    }

    private static bool NeedsOriginalGraphTypePreservation(string graphType)
        => graphType is "EntryOverrideTree" or "GlobalTransitionConduit" or "VirtualParameterValueTree";

    private static string MapValveResourcePathToAg(string value, string? propertyName)
    {
        if (!ShouldRemapResourcePath(value, propertyName))
        {
            return value;
        }

        var normalized = value.Replace('\\', '/');

        if (TryMapValveGraphResourcePathToAg(normalized, propertyName, out var graphResourcePath))
        {
            return graphResourcePath;
        }

        if (!normalized.StartsWith("data://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"data://{normalized}";
        }

        if (normalized.EndsWith(".vnmclip", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^8] + ".anim";
        }
        else if (normalized.EndsWith(".vnmskel", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^8] + ".skel";
        }

        return normalized;
    }

    private static string MapAgResourcePathToValve(string value, string propertyName)
    {
        if (!ShouldRemapResourcePath(value, propertyName))
        {
            return value;
        }

        if (TryMapAgGraphResourcePathToValve(value, propertyName, out var graphResourcePath))
        {
            return graphResourcePath;
        }

        var normalized = value.StartsWith("data://", StringComparison.OrdinalIgnoreCase)
            ? value[7..]
            : value;

        if (normalized.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^5] + ".vnmclip";
        }
        else if (normalized.EndsWith(".skel", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^5] + ".vnmskel";
        }

        return normalized;
    }

    private static bool ShouldRemapResourcePath(string value, string? propertyName)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (propertyName is "m_animClip" or "m_skeleton" or "m_resourceID" or "m_defaultResourceID")
        {
            return true;
        }

        if (propertyName is "m_graphDefinition" or "m_variation")
        {
            return true;
        }

        return value.StartsWith("data://", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".vnmclip", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".vnmskel", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".vnmgraph", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".ag", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapValveGraphResourcePathToAg(string value, string? propertyName, out string mappedValue)
    {
        mappedValue = string.Empty;

        if (propertyName is not ("m_graphDefinition" or "m_variation"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            mappedValue = value;
            return true;
        }

        var plusIdx = value.IndexOf('+', StringComparison.Ordinal);
        if (plusIdx >= 0)
        {
            var baseGraphPath = value[..plusIdx];
            var variationPath = value[(plusIdx + 1)..];
            var variationName = Path.GetFileNameWithoutExtension(variationPath);

            if (string.IsNullOrWhiteSpace(variationName))
            {
                mappedValue = value;
                return true;
            }

            mappedValue = EnsureAgDataPath(baseGraphPath) + "/" + variationName + ".ag";
            return true;
        }

        mappedValue = EnsureAgDataPath(value);
        return true;
    }

    private static bool TryMapAgGraphResourcePathToValve(string value, string propertyName, out string mappedValue)
    {
        mappedValue = string.Empty;

        if (propertyName is not ("m_graphDefinition" or "m_variation"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            mappedValue = value;
            return true;
        }

        var normalized = value.Replace('\\', '/');
        if (normalized.StartsWith("data://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[7..];
        }

        var subresourceIdx = normalized.LastIndexOf(".ag/", StringComparison.OrdinalIgnoreCase);
        if (subresourceIdx >= 0)
        {
            var baseGraphPath = normalized[..(subresourceIdx + 3)];
            var variationPath = normalized[(subresourceIdx + 4)..];
            var variationName = Path.GetFileNameWithoutExtension(variationPath);

            if (string.IsNullOrWhiteSpace(variationName))
            {
                mappedValue = EnsureValveGraphPath(baseGraphPath);
                return true;
            }

            mappedValue = EnsureValveGraphPath(baseGraphPath) + "+" + variationName + ".vnmgraph";
            return true;
        }

        mappedValue = EnsureValveGraphPath(normalized);
        return true;
    }

    private static string EnsureAgDataPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("data://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"data://{normalized}";
        }

        if (normalized.EndsWith(".vnmgraph", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^9] + ".ag";
        }

        return normalized;
    }

    private static string EnsureValveGraphPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.EndsWith(".ag", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3] + ".vnmgraph";
        }

        return normalized;
    }

    private static Dictionary<string, string> CreateReverseDictionary(Dictionary<string, string> source)
    {
        var dictionary = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);

        foreach (var pair in source)
        {
            dictionary[pair.Value] = pair.Key;
        }

        return dictionary;
    }

    private static Dictionary<string, string> CreateAgToValvePropertyMap()
    {
        var dictionary = CreateReverseDictionary(ValveToAgPropertyMap);
        dictionary.Remove("m_type");
        return dictionary;
    }

    private static string MapValveClassName(string? valveClassName)
    {
        if (string.IsNullOrEmpty(valveClassName))
        {
            return "EE::Animation::UnknownToolsType";
        }

        if (ValveToAgClassMap.TryGetValue(valveClassName, out var mapped))
        {
            return mapped;
        }

        var stem = valveClassName
            .Replace("CNmGraphDoc", string.Empty, StringComparison.Ordinal)
            .Replace("CnmGraphDoc", string.Empty, StringComparison.Ordinal)
            .Replace("::CData", "::Data", StringComparison.Ordinal)
            .Replace("Node::CData", "ToolsNode::Data", StringComparison.Ordinal)
            .Replace("Node", "ToolsNode", StringComparison.Ordinal);

        return $"EE::Animation::{stem}";
    }

    private static string MapAgClassName(string agTypeId)
    {
        if (AgToValveClassMap.TryGetValue(agTypeId, out var mapped))
        {
            return mapped;
        }

        if (agTypeId == "EE::NodeGraph::Pin"
            || agTypeId == "EE::NodeGraph::FlowGraph::Connection"
            || agTypeId == "EE::Animation::Variation"
            || agTypeId == "EE::Animation::VariationHierarchy"
            || agTypeId.EndsWith("::TimedStateEvent", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var stem = agTypeId.Replace("EE::Animation::", string.Empty, StringComparison.Ordinal);
        stem = stem.Replace("::Data", "::CData", StringComparison.Ordinal);
        stem = stem.Replace("ToolsNode", "Node", StringComparison.Ordinal);
        return $"CNmGraphDoc{stem}";
    }

    private static string MapValvePropertyName(string propertyName)
        => ValveToAgPropertyMap.TryGetValue(propertyName, out var mapped) ? mapped : propertyName;

    private static string MapAgPropertyName(string propertyName)
        => AgToValvePropertyMap.TryGetValue(propertyName, out var mapped) ? mapped : propertyName;

    private static string? GetAgTypeIdForProperty(string propertyName, KVObject value)
    {
        if (value.TryGetValue("_class", out _))
        {
            return null;
        }

        return propertyName switch
        {
            "m_rootGraph" => "EE::Animation::FlowGraph",
            "m_childGraph" => GuessGraphType(value),
            "m_secondaryGraph" => GuessGraphType(value),
            "m_blendSpace" when IsValveBlend2DBlendSpace(value) => "EE::Animation::Blend2DToolsNode::BlendSpace",
            "m_variationHierarchy" => "EE::Animation::VariationHierarchy",
            "m_defaultVariationData" => GuessVariationDataType(value),
            "m_variationData" => GuessVariationDataType(value),
            "m_inputTimeRemapRange" or "m_clampRange" => "EE::FloatRange",
            "m_inputRange" or "m_outputRange" => "EE::Animation::FloatRemapNode::RemapRange",
            _ => null,
        };
    }

    private static string? GetAgTypeIdForArrayItem(string propertyName, KVObject value)
    {
        if (value.TryGetValue("_class", out _))
        {
            return null;
        }

        return propertyName switch
        {
            "m_nodes" => GuessNodeType(value),
            "m_connections" => "EE::NodeGraph::FlowGraph::Connection",
            "m_inputPins" or "m_outputPins" => "EE::NodeGraph::Pin",
            "m_variations" => "EE::Animation::Variation",
            "m_overrides" => "EE::Animation::VariationDataToolsNode::OverrideValue",
            "m_conditions" => "EE::Animation::GraphEventConditionToolsNode::Condition",
            "m_mappings" => "EE::Animation::IDToFloatToolsNode::Mapping",
            "m_timeRemainingEvents" or "m_timeElapsedEvents" => "EE::Animation::StateToolsNode::TimedStateEvent",
            _ => null,
        };
    }

    private static string GuessGraphType(KVObject value)
    {
        var graphType = value.GetStringProperty("m_graphType");
        return graphType == "StateMachine" ? "EE::Animation::StateMachineGraph" : "EE::Animation::FlowGraph";
    }

    private static string GuessNodeType(KVObject value)
        => MapValveClassName(value.GetStringProperty("_class"));

    private static string GuessVariationDataType(KVObject value)
    {
        if (value.TryGetValue("_class", out _))
        {
            return MapValveClassName(value.GetStringProperty("_class"));
        }

        if (value.TryGetValue("m_animClip", out _))
        {
            return "EE::Animation::AnimationClipToolsNode::Data";
        }

        return "EE::Animation::VariationDataToolsNode::Data";
    }

    private static bool IsValveBlend1DBlendSpace(KVObject value)
    {
        if (!value.TryGetValue("m_points", out var points) || !points.IsArray || points.Count == 0)
        {
            return false;
        }

        var firstPoint = points[0];
        return firstPoint.ValueType == KVValueType.Collection
            && firstPoint.ContainsKey("m_flValue")
            && firstPoint.ContainsKey("m_pinID");
    }

    private static bool IsValveBlend2DBlendSpace(KVObject value)
    {
        if (!value.TryGetValue("m_points", out var points) || !points.IsArray || points.Count == 0)
        {
            return false;
        }

        return points[0].IsArray;
    }
}
