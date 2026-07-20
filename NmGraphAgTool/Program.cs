using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NmGraphAgTool.Converters;

namespace NmGraphAgTool;

internal static class Program
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
            return options.Watch
                ? RunWatchMode(options)
                : RunSingleConversion(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunSingleConversion(Options options)
    {
        var output = ConvertFile(options.InputPath, options.Mode);
        File.WriteAllText(options.OutputPath, output);
        Console.WriteLine($"Written {options.OutputPath}");
        return 0;
    }

    private static int RunWatchMode(Options options)
    {
        if (!Directory.Exists(options.InputPath))
        {
            throw new DirectoryNotFoundException($"Input directory does not exist: {options.InputPath}");
        }

        Directory.CreateDirectory(options.OutputPath);
        ConvertExistingFiles(options);

        using var watcher = new FileSystemWatcher(options.InputPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            Filter = "*.*",
            EnableRaisingEvents = true,
        };

        using var stopSignal = new ManualResetEventSlim(false);
        using var eventQueue = new BlockingCollection<string>();
        var timerByPath = new ConcurrentDictionary<string, Timer>(PathComparer);

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopSignal.Set();
        };

        FileSystemEventHandler fileHandler = (_, eventArgs) => QueuePath(eventArgs.FullPath, eventQueue, timerByPath);
        RenamedEventHandler renameHandler = (_, eventArgs) => QueuePath(eventArgs.FullPath, eventQueue, timerByPath);

        Console.CancelKeyPress += cancelHandler;
        watcher.Created += fileHandler;
        watcher.Changed += fileHandler;
        watcher.Renamed += renameHandler;

        try
        {
            Console.WriteLine($"Watching {options.InputPath} -> {options.OutputPath}. Press Ctrl+C to stop.");

            while (!stopSignal.IsSet)
            {
                if (!eventQueue.TryTake(out var path, 250))
                {
                    continue;
                }

                try
                {
                    ConvertPath(path, options);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to convert {path}: {ex.Message}");
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            watcher.Created -= fileHandler;
            watcher.Changed -= fileHandler;
            watcher.Renamed -= renameHandler;

            foreach (var timer in timerByPath.Values)
            {
                timer.Dispose();
            }
        }

        return 0;
    }

    private static void ConvertExistingFiles(Options options)
    {
        foreach (var path in Directory.EnumerateFiles(options.InputPath, "*.*", SearchOption.AllDirectories))
        {
            ConvertPath(path, options);
        }
    }

    private static void ConvertPath(string path, Options options)
    {
        if (!TryGetRelativeOutputPath(path, options.InputPath, options.Mode, out var relativeOutputPath))
        {
            return;
        }

        WaitForFile(path);

        var outputPath = Path.Combine(options.OutputPath, relativeOutputPath!);
        var output = ConvertFile(path, options.Mode);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, output);
        Console.WriteLine($"Written {outputPath}");
    }

    internal static bool TryGetRelativeOutputPath(string inputPath, string sourceDirectory, ConversionMode mode, out string? relativeOutputPath)
    {
        relativeOutputPath = null;

        var expectedExtension = GetInputExtension(mode);
        if (!string.Equals(Path.GetExtension(inputPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativeInputPath = Path.GetRelativePath(sourceDirectory, inputPath);
        var outputExtension = GetOutputExtension(mode);
        relativeOutputPath = Path.ChangeExtension(relativeInputPath, outputExtension);
        return true;
    }

    private static string ConvertFile(string inputPath, ConversionMode mode)
    {
        var input = File.ReadAllText(inputPath);
        return mode switch
        {
            ConversionMode.VnmGraphToAg => EsoAgConverter.ConvertVnmGraphToAg(input),
            ConversionMode.AgToVnmGraph => EsoAgConverter.ConvertAgToVnmGraph(input),
            _ => throw new InvalidOperationException($"Unsupported conversion mode: {mode}"),
        };
    }

    private static void QueuePath(string path, BlockingCollection<string> eventQueue, ConcurrentDictionary<string, Timer> timerByPath)
    {
        timerByPath.AddOrUpdate(
            path,
            queuedPath => new Timer(_ => EnqueuePath(queuedPath, eventQueue, timerByPath), null, 200, Timeout.Infinite),
            (_, existingTimer) =>
            {
                existingTimer.Change(200, Timeout.Infinite);
                return existingTimer;
            });
    }

    private static void EnqueuePath(string path, BlockingCollection<string> eventQueue, ConcurrentDictionary<string, Timer> timerByPath)
    {
        if (timerByPath.TryRemove(path, out var timer))
        {
            timer.Dispose();
        }

        eventQueue.Add(path);
    }

    private static void WaitForFile(string path)
    {
        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static string GetInputExtension(ConversionMode mode)
        => mode switch
        {
            ConversionMode.VnmGraphToAg => ".vnmgraph",
            ConversionMode.AgToVnmGraph => ".ag",
            _ => throw new InvalidOperationException($"Unsupported conversion mode: {mode}"),
        };

    private static string GetOutputExtension(ConversionMode mode)
        => mode switch
        {
            ConversionMode.VnmGraphToAg => ".ag",
            ConversionMode.AgToVnmGraph => ".vnmgraph",
            _ => throw new InvalidOperationException($"Unsupported conversion mode: {mode}"),
        };

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
            var watch = false;

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
                    case "--watch":
                        watch = true;
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

            options = new Options(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), mode.Value, watch);

            if (!options.Watch && Directory.Exists(options.InputPath))
            {
                throw new ArgumentException(
                    "Input path points to a directory, but --watch was not parsed. " +
                    "If you passed a quoted Windows path ending with '\\' before --watch, remove the trailing slash or escape it as '\\\\'.");
            }

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
        Console.WriteLine("  NmGraphAgTool --vnmgraph-to-ag -i <input-folder> -o <output-folder> --watch");
        Console.WriteLine("  NmGraphAgTool --ag-to-vnmgraph -i <input-folder> -o <output-folder> --watch");
    }

    private readonly record struct Options(string InputPath, string OutputPath, ConversionMode Mode, bool Watch);

    internal enum ConversionMode
    {
        VnmGraphToAg,
        AgToVnmGraph,
    }
}
