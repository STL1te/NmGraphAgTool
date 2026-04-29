using System;
using System.IO;
using Xunit;

namespace NmGraphAgTool.Tests;

public sealed class WatchModeTests
{
    [Fact]
    public void TryGetRelativeOutputPath_MapsVnmGraphToAg()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine("C:", "graphs", "src"));
        var inputPath = Path.Combine(sourceDirectory, "locomotion", "idle.vnmgraph");

        var success = Program.TryGetRelativeOutputPath(
            inputPath,
            sourceDirectory,
            Program.ConversionMode.VnmGraphToAg,
            out var relativeOutputPath);

        Assert.True(success);
        Assert.Equal(Path.Combine("locomotion", "idle.ag"), relativeOutputPath);
    }

    [Fact]
    public void TryGetRelativeOutputPath_MapsAgToVnmGraph()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine("C:", "graphs", "src"));
        var inputPath = Path.Combine(sourceDirectory, "combat", "attack.ag");

        var success = Program.TryGetRelativeOutputPath(
            inputPath,
            sourceDirectory,
            Program.ConversionMode.AgToVnmGraph,
            out var relativeOutputPath);

        Assert.True(success);
        Assert.Equal(Path.Combine("combat", "attack.vnmgraph"), relativeOutputPath);
    }

    [Fact]
    public void TryGetRelativeOutputPath_RejectsUnexpectedExtension()
    {
        var sourceDirectory = Path.GetFullPath(Path.Combine("C:", "graphs", "src"));
        var inputPath = Path.Combine(sourceDirectory, "readme.txt");

        var success = Program.TryGetRelativeOutputPath(
            inputPath,
            sourceDirectory,
            Program.ConversionMode.VnmGraphToAg,
            out var relativeOutputPath);

        Assert.False(success);
        Assert.Null(relativeOutputPath);
    }
}
