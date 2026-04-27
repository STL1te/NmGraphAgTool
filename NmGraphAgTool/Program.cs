using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NmGraphAgTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        if (!TryParseArguments(args, out var options))
        {
            return 1;
        }

        try
        {
            var input = File.ReadAllText(options.InputPath);
            var output = options.Mode switch
            {
                ConversionMode.VnmGraphToAg => NmGraphAgConverter.ConvertVnmGraphToAg(input),
                ConversionMode.AgToVnmGraph => NmGraphAgConverter.ConvertAgToVnmGraph(input),
                _ => throw new InvalidOperationException($"Unsupported conversion mode: {options.Mode}"),
            };

            File.WriteAllText(options.OutputPath, output);
            Console.WriteLine($"Written {options.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool TryParseArguments(string[] args, out Options options)
    {
        options = default;

        if (args.Length == 0 || args.Any(arg => arg is "--help" or "-h"))
        {
            PrintUsage();
            return false;
        }

        try
        {
            string? inputPath = null;
            string? outputPath = null;
            ConversionMode? mode = null;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--vnmgraph-to-ag":
                        mode = SetMode(mode, ConversionMode.VnmGraphToAg);
                        break;
                    case "--ag-to-vnmgraph":
                        mode = SetMode(mode, ConversionMode.AgToVnmGraph);
                        break;
                    case "--input":
                    case "-i":
                        inputPath = ReadOptionValue(args, ref i, args[i]);
                        break;
                    case "--output":
                    case "-o":
                        outputPath = ReadOptionValue(args, ref i, args[i]);
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[i]}");
                        PrintUsage();
                        return false;
                }
            }

            if (mode is null)
            {
                Console.Error.WriteLine("Specify exactly one conversion mode.");
                PrintUsage();
                return false;
            }

            if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath))
            {
                Console.Error.WriteLine("Both input and output paths are required.");
                PrintUsage();
                return false;
            }

            options = new Options(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), mode.Value);
            return true;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return false;
        }
    }

    private static ConversionMode SetMode(ConversionMode? currentMode, ConversionMode newMode)
    {
        if (currentMode is not null)
        {
            throw new ArgumentException("Specify exactly one conversion mode.");
        }

        return newMode;
    }

    private static string ReadOptionValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  NmGraphAgTool --vnmgraph-to-ag -i <input.vnmgraph> -o <output.ag>");
        Console.WriteLine("  NmGraphAgTool --ag-to-vnmgraph -i <input.ag> -o <output.vnmgraph>");
    }

    private readonly record struct Options(string InputPath, string OutputPath, ConversionMode Mode);

    private enum ConversionMode
    {
        VnmGraphToAg,
        AgToVnmGraph,
    }
}
