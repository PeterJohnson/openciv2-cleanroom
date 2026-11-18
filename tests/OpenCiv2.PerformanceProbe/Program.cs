using OpenCiv2.PerformanceCases;

if (args.Length != 2) throw new ArgumentException("Expected input JSON and repetition count");
Console.WriteLine(Workloads.Run(File.ReadAllText(args[0]), int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), typeof(Program).Assembly));
