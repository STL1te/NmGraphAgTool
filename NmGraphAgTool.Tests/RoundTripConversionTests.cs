using System.Text;
using NmGraphAgTool;
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
            var ag = NmGraphAgConverter.ConvertVnmGraphToAg(original);
            var roundTripped = NmGraphAgConverter.ConvertAgToVnmGraph(ag);

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
